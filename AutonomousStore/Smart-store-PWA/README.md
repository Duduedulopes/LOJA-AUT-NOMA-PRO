# Smart Store — Catálogo

Assistente virtual da loja, em dois apps PWA no mesmo domínio:

- `/` — app do cliente: consulta preço, estoque, promoções e localização
- `/admin` — painel do administrador: produtos, radar de perguntas e integrações

Ambos são instaláveis no celular e funcionam offline depois da primeira visita.
Sem framework, sem build, sem dependência: HTML, CSS e JS puros.

## Relação com o SmartGO

Os dois assistentes cobrem domínios diferentes e se complementam:

| | SmartGO | Smart Store (este) |
|---|---|---|
| Assunto | plataforma, cadastro, pagamento | produtos da loja |
| Fonte | base de conhecimento do SmartGO | catálogo cadastrado no admin |

Este app resolve as perguntas de catálogo localmente e **encaminha o resto para
o SmartGO**. Configure o endpoint em `/admin` → Ajustes.

Requisição enviada:

```json
{
  "mensagem": "texto do cliente",
  "historico": [ { "role": "user", "content": "..." } ],
  "origem": "smart-store-catalogo"
}
```

A resposta é aceita em vários formatos — `resposta`, `mensagem`, `reply`,
`message`, `texto`, `answer`, `output`, padrão OpenAI (`choices[0].message.content`)
ou texto puro. Se a sua API usar outro nome de campo, ajuste o array
`CAMPOS_RESPOSTA` em `shared/dados.js`.

Se o SmartGO estiver em outro domínio, ele precisa liberar CORS para a origem
deste app.

## Estrutura

```
index.html                 app do cliente
manifest.webmanifest       manifest do cliente
sw.js                      service worker (cobre os dois apps)
shared/dados.js            catálogo, persistência, motor de respostas e ponte SmartGO
admin/index.html           painel do administrador
admin/manifest.webmanifest manifest do admin
icons/                     ícones PWA
```

Os dois apps compartilham `shared/dados.js` e o mesmo `localStorage`, então um
produto cadastrado no admin aparece na consulta do cliente imediatamente.

## Como as respostas funcionam

1. **Catálogo local** — preço, estoque, corredor, promoções. Instantâneo,
   gratuito e incapaz de inventar dado, porque só lê o que está cadastrado.
2. **SmartGO** — recebe o que a camada 1 não resolveu.
3. **OpenRouter** — alternativa à camada 2, caso você queira testar sem o
   SmartGO conectado.

Sem nenhuma integração configurada o app funciona normalmente: apenas informa
que não encontrou a informação no catálogo.

## Radar de perguntas

Toda consulta é registrada, marcando o que o catálogo não soube responder.
A aba Perguntas mostra o histórico e a taxa de atendimento — é a informação que
o varejo normalmente perde: o que o cliente procurou e não achou.

## Rodar localmente

Service worker e microfone exigem `https` ou `localhost`; abrir por `file://`
não funciona.

```bash
python3 -m http.server 8080
# http://localhost:8080
```

## Publicar no Cloudflare Pages

1. Suba esta pasta para um repositório no GitHub.
2. Cloudflare → **Workers & Pages** → **Create** → **Pages** → **Connect to Git**.
3. Selecione o repositório:
   - Framework preset: **None**
   - Build command: vazio
   - Build output directory: `/` (ou o nome da pasta, se não estiver na raiz)
4. Em cerca de um minuto sobe em `https://seu-projeto.pages.dev`.

O HTTPS já vem incluso, que é o que libera microfone, instalação do PWA e
service worker.

Ao publicar mudanças, suba a versão do `CACHE` em `sw.js` para invalidar o
cache antigo.

## Limitações conhecidas

- **Microfone**: usa a Web Speech API — boa no Chrome e no Edge, instável no
  Safari, indisponível em WebView do Android. Para app híbrido, troque por
  `MediaRecorder` mais transcrição via API.
- **Chaves no front-end**: aceitável em demo, inaceitável em produção. Devem
  migrar para a API .NET.
- **Dados em `localStorage`**: cada dispositivo tem seu próprio catálogo. Ao
  ligar a API, só as funções do objeto `Dados` precisam mudar.

## Próximo passo

Trocar `Dados` por chamadas à API .NET com Postgres (Neon ou Supabase, ambos
gratuitos) e hospedar a API no Azure App Service F1. A interface não muda.
