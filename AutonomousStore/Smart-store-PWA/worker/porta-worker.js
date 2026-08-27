/* ============================================================
   Smart Store — Worker da porta
   ------------------------------------------------------------
   Caixa de correio entre o leitor (tablet) e o cliente (celular).

   O tablet, ao validar um QR, deposita aqui um recado.
   O celular pergunta a cada 2 segundos se tem recado para ele.
   O recado é entregue uma única vez e depois some.

   Este Worker NÃO decide nada sobre acesso. Quem valida a
   assinatura do QR é o leitor. Aqui só passa mensagem.

   Rotas
     POST /entrada          deposita um recado
     GET  /entrada?id=XXX   busca e consome o recado da conta XXX
     GET  /log              últimas leituras (para o painel admin)
     GET  /                 verificação de saúde

   Precisa de um KV chamado PORTA vinculado ao Worker.
   ============================================================ */

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type"
};

function json(corpo, status){
  return new Response(JSON.stringify(corpo), {
    status: status || 200,
    headers: Object.assign({"Content-Type": "application/json; charset=utf-8"}, CORS)
  });
}

export default {
  async fetch(request, env){
    const url = new URL(request.url);

    if(request.method === "OPTIONS")
      return new Response(null, {headers: CORS});

    /* aceita o KV vinculado como PORTA ou como KV — tanto faz o nome */
    const gaveta = env.PORTA || env.KV;

    if(!gaveta)
      return json({erro:"nenhum KV vinculado. Vincule um KV com nome de variável PORTA ou KV"}, 500);

    /* ---------- o tablet deposita o recado ---------- */
    if(url.pathname === "/entrada" && request.method === "POST"){
      let dados;
      try{ dados = await request.json(); }
      catch(e){ return json({erro:"corpo inválido"}, 400); }

      if(!dados || !dados.id) return json({erro:"faltou o id da conta"}, 400);

      const evento = {
        id: String(dados.id),
        nome: String(dados.nome || ""),
        ok: !!dados.ok,
        motivo: String(dados.motivo || ""),
        quando: new Date().toISOString()
      };

      /* recado individual: expira em 2 minutos se ninguém pegar */
      await gaveta.put("ent:" + evento.id, JSON.stringify(evento), {expirationTtl: 120});

      /* histórico: guarda por 24 horas, para o painel */
      await gaveta.put("log:" + Date.now() + ":" + evento.id,
                          JSON.stringify(evento), {expirationTtl: 86400});

      return json({ok:true, entregue:false});
    }

    /* ---------- o celular busca o recado ---------- */
    if(url.pathname === "/entrada" && request.method === "GET"){
      const id = url.searchParams.get("id");
      if(!id) return json({erro:"faltou o id"}, 400);

      const bruto = await gaveta.get("ent:" + id);
      if(!bruto) return json({evento:null});

      await gaveta.delete("ent:" + id);   /* entrega uma vez só */
      return json({evento: JSON.parse(bruto)});
    }

    /* ============================================================
       CLIENTE ATIVO NA LOJA
       Quando a porta libera alguém, esse alguém vira o "cliente
       ativo". É para ele que as tags RFID lidas vão.
       Vale por 1 hora ou até outro cliente entrar.
       ============================================================ */
    if(url.pathname === "/ativo"){
      if(request.method === "POST"){
        let d;
        try{ d = await request.json(); }catch(e){ return json({erro:"corpo inválido"}, 400); }
        if(!d || !d.id) return json({erro:"faltou o id"}, 400);
        await gaveta.put("ativo", JSON.stringify({
          id: String(d.id), nome: String(d.nome||""), desde: new Date().toISOString()
        }), {expirationTtl: 3600});
        return json({ok:true});
      }
      if(request.method === "GET"){
        const b = await gaveta.get("ativo");
        return json({ativo: b ? JSON.parse(b) : null});
      }
    }

    /* ============================================================
       TAGS RFID
       POST /produto   { tag:"A1B2C3D4" }   → vem do ESP32
       GET  /produto?id=C123                → o celular busca e consome
       ============================================================ */
    if(url.pathname === "/produto" && request.method === "POST"){
      let d;
      try{ d = await request.json(); }catch(e){ return json({erro:"corpo inválido"}, 400); }

      const tag = String((d && d.tag) || "").trim().toUpperCase();
      if(!tag) return json({erro:"faltou a tag"}, 400);

      /* para quem vai? cliente informado, ou o ativo da loja */
      let destino = d && d.cliente ? String(d.cliente) : null;
      if(!destino){
        const a = await gaveta.get("ativo");
        if(!a) return json({erro:"nenhum cliente ativo na loja", tag:tag}, 409);
        destino = JSON.parse(a).id;
      }

      /* evita leitura repetida da mesma tag em poucos segundos */
      const marca = "vista:" + destino + ":" + tag;
      if(await gaveta.get(marca)) return json({ok:true, repetida:true, tag:tag});
      await gaveta.put(marca, "1", {expirationTtl: 60});

      /* fila de tags pendentes desse cliente */
      const chave = "prod:" + destino;
      let fila = [];
      const atual = await gaveta.get(chave);
      if(atual){ try{ fila = JSON.parse(atual); }catch(e){} }
      fila.push({tag: tag, quando: new Date().toISOString()});

      await gaveta.put(chave, JSON.stringify(fila.slice(-30)), {expirationTtl: 900});
      return json({ok:true, tag:tag, cliente:destino, naFila:fila.length});
    }

    if(url.pathname === "/produto" && request.method === "GET"){
      const id = url.searchParams.get("id");
      if(!id) return json({erro:"faltou o id"}, 400);

      const bruto = await gaveta.get("prod:" + id);
      if(!bruto) return json({tags: []});

      await gaveta.delete("prod:" + id);
      return json({tags: JSON.parse(bruto)});
    }

    /* ---------- histórico para o painel ---------- */
    if(url.pathname === "/log" && request.method === "GET"){
      const lista = await gaveta.list({prefix:"log:", limit:60});
      const eventos = [];
      for(const k of lista.keys){
        const v = await gaveta.get(k.name);
        if(v) eventos.push(JSON.parse(v));
      }
      eventos.sort((a,b)=> a.quando < b.quando ? 1 : -1);
      return json({eventos});
    }

    if(url.pathname === "/")
      return json({servico:"smart-store-porta", ok:true, hora:new Date().toISOString()});

    return json({erro:"rota desconhecida"}, 404);
  }
};
