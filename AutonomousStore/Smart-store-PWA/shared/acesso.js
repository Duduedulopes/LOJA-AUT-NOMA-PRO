/* ============================================================
   Smart Store — controle de acesso
   ------------------------------------------------------------
   O celular do cliente gera um QR contendo um token assinado e
   válido por poucos segundos. O tablet na porta lê esse QR e
   valida a assinatura SOZINHO, sem precisar de servidor.

   Formato do token:  SSA1.<payload em base64url>.<assinatura>
   payload = { id, nome, exp, nonce }

   IMPORTANTE — segurança
   Nesta versão o segredo está no front-end, o que só é aceitável
   em demonstração. Em produção o leitor deve validar por chave
   pública (o celular assina com a privada, guardada no servidor)
   ou consultar a API. Troque apenas as funções assinar() e
   validarToken() — o resto da interface continua igual.
   ============================================================ */

const SEGREDO_DEMO = "smart-store-demo-2026";
const VALIDADE_SEGUNDOS = 60;

/* ------------------------------------------------------------
   ENDEREÇO DO WORKER DA PORTA
   Cole aqui o endereço do seu Worker, sem barra no final.
   Instruções completas em worker/LEIA-ME.md

   Exemplo:
   const URL_PORTA = "https://smart-store-porta.seu-usuario.workers.dev";

   Deixando vazio, tudo continua funcionando — o tablet valida e
   libera normalmente, o celular é que não recebe a confirmação.
   ------------------------------------------------------------ */
const URL_PORTA = "https://smart-store-porta.contato-dudulopes.workers.dev";

const DB_CONTA    = "ss_conta_v1";
const DB_ENTRADAS = "ss_entradas_v1";
const DB_CARRINHO = "ss_carrinho_v1";
const DB_LOJA     = "ss_loja_v1";

/* ---------- base64url ---------- */
const b64u = {
  enc(str){
    return btoa(unescape(encodeURIComponent(str)))
      .replace(/\+/g,"-").replace(/\//g,"_").replace(/=+$/,"");
  },
  dec(str){
    str = str.replace(/-/g,"+").replace(/_/g,"/");
    while(str.length % 4) str += "=";
    return decodeURIComponent(escape(atob(str)));
  }
};

/* ---------- assinatura ---------- */
async function assinar(texto){
  const dados = new TextEncoder().encode(texto + "|" + SEGREDO_DEMO);
  const hash = await crypto.subtle.digest("SHA-256", dados);
  return Array.from(new Uint8Array(hash))
    .map(b=>b.toString(16).padStart(2,"0")).join("").slice(0,32);
}

/* ---------- geração (celular do cliente) ---------- */
async function gerarToken(conta){
  const payload = {
    id: conta.id,
    nome: conta.nome,
    exp: Math.floor(Date.now()/1000) + VALIDADE_SEGUNDOS,
    nonce: Math.random().toString(36).slice(2,10)
  };
  const corpo = b64u.enc(JSON.stringify(payload));
  const sig = await assinar(corpo);
  return "SSA1." + corpo + "." + sig;
}

/* ---------- validação (tablet na porta) ---------- */
async function validarToken(token){
  if(typeof token !== "string" || token.indexOf("SSA1.") !== 0)
    return {ok:false, motivo:"QR não é da Smart Store"};

  const partes = token.split(".");
  if(partes.length !== 3) return {ok:false, motivo:"QR inválido"};

  const [, corpo, sig] = partes;

  const esperada = await assinar(corpo);
  if(sig !== esperada) return {ok:false, motivo:"Assinatura inválida"};

  let payload;
  try{ payload = JSON.parse(b64u.dec(corpo)); }
  catch(e){ return {ok:false, motivo:"QR corrompido"}; }

  const agora = Math.floor(Date.now()/1000);
  if(payload.exp < agora)
    return {ok:false, motivo:"QR expirado. Gere um novo no aplicativo.", conta:payload};

  const loja = Loja.estado();
  if(!loja.aberta)
    return {ok:false, motivo:"Loja fechada no momento", conta:payload};
  if((loja.bloqueados||[]).indexOf(payload.id) >= 0)
    return {ok:false, motivo:"Conta bloqueada", conta:payload};

  return {ok:true, conta:payload};
}

/* ============================================================
   CONTAS DE CLIENTE
   ------------------------------------------------------------
   Cadastro e login com e-mail e senha, validados de verdade —
   mas contra o que está gravado NESTE aparelho. A senha nunca é
   guardada em texto: fica só o hash SHA-256 com sal.

   Para a conta funcionar em qualquer celular, isto precisa virar
   uma tabela no banco. Troque apenas Contas.criar e Contas.login
   por chamadas à API; o resto da interface não muda.
   ============================================================ */

const DB_CONTAS = "ss_contas_v1";

async function hashSenha(senha, sal){
  const dados = new TextEncoder().encode(sal + "::" + senha);
  const h = await crypto.subtle.digest("SHA-256", dados);
  return Array.from(new Uint8Array(h)).map(b=>b.toString(16).padStart(2,"0")).join("");
}

const emailValido = e => /^[^@\s]+@[^@\s]+\.[a-z]{2,}$/i.test((e||"").trim());

const Contas = {
  todas(){
    try{ return JSON.parse(localStorage.getItem(DB_CONTAS)) || []; }catch(e){ return []; }
  },
  salvar(l){ localStorage.setItem(DB_CONTAS, JSON.stringify(l)); },

  async criar(nome, email, senha){
    email = (email||"").trim().toLowerCase();
    nome = (nome||"").trim();

    if(!nome)                 return {erro:"Informe seu nome."};
    if(!emailValido(email))   return {erro:"E-mail inválido."};
    if((senha||"").length < 6) return {erro:"A senha precisa ter ao menos 6 caracteres."};

    const lista = Contas.todas();
    if(lista.some(c=>c.email === email))
      return {erro:"Já existe uma conta com este e-mail. Faça login."};

    const sal = Math.random().toString(36).slice(2,12);
    const conta = {
      id: "C" + Math.random().toString(36).slice(2,8).toUpperCase(),
      nome: nome,
      email: email,
      sal: sal,
      hash: await hashSenha(senha, sal),
      desde: new Date().toISOString(),
      pagamento: "Cartão final 4821"
    };
    lista.push(conta);
    Contas.salvar(lista);
    return {conta: conta};
  },

  async login(email, senha){
    email = (email||"").trim().toLowerCase();
    if(!emailValido(email)) return {erro:"E-mail inválido."};

    const conta = Contas.todas().find(c=>c.email === email);
    if(!conta) return {erro:"Não encontrei conta com este e-mail."};

    const h = await hashSenha(senha || "", conta.sal);
    if(h !== conta.hash) return {erro:"Senha incorreta."};

    return {conta: conta};
  }
};

const Conta = {
  atual(){
    try{ return JSON.parse(localStorage.getItem(DB_CONTA)); }catch(e){ return null; }
  },
  abrirSessao(conta){
    /* a sessão guarda só o essencial — nunca o hash da senha */
    const s = {id:conta.id, nome:conta.nome, email:conta.email, pagamento:conta.pagamento};
    localStorage.setItem(DB_CONTA, JSON.stringify(s));
    return s;
  },
  sair(){
    localStorage.removeItem(DB_CONTA);
    localStorage.removeItem(DB_CARRINHO);
  }
};

/* ---------- estado da loja (definido pelo admin) ---------- */
const Loja = {
  estado(){
    try{
      const s = JSON.parse(localStorage.getItem(DB_LOJA));
      if(s) return s;
    }catch(e){}
    return {aberta:true, bloqueados:[], nome:"Smart Store — Unidade Centro"};
  },
  salvar(e){
    localStorage.setItem(DB_LOJA, JSON.stringify(Object.assign(Loja.estado(), e)));
  }
};

/* ---------- registro de entradas ---------- */
const Entradas = {
  lista(){
    try{ return JSON.parse(localStorage.getItem(DB_ENTRADAS)) || []; }catch(e){ return []; }
  },
  registrar(reg){
    const l = Entradas.lista();
    l.unshift(Object.assign({quando:new Date().toISOString()}, reg));
    try{ localStorage.setItem(DB_ENTRADAS, JSON.stringify(l.slice(0,200))); }catch(e){}
  },
  limpar(){ localStorage.removeItem(DB_ENTRADAS); }
};

/* ---------- carrinho ---------- */
const Carrinho = {
  itens(){
    try{ return JSON.parse(localStorage.getItem(DB_CARRINHO)) || []; }catch(e){ return []; }
  },
  salvar(l){ localStorage.setItem(DB_CARRINHO, JSON.stringify(l)); },
  adicionar(produto, qtd){
    const l = Carrinho.itens();
    const i = l.findIndex(x=>x.id===produto.id);
    if(i>=0) l[i].qtd += (qtd||1);
    else l.push({id:produto.id, nome:produto.nome, preco:produto.preco, qtd:qtd||1});
    Carrinho.salvar(l);
    return l;
  },
  remover(id){
    Carrinho.salvar(Carrinho.itens().filter(x=>x.id!==id));
  },
  mudarQtd(id, delta){
    const l = Carrinho.itens();
    const i = l.findIndex(x=>x.id===id);
    if(i<0) return l;
    l[i].qtd += delta;
    if(l[i].qtd <= 0) l.splice(i,1);
    Carrinho.salvar(l);
    return l;
  },
  total(){
    return Carrinho.itens().reduce((s,x)=>s + x.preco*x.qtd, 0);
  },
  quantidade(){
    return Carrinho.itens().reduce((s,x)=>s + x.qtd, 0);
  },
  limpar(){ localStorage.removeItem(DB_CARRINHO); }
};

/* ============================================================
   PONTE PARA O SERVIDOR (a implementar)
   ------------------------------------------------------------
   Para o celular ser avisado de que a porta abriu, o tablet e o
   celular precisam falar com o mesmo servidor. Duas funções
   resolvem isso — hoje elas não fazem nada.

   Com Supabase, por exemplo:
     avisarEntrada  -> insert na tabela "entradas"
     ouvirEntradas  -> subscribe no canal realtime dessa tabela
   ============================================================ */

const Porta = {
  /* permite trocar o endereço sem mexer no código, pelo painel admin */
  url(){
    try{ return localStorage.getItem("ss_porta_url") || URL_PORTA; }
    catch(e){ return URL_PORTA; }
  },
  definirUrl(u){
    try{ localStorage.setItem("ss_porta_url", (u||"").trim().replace(/\/+$/,"")); }catch(e){}
  },
  ligada(){ return !!Porta.url(); }
};

/* O TABLET chama isto depois de validar o QR. */
async function avisarEntrada(reg){
  const base = Porta.url();
  if(!base) return false;
  try{
    const r = await fetch(base + "/entrada", {
      method:"POST",
      headers:{"Content-Type":"application/json"},
      body: JSON.stringify({
        id: reg.id || "",
        nome: reg.nome || "",
        ok: !!reg.ok,
        motivo: reg.motivo || ""
      })
    });
    return r.ok;
  }catch(e){ return false; }
}

/* O CELULAR chama isto para ficar de olho no próprio recado.
   Devolve uma função que encerra a escuta. */
function ouvirEntradas(idConta, aoReceber, intervaloMs){
  const base = Porta.url();
  if(!base || !idConta) return function(){};

  let ativo = true;

  const timer = setInterval(async ()=>{
    if(!ativo || document.hidden) return;
    try{
      const r = await fetch(base + "/entrada?id=" + encodeURIComponent(idConta), {cache:"no-store"});
      if(!r.ok) return;
      const j = await r.json();
      if(j && j.evento) aoReceber(j.evento);
    }catch(e){}
  }, intervaloMs || 2000);

  return function(){ ativo = false; clearInterval(timer); };
}

/* ------------------------------------------------------------
   RFID
   O leitor da porta avisa quem entrou; o ESP32 manda as tags
   para esse cliente; o celular busca e joga no carrinho.
   ------------------------------------------------------------ */

/* O LEITOR DA PORTA chama isto ao liberar alguém. */
async function definirClienteAtivo(conta){
  const base = Porta.url();
  if(!base || !conta || !conta.id) return false;
  try{
    const r = await fetch(base + "/ativo", {
      method:"POST",
      headers:{"Content-Type":"application/json"},
      body: JSON.stringify({id: conta.id, nome: conta.nome || ""})
    });
    return r.ok;
  }catch(e){ return false; }
}

/* O CELULAR chama isto para receber as tags lidas. */
function ouvirProdutos(idConta, aoLer, intervaloMs){
  const base = Porta.url();
  if(!base || !idConta) return function(){};

  let ativo = true;
  const timer = setInterval(async ()=>{
    if(!ativo || document.hidden) return;
    try{
      const r = await fetch(base + "/produto?id=" + encodeURIComponent(idConta), {cache:"no-store"});
      if(!r.ok) return;
      const j = await r.json();
      if(j && j.tags && j.tags.length) j.tags.forEach(t=>aoLer(t));
    }catch(e){}
  }, intervaloMs || 2000);

  return function(){ ativo = false; clearInterval(timer); };
}

/* Histórico guardado no Worker, para o painel do administrador. */
async function historicoPorta(){
  const base = Porta.url();
  if(!base) return null;
  try{
    const r = await fetch(base + "/log", {cache:"no-store"});
    if(!r.ok) return null;
    const j = await r.json();
    return j.eventos || [];
  }catch(e){ return null; }
}
