# Multiempresa — o AutonomousStore como assinatura

Plano de arquitetura combinado com o Eduardo e **Fases 1 e 2 já implementadas** (branch `feature/multiempresa`, ainda sem commit).
Escrito em 21/09/2026.

O objetivo: uma instalação hospedada atende várias empresas. Cada empresa enxerga só os dados dela.
O Criador (Eduardo) enxerga e controla tudo. Ainda não há empresa cliente: o alvo é um **MVP demonstrável e
publicado, sem custo de infraestrutura**, com o leitor RC522 como hardware.

---

## 1. Quem é quem

```
Criador (Eduardo)                 → vê e controla tudo
 ├─ Técnicos (suporte)            → equipe da plataforma, um nível abaixo do Criador
 └─ Empresa / Admin (assinante)   → a fronteira de isolamento
     └─ Lojas → Compradores
```

| Papel | Escopo | Onde mora |
|---|---|---|
| **Criador** | Todas as empresas, pessoas e ações do suporte. Cadastra empresas e técnicos | `UsuariosCriador` |
| **Técnico** (`Suporte`) | Chamados de **todas** as empresas (MVP), inclusive os que os Admins abrem. Não pertence a nenhuma. Cadastro é só do Criador | `UsuariosSuporte` |
| **Admin** | Só a própria empresa: compradores, pedidos, produtos. **Lojas: só consulta** — quem abre, renomeia, desativa e reativa é o Criador ou o suporte (§7, revisão de 22/09/2026). Nos chamados vê o que os detectores acharam e os pedidos que ele mesmo escreveu; pode relatar um erro e **chamar o suporte**, que o atende | `UsuariosAdmin` + `TenantId` |
| **Comprador** | Pertence a **uma empresa**. Compra em qualquer loja dela. Outra empresa exige outro cadastro | `Clientes` + `TenantId` |

Nomes no código: a **empresa assinante** é `Tenant` (tabela `Tenants`); a **loja** é `Store` (tabela `Lojas`).
Não confundir com `Company` (tabela `Empresas`), que é a **marca dona de um produto** no catálogo.

---

## 2. Como o isolamento funciona

1. **O token diz a empresa.** O JWT do Admin e do Comprador carrega a claim `tenant_id`. Criador e Técnico não têm
   empresa: enxergam todas.
2. **Um middleware** (`Tenancy/TenantMiddleware`) lê isso no começo de cada requisição e preenche o `TenantContext`.
   No SignalR, o `TenantHubFilter` faz o mesmo em cada chamada de hub (que não passa pelo pipeline HTTP).
3. **O banco filtra sozinho.** O `AutonomousDbContext` aplica um filtro global em toda entidade de empresa
   (`ConfigurarTenant<T>`). Os repositórios não precisam lembrar de nada.
4. **Falha fechada.** Sem empresa definida, nada é visível. Gravar sem empresa dá erro, e nunca um registro invisível.
5. **Carimbo automático.** Ao gravar, a empresa da requisição vira a dona do registro. Uma empresa comum não consegue
   gravar na outra, e a empresa de um registro nunca muda.
6. **O cabeçalho nunca vence o token.** O app do comprador diz a empresa antes do login pelo `X-Empresa` (o link/QR da
   loja). Depois do login vale só o token: um Admin não vira "Admin de outra empresa" mandando um cabeçalho.
7. **Únicos por empresa:** código de barras, tag RFID, e-mail, CPF, `GoogleId` e chave de ocorrência. O e-mail do
   **Admin** continua único na plataforma inteira, porque é ele que revela a empresa no login.

Sem filtro, de propósito: `AdminUser` (é procurado pelo e-mail no login, antes de haver empresa). Um teste de
segurança falha o build se alguém criar uma entidade de empresa e esquecer o filtro.

**Suspender** uma empresa corta o acesso em até 30 segundos (cache do resolvedor; o Criador o invalida na hora).

---

## 3. Privacidade e auditoria

- O técnico **não** vê dados de pessoas por padrão: e-mail, CPF e telefone mascarados **no servidor**. Revelar exige
  um **motivo**, que fica registrado. *(Implementado na Fase 2, em `/api/suporte/*`.)*
- A empresa aparece para o técnico como **código + nome fantasia**. Nunca CNPJ, contato, e-mail ou telefone.
- **Nunca** exibidos a ninguém: senha/hash, tokens de reset, `GoogleId`, `ProviderToken`, chaves.
- **Auditoria** (`Auditorias`): só acrescenta, nunca altera. Já grava: criação de Criador, login do Criador e do
  técnico, cadastro de técnico, criação/suspensão/reativação de empresa e mudança de limite de lojas.
  O Criador lê em `GET /api/platform/auditoria`.
- Achado que motivou a máscara: `Ocorrencia.AbertoPor`, `quemEmail` e `MensagemDeSuporte.AutorEmail` guardam
  e-mail e nome de quem pediu ajuda em texto puro.

---

## 4. Assinatura

- A empresa tem `LimiteDeLojas`; o preço acompanha. O limite conta só as lojas **ativas** e é conferido em todo caminho
  que aumenta esse número: **criar** loja, **reativar** uma desativada e o Criador **baixar** o limite (recusado com 409 se a
  empresa já tem mais lojas ativas do que o novo limite). Desativar libera a vaga e não apaga nada. O nome da loja é único por
  empresa, sem diferenciar maiúscula.
  Conferir e gravar não são atômicos: dois cliques no mesmo instante poderiam passar do limite em uma loja. É limite de plano,
  não de segurança, e isso foi aceito de propósito.
- Cobrança **manual** no começo (Pix ou boleto mensal). Cada empresa recebe o dinheiro das próprias vendas: a plataforma
  não toca no dinheiro. O pagamento no app segue **simulado e sinalizado**; o gateway real entra depois, configurado
  **por empresa**.
- O suporte não depende do plano por enquanto (regras de negócio ficam para depois).

---

## 5. Fases

| Fase | Entrega | Situação |
|---|---|---|
| 0 | Decisões | ✅ |
| **1 — Fundação** | `Tenant`, `Store`, limite de lojas, papéis, claim no JWT, filtro global, auditoria, migração dos dados atuais, testes de isolamento | ✅ **feita e testada** (ver §6) |
| **2 — Por empresa** | Admin e Client por empresa (Client manda `X-Empresa`; telas de loja com checagem do limite). Suporte com a visão do técnico: máscara, "revelar com motivo", código + nome fantasia, cadastro de **compradores**. `Admin` deixa de ser "da casa" nos chamados | ✅ **feita e testada** (ver §7). Ficam para depois: relatores de erro do AdminApp/SuporteApp sem empresa e o atalho `Tenancy:EmpresaPadrao` |
| 3 | Fluxo de compra fechado: entrada autônoma, checkout e confirmação (pagamento simulado e sinalizado) | a fazer |
| **4 — Dashboards** | AdminApp (Fase 2) e o **4º app, `CriadorApp`** (painel, empresas, técnicos, suporte, auditoria) | 🔶 **`CriadorApp` feito e testado** (ver §8). Falta: intenções do Criador no Rede-Neural, e relatórios exportáveis de chamados |
| 5 | Publicar a demo: configuração por ambiente, segredos fora do git, CORS com domínios reais, backup, hospedagem quase gratuita | a fazer |
| Depois | Pagamento real por empresa, assinatura automática, monitoramento de rede/dispositivos, domínio próprio | — |

---

## 6. Fase 1 — o que foi feito e como foi verificado

**Verificação**
- Solução inteira compila sem erros; **148 testes passam** (Domain 123, Infrastructure 24, Application 1).
- 24 testes de isolamento rodam contra um banco relacional de verdade (SQLite em memória). Foram validados por
  **mutação**: com o filtro desligado, 9 quebram; com o erro sutil "nulo == nulo", 1 quebra.
- A migração foi aplicada numa **cópia restaurada do backup** do banco real: nenhuma linha perdida (3 produtos, 61 sessões,
  11 ocorrências…), tudo na empresa padrão, 8 chaves estrangeiras, índices por empresa. Reverter e reaplicar também funciona.
- 44 verificações HTTP com a API no ar (empresas, isolamento, suspensão, portas fechadas, auditoria) e 5 no chat
  SignalR (o Admin entra no chamado da própria empresa; o comprador de outra empresa, não).

**Mudanças de comportamento**
- `POST /api/admin-auth/register` — antes **aberto a qualquer um**, agora responde 403. Admin nasce junto com a empresa
  (`POST /api/platform/empresas`).
- `POST /api/suporte-auth/register` — só o Criador. A resposta não traz o token do técnico novo.
- O primeiro Criador só nasce com o `Criador:CodigoDeInstalacao` do servidor (vazio = cadastro desligado).
- Cadastro/login de comprador exigem uma empresa (`[ExigeEmpresa]`).
- Tokens de Admin e Comprador passam a levar `tenant_id`. Tokens antigos (sem a claim) valem só para a empresa padrão e
  expiram em 7 dias.
- Token do Criador vale 8 horas, e não 7 dias.
- Transição: sem o cabeçalho `X-Empresa`, o anônimo cai na empresa `Tenancy:EmpresaPadrao` (`piloto`), para os apps de
  hoje continuarem funcionando. **Remover quando o ClientApp e os dispositivos identificarem a empresa (Fase 2).**

**Coisas que ficaram de fora, de propósito**
- Repositório e telas de `Store`. A tabela e a empresa/loja padrão existem; o uso ficou para a Fase 2 (feito, §7).
- O `SuporteApp` ainda mostrava "Criar conta e entrar", que agora é recusado. Saiu da tela na Fase 2 (§7).
- Endpoints de hardware (`Sessions/current-open`, `items/by-rfid`, `Vision`) continuam anônimos, agindo na empresa
  padrão. Cada dispositivo precisará de credencial própria por empresa.

---

## 7. Fase 2 — o que já foi feito e como foi verificado

**Feito**
- **Técnico** (`/api/suporte/*`): lista as empresas como "0002 · Rede Sabor"; lista e busca compradores **mascarados**;
  `revelar` exige motivo (mín. 10 caracteres) e a auditoria é gravada **antes** de o dado sair; cadastra comprador em nome de
  uma empresa (a senha é aleatória e ninguém a conhece — a pessoa define a dela por "esqueci minha senha"; empresa suspensa
  recusa).
- **Chamados e ocorrências**: para o técnico, o e-mail de quem pediu ajuda vem mascarado (`AbertoPor` e `quemEmail`) e a
  empresa vem como código + nome. O envio em tempo real do chat vai **sempre mascarado**: ele é montado uma vez para o grupo
  inteiro, e montá-lo para quem respondeu entregaria ao técnico o e-mail que o Admin tem direito de ver.
- **Chamado em nome de uma empresa** (`empresaId`), aberto pelo técnico ou pelo Criador. Para Admin e comprador o campo é ignorado.
- **Código da empresa** (`Tenant.Codigo`, migração `AdicionarCodigoDaEmpresa`, aplicada no banco local).
- **Furo que já existia e foi fechado:** o `CustomersController` só exigia estar logado — qualquer comprador lia e alterava o
  cadastro de qualquer outro pelo id (inclusive o e-mail, que com "esqueci minha senha" dava a conta). Agora só o próprio
  comprador altera; o Admin da empresa e o Criador leem; o técnico usa `/api/suporte/compradores`.
- **Bug meu da Fase 1, corrigido:** o Criador não conseguia abrir chamado ("App desconhecido: CriadorApp").
- **ClientApp**: identifica a empresa pelo link ou QR da loja (`?empresa=redesabor`), lembra no navegador e manda `X-Empresa` em
  toda chamada; o relator de erros do app também manda.
- **SuporteApp**: saiu o "Criar conta" (o servidor o recusa); o gerente dele passou a ter perfil de técnico (§9).
- **Regra dos chamados — o Admin NÃO é "da casa".** Só o técnico e o Criador são. O Admin vê o que os detectores acharam e os
  pedidos que ele mesmo escreveu; pode relatar um erro e **chamar o suporte** para tentar resolvê-lo. Ao chamar, ele vira o autor
  do chamado e a descrição vira a primeira mensagem, e o técnico o atende como atende qualquer outro. Pedidos de comprador e
  chamados preventivos do técnico são do suporte: para o Admin, respondem 404. A tela de Ocorrências do AdminApp avisa que a
  conversa com o suporte fica em **Suporte**, no menu.
- **Lojas — revisado em 22/09/2026: o Admin deixou de mexer.** No desenho original (Fase 2) o Admin cadastrava e geria as
  próprias lojas; o Eduardo decidiu que abrir, renomear, desativar e reativar loja é negócio da assinatura, não
  autoatendimento — quem faz é o **Criador ou o suporte**, em nome da empresa.
  - `GET /api/lojas` (só Admin, só leitura): a mesma consulta de sempre — lista, `ativas`, `limite`.
  - `POST/PUT/POST .../{id}/desativar/POST .../{id}/ativar` **saíram** do `LojasController` (hoje só tem o `GET`) e foram para
    um controller novo, `LojasDaPlataformaController` (`api/platform/empresas/{empresaId}/lojas/...`,
    `[Authorize(Roles = Papeis.DaPlataforma)]` — Criador **ou** Suporte). Precisou ser um controller à parte: em ASP.NET Core
    um `[Authorize]` de método SOMA ao da classe, não afrouxa — não daria para abrir só essas rotas ao Suporte dentro do
    `PlatformController`, que é só-Criador inteiro.
  - Mesmas regras de sempre (limite, nome único por empresa, tamanho das colunas, loja de outra empresa é 404) mais uma nova:
    **empresa suspensa não ganha capacidade** — criar ou reativar loja numa empresa suspensa é 403 (desativar continua
    liberado, porque reduz, não aumenta). Auditoria continua gravando `loja.criada/desativada/reativada`, agora com o ator
    correto (Criador ou Suporte, nunca Admin).
- **Tela Lojas no AdminApp** (`Pages/Lojas.razor`): virou só consulta — uma caixa de pesquisa (nome ou segmento) sobre a lista
  de `GET /api/lojas`. Saíram o formulário de cadastro e o selo/aviso de limite (foram para **Plano**, abaixo).
- **Aba Plano, nova, no AdminApp** (`Pages/Plano.razor`, no menu ao lado de Ocorrências e Suporte): "seu plano atual" (o mesmo
  "X de Y lojas ativas" que saiu de Lojas, com o CTA "Falar com o suporte") e uma comparação de **planos fictícios**
  (Piloto/Crescimento/Rede) — sem preço real de propósito, o Eduardo ainda não decidiu valores; a tela avisa isso por escrito.
- **Gestão de lojas no CriadorApp** (`Pages/EmpresaLojas.razor`, `/empresas/{id}/lojas`, link "Lojas" na linha de cada empresa
  em `Pages/Empresas.razor`): a MESMA tela que o AdminApp tinha (cadastro + lista com renomear/desativar/reativar), agora
  parametrizada pela empresa da URL em vez da empresa do token — porque quem opera não tem uma empresa só seguida no token.
- **Achado e corrigido, sem relação com lojas:** o sino do AdminApp (`GET /api/ocorrencias/nao-vistas`) nunca aplicava a mesma
  regra de "isso não é do Admin" que a lista e o resumo já aplicavam — um vazamento por contagem em potencial (hoje sem efeito
  prático, porque todo pedido nasce direto em `NoSuporte`, nunca fica em `Nova` com dono; ficou como trava para o futuro). A
  causa real do sino "1" com a lista "0" que o Eduardo viu foi outra, mais simples: o sino não tem janela de tempo (conta
  desde sempre) e a tela de Ocorrências abre filtrada nos últimos 7 dias — uma ocorrência antiga acende o sino e some da lista
  até alguém limpar o filtro. Não é bug de isolamento.
- **`Email:Habilitado`**: liga/desliga o envio de e-mail. Em `Development` o padrão é **desligado**; nos demais ambientes, ligado. O
  valor não pode ficar no `appsettings.json` versionado, porque valeria também em `Development`. Com o envio desligado, o link de
  redefinição de senha vai para o log, só em `Development`.
- **O gerente chama todos pelo primeiro nome**, o Criador inclusive. O "Chefe" saiu do tratamento (§9).

**Como foi verificado**
- **339 testes** passam: Domain 183, Infrastructure 67, Gerente 48, WebApi 42, Application 1. A solução
  compila sem erros nem avisos.
- **Rodada completa em cópia do banco real**, com a API no ar: 4 roteiros HTTP (Fase 1, Fase 2, 2B — o suporte atende e o Admin
  pede ajuda — e 2C — lojas e o limite) e 3 do chat (SignalR). Tudo passa. A rodada também confere que **nenhum e-mail saiu de
  verdade** (bloqueados pela trava: sim; tentativas de envio real: 0) e que o banco real ficou intacto.
- **Mutação:** com a máscara do envio em tempo real desligada, exatamente as 2 verificações certas falham; com `faturamento`
  liberado ao técnico por engano, 6 testes falham; com o técnico caindo de novo no texto de ajuda do Chefe, 2 falham.
- **Mutação das lojas** (9 mutações, todas mortas): com o limite ignorado ao criar, ao reativar ou ao baixar; com o contador contando lojas
  desativadas ou as de outra empresa; com a rota aberta a qualquer logado; com o nome repetido comparado sem cuidado com
  maiúscula ou entre empresas; e com "renomear" conflitando com a própria loja, os testes falham exatamente nos pontos certos.
  (Uma delas, a última, só era pega por um Traceback do roteiro: o roteiro foi endurecido para reportar a verificação que caiu.)
- **No navegador:** a tela Lojas do AdminApp (busca filtrando por nome/segmento, sem cadastro nem selo de limite) e a aba
  Plano (o "2 de 2" que saiu de lá, e o plano fictício "parecido" mudando de acordo com o limite real da empresa). No
  CriadorApp, `Empresas → Lojas`: cliquei em **Desativar** de verdade na tela — a mensagem de sucesso apareceu, a contagem
  caiu de "2 de 2" para "1 de 2", e o botão virou "Reativar" (a tela recarrega do servidor depois de cada ação, não confia
  só no clique).
- **No navegador, pela tela real do ClientApp:** cadastro com `?empresa=redesabor` grava na Rede Sabor; sem o parâmetro usa a
  empresa lembrada; `?empresa=piloto` manda sobre a lembrada; valor inválido na barra de endereço é ignorado; o relator de
  erros envia `X-Empresa`.
- **Migração do código:** testada em cópia com três empresas (numeradas 1, 2, 3), revertida, reaplicada, e só então aplicada no real.

**O que fica da Fase 2 para depois**
- Os relatores de erro do **AdminApp** e do **SuporteApp** não mandam empresa (e os três têm a URL da API cravada em
  `https://localhost:7167/`, que não é a porta que o ClientApp usa).
- Tirar o atalho de transição `Tenancy:EmpresaPadrao` quando os apps e os dispositivos identificarem a empresa.

**Cuidado nos testes (aprendido do jeito difícil, em 21/09/2026)**
A API em `Development` lê o `appsettings.Development.json`, que tem a **senha SMTP real**. Cada cadastro de comprador envia um
"Bem-vindo(a)" pela conta do Eduardo, para o endereço digitado. Ao testar, exporte
`Email__SenderEmail=COLOQUE_DESLIGADO_NOS_TESTES` (a convenção do próprio `SmtpEmailService`: avisa no log e não conecta) e use
endereços em domínio `.invalid`. Nunca `exemplo.com`, `x.com` ou `teste.com`: existem, e `exemplo.com` tem servidor de e-mail.
Desde a Fase 2 há também o `Email:Habilitado` (padrão desligado em `Development`), como uma terceira trava: os testes continuam
exportando as três.

---

## 8. Fase 4 (parcial) — o `CriadorApp`, o dashboard do Criador

**Feito**
- **`AutonomousStore.CriadorApp`**, o 4º projeto Blazor WASM da solução, na porta 5292 (padrão dos outros três: `AutonomousStore.WebApi`
  em `https://localhost:7167`). Login próprio (`/api/criador-auth/login`) — sem tela de cadastro: o primeiro Criador nasce pelo
  código de instalação (Swagger), e o próprio Criador cadastra o resto depois de logado.
- **Painel** (`/`, `GET /api/platform/painel`, uma chamada só): quantas empresas (ativas/suspensas), lojas ativas contra o limite
  (soma só das empresas **ativas** — uma suspensa não está pagando, e contar o limite dela infla a capacidade real), técnicos
  ativos, compradores, e o resumo do suporte (na fila, quantos graves, há quanto tempo espera o mais antigo, resolvidas nos
  últimos 30 dias, tempo médio de resolução, e quem resolveu). Uma linha por empresa, com as mesmas contas.
  - **O Mapa da Rede:** o centro do painel não é uma tabela — é a própria hierarquia do produto desenhada (SVG, sem biblioteca de
    gráfico): o Criador no centro, cada empresa numa órbita curva, o raio do círculo cresce com o quanto ela usa a plataforma
    (lojas + compradores), um pulso rosa avisa qual empresa tem chamado esperando o suporte, e o hub central pulsa quando há
    algum grave na fila. Clicar num nó destaca a linha da empresa na tabela abaixo — é outra leitura dos MESMOS dados do painel,
    não um segundo sistema.
- **Empresas** (`/empresas`): cadastra empresa + primeiro Admin juntos (`POST /api/platform/empresas`), suspende, reativa, muda o
  limite de lojas — as mesmas rotas que a Fase 1/2 já tinham; a tela só faltava. Cada linha tem um link **Lojas**.
- **Gestão de lojas** (`/empresas/{id}/lojas`, revisão de 22/09/2026): abrir, renomear, desativar e reativar uma loja de UMA
  empresa — a mesma tela que o AdminApp tinha (§7), parametrizada pela empresa da URL. Usa rotas novas,
  `LojasDaPlataformaController` (`api/platform/empresas/{empresaId}/lojas/...`), abertas ao Criador **e** ao Suporte.
- **Técnicos** (`/tecnicos`, `GET /api/platform/tecnicos`, novo): lista a equipe (inclusive quem foi desativado) e cadastra
  (`POST /api/suporte-auth/register`, que já era só do Criador desde a Fase 1).
- **Fila** (`/fila`) e **Histórico** (`/historico`): as MESMAS telas do SuporteApp (`Home.razor`/`Ocorrencias.razor`, copiadas sem
  mudar a lógica) — funcionam porque `Papeis.DaPlataforma` (usado em `/api/ocorrencias` e nos chamados) sempre incluiu Criador e
  Suporte igualmente. O Criador vê e responde a mesma fila que o técnico.
- **Auditoria** (`/auditoria`, novo na tela — a rota `GET /api/platform/auditoria` já existia): filtro por empresa, ação e período.
- **O gerente** atende o Criador pelo perfil `PerfilDeQuemFala.Criador(nome)` — chama pelo primeiro nome, com as sugestões da
  plataforma ("teve algum furo de sistema?", "status da api"), igual ao SuporteApp (ver §9).

**Rotas novas na API** (`PlatformController`, só Criador): `GET /api/platform/painel` e `GET /api/platform/tecnicos`. As contas do
painel moram em `PainelDaPlataforma` (`AutonomousStore.WebApi/Services`), separadas do controller de propósito: é código puro
(recebe o que os repositórios leram, devolve o que a tela mostra), testável sem banco nem servidor.
As rotas de loja (§7) moram num controller À PARTE (`LojasDaPlataformaController`), Criador **ou** Suporte — diferente do
`PlatformController`, que é só-Criador.

**Como foi verificado**
- **339 testes** passam: Domain 183, Infrastructure 67, Gerente 48, WebApi 42, Application 1. Solução compila sem erros nem avisos.
- **Roteiro HTTP novo** (Fase 4A): o painel comparado número a número com as rotas que já existiam (`/empresas`, `/{id}/lojas`,
  `/suporte/compradores`, `/ocorrencias?estado=NoSuporte`) — os dois lados têm de bater; um chamado aberto e depois resolvido faz a
  fila e o "quem resolveu" andarem no painel; só o Criador entra nas rotas novas (401 anônimo, 403 Admin/técnico/comprador).
  Entra na rodada completa (agora com 8 roteiros).
- **`PainelDaPlataformaTests`** (14 testes): as contas isoladas do banco — só empresas ativas somam lojas/limite, cada empresa
  conta só o que é dela, chamado da plataforma (sem empresa) soma no total mas em nenhuma linha, média de resolução por técnico,
  nome vazio vira "(não informado)" sem sumir da lista, e um relógio desalinhado (resolvida "antes" de nascer) não vira tempo
  negativo na tela.
- **No navegador**, com API e CriadorApp no ar (cópia do banco real): login, as 6 telas, o Mapa da Rede com dados reais (raio e
  pulso corretos), clicar num nó destaca a linha certa da tabela, e o gerente cumprimentando pelo nome.

**Achado nesta rodada, sem relação com o código:** o banco real já tinha, antes desta sessão, um técnico de suporte cadastrado
com o e-mail do próprio Eduardo e dois chamados de teste do ClientApp — quase certamente de um teste manual anterior dele mesmo.
Apareceu na cópia usada para testar o CriadorApp; nada foi escrito no banco real.

**O Criador de verdade existe desde 22/09/2026.** O Eduardo criou a própria conta (Swagger, com o `Criador:CodigoDeInstalacao`
que faltava no `appsettings.Development.json` local — foi adicionado). Duas consequências para quem mexer nisto depois:
- Os roteiros de teste (`e2e.py`...) testam "o PRIMEIRO Criador, com o código de instalação"; com um Criador já existindo, essa
  chamada cai no outro ramo do controller (exige token de Criador) e falha com 401. O `rodada.sh` agora apaga
  `UsuariosCriador` da CÓPIA logo após restaurar (nunca do banco real), para a cópia voltar a ficar "sem Criador", do jeito
  que o roteiro espera.
- As checagens de "banco real intacto" da rodada não podiam mais exigir `Criadores=0, Auditorias=0` (crescem com o uso real
  dele); passaram a exigir só `Tenants=1` com igualdade, e mostrar os outros dois números só de forma informativa.

**Fica para depois:** as intenções do Criador no Rede-Neural (perguntar ao gerente "quantas empresas...", "qual está suspensa" —
ver §9); relatórios exportáveis de chamados; desativar técnico pela tela (a entidade já tem `Deactivate()`, falta a rota).

---

## 9. O gerente virtual (Rede-Neural) em cada app

O gerente é uma rede neural escrita do zero: o Python (`Rede-Neural/`) é onde ela nasce e é medida, o C# (`AutonomousStore.Gerente`)
é onde ela vive, e o `intencao.json` é a língua que os dois falam. O **mesmo cérebro** responde de forma diferente a cada app,
por uma **lista de permissão** (`PerfilDeQuemFala`): intenção nova nasce fechada.

| Perfil | App | Trata por | Escreve | Enxerga |
|---|---|---|---|---|
| Chefe | AdminApp | primeiro nome | sim | as 42 intenções |
| Cliente | ClientApp | primeiro nome | não | 12 (preço, prateleira, carrinho dele…) |
| **Técnico** *(novo)* | SuporteApp | primeiro nome | não | 8: a conversa + `status_api`, `logs_sistema`, `furo_sistema` |
| **Criador** *(novo)* | CriadorApp (§8, feito) | primeiro nome | não | as mesmas 8, por enquanto |

"Chefe" é só o nome do **perfil** de permissão do AdminApp no código: o gerente chama todos pelo nome, indiferente de quem for.

- O técnico **deixou de herdar o Chefe**: antes o SuporteApp usava o perfil do dono da loja. Como o técnico atende várias
  empresas, "quanto faturamos hoje?" somaria o dinheiro de empresas diferentes — e ele não tem por que ler o de ninguém.
- Sugestões, ajuda, "fora de escopo" e recusa agora dependem do **tipo** do perfil. Antes dependiam de "pode escrever?", que
  tratava o técnico (que não escreve) como comprador — e mandaria o suporte "abrir um chamado no Suporte".
- Uma recusa **nem busca o dado**: os testes provam, nos dois caminhos (botão e frase digitada), que nenhuma API é consultada.
- Os testes leem as 42 intenções do próprio `intencao.json`. Se o Rede-Neural treinar uma intenção nova, ela nasce recusada
  para comprador, técnico e Criador, e alguém precisa decidir para quem abrir.

**Para o Criador responder sobre a plataforma** (quantas empresas, quais suspensas, o que o suporte fez…) são precisas
**intenções novas**. Isso é trabalho do Rede-Neural, seguindo as regras do caderno: corpus, treino, calibração do limiar,
guardas, e medir antes e depois. Depois vêm os tratadores no C#, lendo `/api/platform/*` — o app que os vai mostrar já existe (§8).

**Medido em 21/09/2026 (lacunas de corpus, para o Rede-Neural):** "onde ficam os logs?" cai em `configurar_camera` (45%);
"mostra os logs do sistema" acerta `logs_sistema` a 82%, abaixo do limiar de 90%; "houve algum furo hoje?" cai em
`status_sistema` (76%). "teve algum furo de sistema?" acerta a 100% e "status da api" a 89%.

**Decisão em aberto — o aprendizado:** as correções que as pessoas fazem no chat vão para **um único** `correcoes.jsonl` do
monitor, e o modelo é o mesmo arquivo para todos. No SaaS, as frases de uma empresa entrariam no corpus que treina o gerente de
todas. Opções: corpus curado só pelo Criador (recomendado), ou aprendizado por empresa com consentimento.

---

## 10. Para operar

**Migração `AdicionarMultiEmpresa`: aplicada no banco local em 21/09/2026**, com nenhuma linha perdida. Backups feitos antes
(pasta de backup do SQL Server): `AutonomousStoreDb_antes_multiempresa_20260921_160520.bak` e
`AutonomousStoreDb_imediatamente_antes_da_migracao_20260921_163627.bak`.

Para aplicar em outro ambiente (rodando dentro de `AutonomousStore/`):

```
dotnet ef database update --project AutonomousStore.Infrastructure --startup-project AutonomousStore.Infrastructure
```

> **Armadilha:** os comandos `dotnet ef` leem a variável de ambiente `AUTONOMOUSSTORE_CONNECTION`. Nesta máquina ela estava
> com o nome antigo do computador (`DESKTOP-PO1CEEU\SQLEXPRESS`) e dava "servidor não encontrado". A API não usa essa
> variável (usa `appsettings.Development.json`), então o problema só aparece nos comandos de migração.

**Criar o primeiro Criador** (o código de instalação você inventa; ele só precisa estar na configuração do servidor, por
exemplo em `appsettings.Development.json`, e ser igual ao que você digita no cadastro):

```
"Criador": { "CodigoDeInstalacao": "<seu-codigo>" }   →   POST /api/criador-auth/register
```

**Cadastrar uma empresa:** `POST /api/platform/empresas` (Swagger, logado como Criador).

---

## 11. Pendências

- Arrumação de pastas do repositório, licença (hoje MIT) e hospedagem definitiva.
- **Trocar a `Jwt:Key` do `appsettings.json` versionado** (54 caracteres, não parece exemplo). Com várias empresas, quem
  tem essa chave forja o token de qualquer uma.
- Termos de uso e LGPD (o sistema guarda CPF de compradores).
- Como o aprendizado do gerente é guardado **por empresa** (§9): as perguntas de uma empresa não podem treinar o gerente de outra.
- **CSS do AdminApp:** em `wwwroot/css/app.css` (por volta da linha 468) um comentário nunca é fechado e engole as regras de
  `.btn-primary`: todo botão principal do painel aparece sem estilo. Já estava no repositório antes da Fase 1. Consertar muda o
  visual de todos os botões, então espera a sua decisão.
- **Mensagens de erro com o nome do parâmetro:** o `Tenant` (e outras entidades) lança `ArgumentException` com o nome do parâmetro, e
  onde o controller devolve `ex.Message` o texto sai com " (Parameter 'nome')". A `Store` já lança sem ele. Conferir os demais pontos.
- Endpoints de hardware continuam anônimos, agindo na empresa padrão (credencial por dispositivo, Fase 5).
