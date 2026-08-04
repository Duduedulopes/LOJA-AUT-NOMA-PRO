/* ============================================================
   Smart Store AI — núcleo compartilhado
   Usado pelo app do cliente (/) e pelo painel admin (/admin).
   Hoje persiste em localStorage; para migrar para a API .NET,
   troque apenas as funções do objeto Dados.
   ============================================================ */

const DB_PRODUTOS  = "ss_produtos_v1";
const DB_PERGUNTAS = "ss_perguntas_v1";
const DB_CONFIG    = "ss_config_v1";

const CATEGORIAS = {
  energetico: "energéticos",
  refrigerante: "refrigerantes",
  agua: "águas",
  suco: "sucos",
  cerveja: "cervejas",
  snack: "salgadinhos",
  mercearia: "mercearia"
};

const SEMENTE = [
  {id:1,  nome:"Red Bull Energy Drink 250ml",     cat:"energetico",   preco:9.90,  estoque:48,  corredor:4, prat:"superior", promo:"",                       sinonimos:"red bull, redbull"},
  {id:2,  nome:"Monster Energy 473ml",            cat:"energetico",   preco:11.50, estoque:32,  corredor:4, prat:"superior", promo:"Leve 2 pague 1",         sinonimos:"monster, monstro"},
  {id:3,  nome:"TNT Energy Drink 269ml",          cat:"energetico",   preco:6.49,  estoque:60,  corredor:4, prat:"meio",     promo:"",                       sinonimos:"tnt"},
  {id:4,  nome:"Fusion Energy 2L",                cat:"energetico",   preco:19.90, estoque:0,   corredor:4, prat:"inferior", promo:"",                       sinonimos:"fusion"},
  {id:5,  nome:"Coca-Cola Original 2L",           cat:"refrigerante", preco:10.99, estoque:120, corredor:3, prat:"inferior", promo:"R$ 8,99 na compra de 2", sinonimos:"coca, coca cola, cocacola"},
  {id:6,  nome:"Coca-Cola Zero Lata 350ml",       cat:"refrigerante", preco:4.79,  estoque:210, corredor:3, prat:"meio",     promo:"",                       sinonimos:"coca zero, coca cola zero"},
  {id:7,  nome:"Guaraná Antarctica 2L",           cat:"refrigerante", preco:8.49,  estoque:75,  corredor:3, prat:"inferior", promo:"",                       sinonimos:"guarana, antarctica"},
  {id:8,  nome:"Pepsi Black Lata 350ml",          cat:"refrigerante", preco:4.29,  estoque:0,   corredor:3, prat:"meio",     promo:"",                       sinonimos:"pepsi"},
  {id:9,  nome:"Sprite Limão 1,5L",               cat:"refrigerante", preco:7.99,  estoque:44,  corredor:3, prat:"meio",     promo:"",                       sinonimos:"sprite"},
  {id:10, nome:"Água Mineral Crystal 500ml",      cat:"agua",         preco:2.49,  estoque:300, corredor:2, prat:"inferior", promo:"",                       sinonimos:"agua, crystal, agua mineral"},
  {id:11, nome:"Água com Gás Perrier 330ml",      cat:"agua",         preco:8.90,  estoque:26,  corredor:2, prat:"superior", promo:"",                       sinonimos:"agua com gas, perrier, com gas"},
  {id:12, nome:"Água de Coco Kero Coco 1L",       cat:"agua",         preco:12.90, estoque:18,  corredor:2, prat:"meio",     promo:"15% off",                sinonimos:"agua de coco, coco, kero coco"},
  {id:13, nome:"Suco de Laranja Del Valle 1L",    cat:"suco",         preco:9.29,  estoque:52,  corredor:2, prat:"meio",     promo:"",                       sinonimos:"suco, suco de laranja, del valle, laranja"},
  {id:14, nome:"Suco de Uva Integral 1,5L",       cat:"suco",         preco:16.90, estoque:21,  corredor:2, prat:"superior", promo:"",                       sinonimos:"suco de uva, uva"},
  {id:15, nome:"Cerveja Heineken Long Neck",      cat:"cerveja",      preco:7.49,  estoque:180, corredor:5, prat:"inferior", promo:"6 por R$ 39,90",         sinonimos:"heineken, cerveja, long neck"},
  {id:16, nome:"Cerveja Brahma Duplo Malte 350ml",cat:"cerveja",      preco:4.19,  estoque:240, corredor:5, prat:"inferior", promo:"",                       sinonimos:"brahma, duplo malte"},
  {id:17, nome:"Batata Ruffles Original 96g",     cat:"snack",        preco:11.90, estoque:64,  corredor:6, prat:"meio",     promo:"",                       sinonimos:"ruffles, batata, salgadinho"},
  {id:18, nome:"Doritos Queijo Nacho 84g",        cat:"snack",        preco:10.49, estoque:38,  corredor:6, prat:"meio",     promo:"2 por R$ 18,00",         sinonimos:"doritos, nacho"},
  {id:19, nome:"Amendoim Japonês 150g",           cat:"snack",        preco:7.90,  estoque:0,   corredor:6, prat:"superior", promo:"",                       sinonimos:"amendoim, amendoim japones"},
  {id:20, nome:"Pipoca de Micro-ondas 100g",      cat:"snack",        preco:4.99,  estoque:88,  corredor:6, prat:"inferior", promo:"",                       sinonimos:"pipoca, microondas"},
  {id:21, nome:"Café Solúvel Nescafé 160g",       cat:"mercearia",    preco:22.90, estoque:35,  corredor:7, prat:"meio",     promo:"",                       sinonimos:"cafe, nescafe, cafe soluvel"},
  {id:22, nome:"Achocolatado Nescau 400g",        cat:"mercearia",    preco:13.49, estoque:47,  corredor:7, prat:"meio",     promo:"Leve 2 pague 1",         sinonimos:"nescau, achocolatado, chocolate em po"},
  {id:23, nome:"Leite Integral Italac 1L",        cat:"mercearia",    preco:5.79,  estoque:96,  corredor:7, prat:"inferior", promo:"",                       sinonimos:"leite, italac"}
];

/* ---------- persistência ---------- */
const Dados = {
  produtos(){
    try{
      const s = localStorage.getItem(DB_PRODUTOS);
      if(s) return JSON.parse(s);
    }catch(e){}
    try{ localStorage.setItem(DB_PRODUTOS, JSON.stringify(SEMENTE)); }catch(e){}
    return SEMENTE.slice();
  },
  salvarProdutos(lista){
    localStorage.setItem(DB_PRODUTOS, JSON.stringify(lista));
  },
  restaurar(){
    localStorage.setItem(DB_PRODUTOS, JSON.stringify(SEMENTE));
  },
  perguntas(){
    try{ return JSON.parse(localStorage.getItem(DB_PERGUNTAS)) || []; }catch(e){ return []; }
  },
  registrarPergunta(texto, respondida, produto){
    const lista = Dados.perguntas();
    lista.unshift({
      texto: texto,
      respondida: !!respondida,
      produto: produto || "",
      quando: new Date().toISOString()
    });
    try{ localStorage.setItem(DB_PERGUNTAS, JSON.stringify(lista.slice(0,300))); }catch(e){}
  },
  limparPerguntas(){ localStorage.removeItem(DB_PERGUNTAS); },
  config(){
    try{ return JSON.parse(localStorage.getItem(DB_CONFIG)) || {}; }catch(e){ return {}; }
  },
  salvarConfig(c){
    localStorage.setItem(DB_CONFIG, JSON.stringify(Object.assign(Dados.config(), c)));
  },
  proximoId(){
    const l = Dados.produtos();
    return l.length ? Math.max.apply(null, l.map(p=>p.id)) + 1 : 1;
  }
};

/* ---------- utilidades ---------- */
const norm = s => (s||"").toLowerCase()
  .normalize("NFD").replace(/[\u0300-\u036f]/g,"")
  .replace(/[^a-z0-9\s]/g," ").replace(/\s+/g," ").trim();

const brl = v => "R$ " + Number(v).toFixed(2).replace(".",",");

const contem = (t,arr)=>{
  const n = norm(t);
  return arr.some(w=>new RegExp("\\b"+norm(w).replace(/\s+/g,"\\s+")).test(n));
};

const sinonimosDe = p => (p.sinonimos||"").split(",").map(s=>s.trim()).filter(Boolean);

/* ---------- busca ---------- */
function acharProdutos(txt, lista){
  const t = norm(txt), achados = [];
  (lista || Dados.produtos()).forEach(p=>{
    let score = 0;
    sinonimosDe(p).forEach(s=>{ const n=norm(s); if(n && t.includes(n)) score += n.length*2; });
    norm(p.nome).split(" ").forEach(w=>{ if(w.length>3 && t.includes(w)) score += w.length; });
    if(score>0) achados.push({p,score});
  });
  return achados.sort((a,b)=>b.score-a.score).map(x=>x.p);
}

function acharCategoria(txt){
  const t = norm(txt);
  const mapa = {
    energetico:["energetico","energeticos","energia"],
    refrigerante:["refrigerante","refrigerantes","refri"],
    agua:["agua","aguas"], suco:["suco","sucos"],
    cerveja:["cerveja","cervejas","alcool","alcoolica"],
    snack:["salgadinho","salgadinhos","snack","snacks","chips"],
    mercearia:["mercearia","alimento","alimentos"]
  };
  for(const k in mapa) if(mapa[k].some(w=>t.includes(w))) return k;
  return null;
}

const ABERTA = ["combina","combinam","sugere","sugestao","sugestoes","recomenda","recomendacao",
  "melhor","ideal","serve para","serve pra","montar","escolher","sem acucar","sem gluten",
  "sem lactose","saudavel","light","diet","o que levo","festa","churrasco","cafe da manha",
  "lanche da tarde","criancas","dieta","vale a pena"];

/* ---------- motor de resposta local ---------- */
/* assuntos de plataforma: cadastro, conta, pagamento, entrega.
   Não são catálogo — vão direto para o SmartGO. */
const PLATAFORMA = ["cadastro","cadastrar","me cadastro","conta","login","senha","entrar",
  "pagamento","pagar","pix","cartao","boleto","parcelar","fatura","cupom","desconto no app",
  "entrega","frete","pedido","rastrear","nota fiscal","troca","devolucao","reembolso",
  "cancelar","suporte","atendente","como funciona a loja","politica","privacidade"];

function responderLocal(txt){
  const t = norm(txt);
  const PRODUTOS = Dados.produtos();

  if(contem(t, PLATAFORMA)) return null;
  if(contem(t, ABERTA)) return null;

  if(contem(t,["ola","oi","bom dia","boa tarde","boa noite","tudo bem","opa"]) && t.length < 28)
    return {fala:"Olá. Consulte preço, estoque e localização de qualquer produto da loja. O que você procura?"};

  if(contem(t,["obrigado","obrigada","valeu","tchau","ate mais"]))
    return {fala:"À disposição. Boas compras."};

  if(contem(t,["quem e voce","voce e um robo","seu nome","como funciona","voce e real"]))
    return {fala:"Sou o assistente de catálogo da Smart Store. Consulto os dados da loja em tempo real, então preço e estoque estão sempre atualizados."};

  if(contem(t,["promocao","promocoes","oferta","ofertas","desconto","descontos"])){
    const promos = PRODUTOS.filter(p=>p.promo && p.estoque>0);
    if(!promos.length) return {fala:"Não há promoções ativas no momento."};
    return {
      fala:promos.length+" promoções ativas hoje. As principais: "+promos.slice(0,3).map(p=>p.nome.replace(/\s+\d.*$/,"")).join(", ")+".",
      card:{titulo:"Promoções de hoje", linhas:promos.map(p=>[p.nome, p.promo])}
    };
  }

  if(contem(t,["quantos produtos","catalogo","o que tem na loja","o que voces tem"]))
    return {fala:PRODUTOS.length+" produtos cadastrados, em "+Object.values(CATEGORIAS).join(", ")+"."};

  const prods = acharProdutos(txt, PRODUTOS);
  const cat = acharCategoria(txt);

  if(prods.length === 0 && cat){
    const lista = PRODUTOS.filter(p=>p.cat===cat);
    if(!lista.length) return null;
    return {
      fala:CATEGORIAS[cat].charAt(0).toUpperCase()+CATEGORIAS[cat].slice(1)+": "+lista.length+" opções, no corredor "+lista[0].corredor+".",
      card:{titulo:"Opções em "+CATEGORIAS[cat], linhas:lista.map(p=>[p.nome, p.estoque>0?brl(p.preco):"esgotado"])}
    };
  }
  if(prods.length === 0) return null;

  const p = prods[0];
  const onde  = contem(t,["onde","fica","acho","encontro","corredor","localiza","achar","encontrar"]);
  const preco = contem(t,["preco","precos","custa","quanto","valor"]);
  const est   = contem(t,["tem","possui","estoque","disponivel","acabou","vende","vendem","resta"]);
  if(!onde && !preco && !est && norm(txt).split(" ").length > 7) return null;

  const linhas = [
    ["Preço", p.estoque>0 ? brl(p.preco) : "—"],
    ["Localização", "corredor "+p.corredor+", prateleira "+p.prat],
    ["Estoque", p.estoque>0 ? p.estoque+" unidades" : "esgotado"]
  ];
  if(p.promo) linhas.push(["Promoção", p.promo]);
  const card = {titulo:p.nome, linhas:linhas, esgotado:p.estoque===0, promo:p.promo};

  if(p.estoque === 0){
    const alt = PRODUTOS.filter(x=>x.cat===p.cat && x.estoque>0)[0];
    return {fala:p.nome+" está esgotado."+(alt?" Alternativa disponível: "+alt.nome+", "+brl(alt.preco)+", corredor "+alt.corredor+".":""), card:card, produto:p.nome, esgotado:true};
  }
  if(onde && !preco)
    return {fala:p.nome+" fica no corredor "+p.corredor+", prateleira "+p.prat+". "+p.estoque+" unidades em estoque.", card:card, produto:p.nome};
  if(preco && !onde)
    return {fala:p.nome+" custa "+brl(p.preco)+"."+(p.promo?" Em promoção: "+p.promo+".":""), card:card, produto:p.nome};
  if(est && !preco && !onde)
    return {fala:"Sim, disponível. "+p.estoque+" unidades de "+p.nome+", no corredor "+p.corredor+".", card:card, produto:p.nome};

  return {fala:p.nome+" custa "+brl(p.preco)+" e fica no corredor "+p.corredor+", prateleira "+p.prat+". "+p.estoque+" unidades em estoque."+(p.promo?" Em promoção: "+p.promo+".":""), card:card, produto:p.nome};
}

/* ============================================================
   PONTE PARA O ASSISTENTE SMARTGO
   ------------------------------------------------------------
   Quando a camada de catálogo não sabe responder, a pergunta é
   encaminhada para o assistente que já existe.

   Configure a URL em /admin → Ajustes. O corpo enviado é:

     { "mensagem": "...", "historico": [ {role, content}, ... ],
       "origem": "smart-store-catalogo" }

   A resposta pode vir em qualquer um destes formatos — a função
   aceita todos, então provavelmente não é preciso mudar sua API:

     { "resposta": "texto" }
     { "mensagem": "texto" }
     { "reply": "texto" }
     { "choices": [ { "message": { "content": "texto" } } ] }
     "texto puro"

   Se a sua API usa outro nome de campo, ajuste apenas o array
   CAMPOS_RESPOSTA abaixo.
   ============================================================ */

const CAMPOS_RESPOSTA = ["resposta","mensagem","reply","message","texto","answer","output"];

function extrairResposta(dado){
  if(typeof dado === "string") return dado;
  if(!dado || typeof dado !== "object") return null;
  for(const c of CAMPOS_RESPOSTA){
    if(typeof dado[c] === "string" && dado[c].trim()) return dado[c];
  }
  const oa = dado.choices && dado.choices[0] && dado.choices[0].message && dado.choices[0].message.content;
  if(typeof oa === "string" && oa.trim()) return oa;
  if(dado.data) return extrairResposta(dado.data);
  return null;
}

async function responderSmartGO(mensagem, historico){
  const cfg = Dados.config();
  if(!cfg.smartgoUrl) throw new Error("URL do SmartGO não configurada");

  const cab = {"Content-Type":"application/json"};
  if(cfg.smartgoToken) cab["Authorization"] = "Bearer " + cfg.smartgoToken;

  const res = await fetch(cfg.smartgoUrl, {
    method:"POST",
    headers:cab,
    body:JSON.stringify({
      mensagem: mensagem,
      historico: historico || [],
      origem: "smart-store-catalogo"
    })
  });

  if(!res.ok) throw new Error("HTTP "+res.status);

  const bruto = await res.text();
  let dado;
  try{ dado = JSON.parse(bruto); }catch(e){ dado = bruto; }

  const txt = extrairResposta(dado);
  if(!txt) throw new Error("resposta não reconhecida");

  return {fala: txt.trim().replace(/[*_#]/g,""), via:"SmartGO"};
}

/* ---------- LLM opcional (OpenRouter) ---------- */
const MODELOS_FALLBACK = [
  "deepseek/deepseek-chat-v3-0324:free",
  "meta-llama/llama-3.3-70b-instruct:free",
  "qwen/qwen-2.5-72b-instruct:free",
  "google/gemma-3-27b-it:free",
  "mistralai/mistral-small-3.2-24b-instruct:free"
];

function promptSistema(){
  const cat = Dados.produtos().map(p=>
    p.nome+" | "+CATEGORIAS[p.cat]+" | "+brl(p.preco)+" | corredor "+p.corredor+", prateleira "+p.prat+
    " | "+(p.estoque>0 ? p.estoque+" un." : "ESGOTADO")+(p.promo ? " | promo: "+p.promo : "")
  ).join("\n");

  return "Você é o assistente de catálogo da loja Smart Store. Fale em português do Brasil.\n\n"+
    "REGRAS OBRIGATÓRIAS\n"+
    "1. Responda APENAS sobre os produtos da lista abaixo. Nada fora dela existe na loja.\n"+
    "2. NUNCA invente preço, estoque ou corredor. Use exatamente os valores da lista.\n"+
    "3. Se pedirem algo que não está na lista, diga que a loja não tem e sugira o mais parecido.\n"+
    "4. Seja breve e objetivo: no máximo 3 frases curtas.\n"+
    "5. Tom profissional e direto. Sem emoji e sem asteriscos.\n\n"+
    "CATÁLOGO DA LOJA\n"+cat;
}

async function responderLLM(historico){
  const cfg = Dados.config();
  if(!cfg.orKey) throw new Error("sem chave");
  const escolhido = (cfg.orModelo && cfg.orModelo !== "auto") ? cfg.orModelo : null;
  const lista = escolhido ? [escolhido].concat(MODELOS_FALLBACK.filter(m=>m!==escolhido)) : MODELOS_FALLBACK;
  let ultimo = null;

  for(const modelo of lista){
    try{
      const res = await fetch("https://openrouter.ai/api/v1/chat/completions",{
        method:"POST",
        headers:{
          "Authorization":"Bearer "+cfg.orKey,
          "Content-Type":"application/json",
          "HTTP-Referer": location.origin,
          "X-Title":"Smart Store AI"
        },
        body:JSON.stringify({
          model:modelo, temperature:0.5, max_tokens:220,
          messages:[{role:"system",content:promptSistema()}].concat(historico)
        })
      });
      if(!res.ok) throw new Error("HTTP "+res.status);
      const j = await res.json();
      const txt = j && j.choices && j.choices[0] && j.choices[0].message && j.choices[0].message.content;
      if(!txt) throw new Error("resposta vazia");
      return {fala: txt.trim().replace(/[*_#]/g,""), via:"LLM · "+modelo.split("/")[1].replace(":free","")};
    }catch(e){ ultimo = e; }
  }
  throw ultimo || new Error("falha na LLM");
}
