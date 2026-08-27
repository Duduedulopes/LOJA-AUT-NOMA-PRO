# Worker da porta — passo a passo

Este Worker é a "caixa de correio" entre o tablet (leitor) e o celular
(cliente). Você não precisa instalar nada no computador: dá para criar
tudo pelo site da Cloudflare.

Tempo estimado: 10 minutos.

---

## O que vamos criar

Duas coisas, ambas gratuitas:

| O quê | Para quê |
|---|---|
| **KV** chamado `porta-recados` | a gaveta onde os recados ficam guardados |
| **Worker** chamado `smart-store-porta` | o carteiro que guarda e entrega os recados |

---

## Parte 1 — criar a gaveta (KV)

1. Entre em `dash.cloudflare.com`.
2. No menu lateral, procure **Storage & Databases** e clique em **KV**.
   (Em algumas contas aparece dentro de **Workers & Pages** → aba **KV**.)
3. Clique em **Create a namespace** (ou "Create").
4. No nome, escreva:

   ```
   porta-recados
   ```

5. Confirme. Pronto, a gaveta existe.

---

## Parte 2 — criar o carteiro (Worker)

1. Menu lateral → **Workers & Pages** → **Create** → **Create Worker**.
2. No nome, escreva:

   ```
   smart-store-porta
   ```

3. Clique em **Deploy**. Ele vai subir um "Hello World" — normal, vamos
   trocar o código no próximo passo.
4. Clique em **Edit code** (ou "Editar código").
5. Apague TUDO que estiver no editor.
6. Abra o arquivo `porta-worker.js` desta pasta, copie o conteúdo inteiro
   e cole no editor.
7. Clique em **Deploy** (canto superior direito).

---

## Parte 3 — ligar a gaveta no carteiro

Sem isso o Worker responde erro, porque não sabe onde guardar os recados.

**Antes de mexer, confira se já não está pronto.** Se ao criar o Worker você
escolheu um modelo que mencionava KV, a Cloudflare pode ter vinculado um
sozinha. Vá em **Settings** → **Bindings** e veja se já existe algum
KV namespace listado.

- **Já existe um** com nome de variável `KV` ou `PORTA`? Não precisa fazer
  nada. O código aceita os dois nomes.
- **Não existe nenhum?** Siga abaixo.

1. Em **Settings** → **Bindings**
   (em algumas contas: **Settings** → **Variables** → **KV Namespace Bindings**).
2. Clique em **Add binding** → escolha **KV namespace**.
3. Preencha:

   | Campo | Valor |
   |---|---|
   | Variable name | `PORTA` |
   | KV namespace | `porta-recados` |

4. Salve e faça **Deploy** de novo.

---

## Parte 4 — testar

Copie o endereço do seu Worker. Ele tem esta cara:

```
https://smart-store-porta.SEU-USUARIO.workers.dev
```

Abra esse endereço no navegador. Se aparecer algo assim, está funcionando:

```json
{"servico":"smart-store-porta","ok":true,"hora":"2026-08-04T..."}
```

Se aparecer um erro dizendo que nenhum KV está vinculado, volte à Parte 3.

---

## Parte 5 — avisar o site qual é o endereço

1. Abra o arquivo `shared/acesso.js` no VS Code.
2. Logo no começo tem esta linha:

   ```js
   const URL_PORTA = "";
   ```

3. Cole o endereço do seu Worker entre as aspas, sem barra no final:

   ```js
   const URL_PORTA = "https://smart-store-porta.SEU-USUARIO.workers.dev";
   ```

4. Salve, e suba os arquivos de novo na Cloudflare (upload da pasta).

---

## Parte 6 — ver funcionando

1. No notebook, abra `/porta/` e ligue a câmera.
2. No celular, abra `/cliente/`, entre com seu nome e vá na aba **Acesso**.
3. Aponte o QR do celular para a câmera do notebook.

O notebook fica verde com seu nome. Em até 2 segundos, o celular mostra
a faixa **ACESSO LIBERADO** por cima da tela.

---

## Custo

O plano gratuito do Cloudflare Workers dá 100 mil requisições por dia.
Cada entrada de cliente gasta cerca de 3 requisições. Dá para umas
30 mil entradas por dia sem pagar nada.

---

## Se algo der errado

**O celular não recebe o aviso.**
Abra o console do navegador (F12) e veja se aparece erro de CORS ou de
rede. Confirme que `URL_PORTA` está preenchido nos arquivos que você
subiu — e não só no seu computador.

**"Nenhum KV vinculado".**
Vá em Settings → Bindings e adicione um KV namespace. O nome da variável
pode ser `PORTA` ou `KV` — o código aceita os dois.

**O tablet lê mas nada acontece.**
Isso é independente do Worker. Verifique se o QR não expirou (ele dura
60 segundos) e se a loja não está fechada no painel do admin.
