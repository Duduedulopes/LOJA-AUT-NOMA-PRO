# AutonomousStore

**Loja autônoma com um gerente virtual que aprende dentro dela**

Varejo sem fricção × rede neural escrita do zero × percepção espacial

Eduardo Lopes, 2026 — MIT, ver [`LICENSE`](LICENSE)

---

## O problema

O varejo tradicional perde receita por atrito. O consumidor abandona a compra ao
enfrentar fila, o controle manual de estoque erra, e manter uma loja 24 horas com
operadores é proibitivo para o pequeno investidor.

Tirar o caixa resolve a fila e cria três perguntas novas: **o quê** saiu da
prateleira, **quem** levou, e **se a conta fecha** no fim do dia.

O RFID responde a primeira com precisão e é cego para as outras duas. As câmeras
respondem a segunda e não sabem o preço de nada. E a terceira — a que decide se a
loja dá lucro ou prejuízo — não está em nenhum dos dois: ela mora na diferença
entre o que os dois viram.

Quem opera a loja fica então com três sistemas e nenhuma resposta. Perguntar
"tivemos furo hoje?" exige abrir um painel, cruzar com outro, e saber de antemão
o que procurar.

## A solução

Um ecossistema autônomo que funciona sem operador de caixa:

1. **Entrada sem atrito** — acesso liberado por QR code assinado no app do cliente
2. **Compra autônoma** — etiquetas RFID identificam os produtos retirados
3. **Pagamento transparente** — checkout automático, sem fila
4. **Um gerente que entende a pergunta** — e vai buscar a resposta nos três sistemas

O gerente não é um chatbot ligado a uma API de terceiros. É uma rede neural
escrita do zero — sem TensorFlow, sem PyTorch, sem chamada a modelo de fora — que
classifica a intenção de uma frase digitada com pressa e sem acento, **treina
dentro do navegador da loja** quando alguém corrige o palpite dela, e devolve ao
projeto em Python o que aprendeu.

> O Python é onde a rede nasce e é medida. O C# é onde ela vive e aprende.
> O `intencao.json` é a língua que os dois falam — e ela vai nos dois sentidos.

## Jornada do cliente

```
  Entrada          Interação        Processamento      Checkout          Saída
  ───────          ─────────        ─────────────      ────────          ─────
  QR code     →    etiqueta    →    API + SQL     →    total no     →    validação
  no app           RFID lida        carrinho           app               carrinho × pago
                   pelo ESP32       + estoque
```

---

## O ecossistema

Três repositórios, três problemas diferentes, um sistema só.

| repositório | o que resolve | linguagem |
|---|---|---|
| **AutonomousStore** (aqui) | a loja: catálogo, sessão de compra, RFID, pagamento, o gerente em execução | C# / .NET 8 |
| [**Rede-Neural**](https://github.com/Duduedulopes/Rede-Neural) | onde a rede nasce: corpus, treino, validação cruzada, o monitor | Python / NumPy |
| [**SO-Espacial**](https://github.com/Duduedulopes/SO-Espacial) | quem pegou, e de qual prateleira, com três webcams comuns | Python / OpenCV |

```
                    ┌──────────────────────────┐
                    │      Rede-Neural         │
                    │  treino · validação      │
                    │  cruzada · monitor       │
                    └────────┬────────▲────────┘
             intencao.json   │        │   modelo aprendido
             (os pesos)      │        │   + correções da loja
                    ┌────────▼────────┴────────┐
                    │     AutonomousStore      │
                    │  a rede EXECUTA e TREINA │
                    │  no navegador da loja    │
                    └────────┬────────▲────────┘
                             │        │
                     RFID: o quê      │  câmeras: quem, e de onde
                             │        │
                    ┌────────▼────────┴────────┐
                    │       SO-Espacial        │
                    └──────────────────────────┘
```

---

## Arquitetura

Solução .NET 8 em camadas, 14 projetos. É uma plataforma multiempresa (SaaS):
uma instalação atende várias empresas assinantes, cada uma isolada por um
`Tenant` — o filtro fica no `AutonomousDbContext` e é global, não uma checagem
espalhada pelos controllers. O `Domain` concentra as regras de negócio nas
entidades e não conhece a infraestrutura; a persistência entra por interfaces
de repositório.

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    ┌──────────────┐
│  ClientApp   │  │   AdminApp   │  │  SuporteApp  │  │  CriadorApp  │    │ EdgeDesktop  │
│ Blazor WASM  │  │ Blazor WASM  │  │ Blazor WASM  │  │ Blazor WASM  │    │     WPF      │
│    :7280     │  │    :7290     │  │    :7291     │  │    :7293     │    │              │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘    └──────┬───────┘
       │                 │                 │                 │                   │
       └───── AutonomousStore.Gerente (o mesmo cérebro) ─────┘                   │
       │                 │                 │                 │                   │
       └─────────────────────────── HTTP / JSON · JWT ───────────────────────────┘
                              │
                   ┌──────────▼──────────┐        ┌──────────────┐
                   │       WebApi        │◄─HTTP──┤    ESP32     │
                   │   ASP.NET Core 8    │        │  RC522 RFID  │
                   │       :7167         │        └──────────────┘
                   └──────────┬──────────┘
                              │
       ┌──────────────────────┼──────────────────────┐
┌──────▼──────┐  ┌────────────▼─────────┐  ┌─────────▼────┐
│ Application │  │   Infrastructure     │  │   Hardware   │
│  casos de   │  │   EF Core 8 · SQL    │  │ RFID · relé  │
│    uso      │  │      Server          │  │ serial · TCP │
└──────┬──────┘  └──────────┬───────────┘  └──────────────┘
       └────────────────────┤
                     ┌──────▼──────┐
                     │   Domain    │  entidades e regras — não depende de nada
                     └─────────────┘
```

| projeto | papel |
|---|---|
| `Domain` | entidades e regras de negócio |
| `Application` | casos de uso |
| `Infrastructure` | EF Core 8, repositórios, SQL Server |
| `WebApi` | ASP.NET Core, REST, JWT, Swagger |
| `Gerente` | a rede neural, o chat e o aprendizado — biblioteca Razor |
| `Comum` | chamados de suporte, compartilhado pelos quatro apps |
| `ClientApp` | o comprador (Blazor WebAssembly) |
| `AdminApp` | o painel do dono da empresa (Blazor WebAssembly) |
| `SuporteApp` | o atendimento (Blazor WebAssembly) |
| `CriadorApp` | o painel do dono da plataforma — empresas, lojas, técnicos (Blazor WebAssembly) |
| `EdgeDesktop` | a máquina que fica na loja (WPF) |
| `Hardware` | abstrações dos dispositivos — RFID, relé, serial, TCP |
| `firmware/` | as soluções do ESP32 (.NET nanoFramework) |

**`AutonomousStore.Gerente` é uma biblioteca de componentes Razor**, e é o que faz
os quatro aplicativos web compartilharem o mesmo gerente: o classificador, o
modelo treinado, o chat e o motor de aprendizado ficam num lugar só. Cada app
passa apenas **quem está falando**.

---

## Multiempresa (SaaS)

Uma instalação, várias empresas. Cada empresa assinante é uma fronteira de
isolamento — um `Tenant` — e o filtro que garante isso é global, aplicado em
toda consulta, não uma checagem espalhada pelos controllers.

```
Criador                     dono da instalação — cadastra e suspende empresas
  │
  ├─ Suporte                 serviço DO CRIADOR, não da empresa — atende chamados de todas elas
  │
  └─ Empresa (Tenant)        a assinante
       ├─ Admin              dono da empresa — só consulta as próprias lojas
       ├─ Loja (Store)       até o limite do plano
       └─ Comprador          pertence a uma única empresa
```

Quatro papéis: **Criador** (dono da plataforma), **Suporte** (técnico, cadastrado
só pelo Criador; dados de comprador ficam mascarados até "revelar com motivo", e
a empresa aparece para ele como código + nome fantasia), **Admin** (dono da
empresa assinante) e **Comprador**.

Abrir, renomear, desativar e reativar uma loja é negócio da assinatura, não do
Admin — ele só consulta as que já tem. Essa regra mora num controller à parte do
painel do Criador: em ASP.NET Core, `[Authorize(Roles=...)]` de classe e de
método **somam**, nunca afrouxam — dar essa permissão ao Suporte dentro do
controller do Criador teria aberto o resto do painel do Criador para o Suporte
junto.

O limite de lojas conta só as **ativas** — `Tenant.PodeTerMaisUmaLoja` confere ao
criar, ao reativar e sempre que alguém baixa o limite do plano. Desativar libera
a vaga na hora; não apaga nada. Empresa suspensa não ganha capacidade nova: criar
e reativar loja dão 403, desativar continua liberado.

Toda ação de quem opera a plataforma — abrir uma empresa, suspender, revelar um
dado mascarado — vira um registro de **auditoria que só se acrescenta**: não
existe método para reescrever uma linha. Fica quem fez, quando, em qual empresa
e, quando a ação pede (como revelar um dado de comprador), o motivo.

O `AutonomousStore.CriadorApp` é o painel do Criador — o quarto aplicativo
Blazor. Em vez de uma tabela genérica de empresas, um "Mapa da Rede" em SVG
desenha a própria hierarquia do produto: o Criador no centro, cada empresa numa
órbita, o raio marca o uso e um pulso marca chamado esperando. Dali ele cadastra
e suspende empresas, abre lojas, cadastra técnicos e vê a fila e o histórico de
chamados de toda a plataforma.

O gerente virtual sabe com qual dos quatro está falando — ver "O mesmo cérebro,
quatro conversas" mais abaixo.

---

## O hardware

A ponta física do sistema é um **ESP32** rodando **.NET nanoFramework** — ou seja,
o firmware do microcontrolador também é C#. Do navegador do cliente ao chip que lê
a etiqueta, a pilha inteira é uma linguagem só.

São três peças, e só três:

| componente | barramento | papel |
|---|---|---|
| **ESP32** | — | Wi-Fi, executa o firmware e fala com a API |
| **RC522** | SPI | o leitor: lê a etiqueta RFID de 13,56 MHz colada no produto |
| **SSD1306** (OLED) | I²C | mostra qual produto acabou de ser lido |

O firmware é construído em **etapas incrementais**, cada uma uma solução que
compila e roda sozinha — pisca-LED, leitura RFID, display, Wi-Fi, chamada à API,
leitor de saída. Serve para isolar defeito de hardware de defeito de software:
quando a leitura falha, dá para voltar uma etapa e saber em qual camada o
problema está.

O caminho crítico opera em hardware real: a etiqueta é lida pelo ESP32, enviada
por Wi-Fi à API, o produto é resolvido no SQL Server, o item é somado à sessão e
o estoque decrementado.

### MVP × produto real

Este MVP usa **RFID de 13,56 MHz** (RC522), que exige aproximar cada produto do
leitor. É suficiente para validar o fluxo completo com hardware de baixo custo, e
foi essa a escolha.

O produto real usaria **RFID UHF (860–960 MHz)**, com alcance de metros e leitura
de vários itens ao mesmo tempo. O cliente sairia da loja com os produtos e a
compra fecharia sem passar nada por leitor nenhum — que é o objetivo do conceito
de varejo sem fricção.

**A arquitetura de software não muda.** O leitor UHF continua mandando etiquetas
para o mesmo endpoint. O que muda é o hardware de borda e o custo por etiqueta.

---

## O gerente

### A rede

Classificador estilo fastText, escrito à mão em C# e em NumPy:

```
frase → peças (palavras + trigramas de caractere) → embutimento 24d
      → média → camada oculta 32 (sigmoide) → softmax
                                              ├─ 42 intenções
                                              └─ 7 tons
```

| | |
|---|---|
| peças no vocabulário | 3.409 |
| dimensão do embutimento | 24 |
| camada oculta | 32 (sigmoide) |
| intenções | 42 |
| parâmetros treináveis (caminho de intenção) | 84.002 |
| corpus de treino | 5.586 frases |
| épocas | 80 |

### Os números

Medidos em **validação cruzada agrupada por frase-base** — variantes da mesma
frase ("chocolates" / "chocolstes") ficam sempre na mesma dobra. Sem esse
agrupamento a acurácia sobe 27 pontos sem o modelo ter melhorado em nada.

| medida | valor |
|---|---|
| acerto no 1º palpite | 58,2% |
| resposta certa entre os 3 primeiros | 76,2% |
| precisão acima do limiar (0,95) | 79,0% |
| cobertura acima do limiar | 47,1% |
| termina certo depois do clique | 72,3% |
| erro silencioso | 9,9% |

Abaixo do limiar a rede não chuta: ela mostra os três palpites mais prováveis e
pede um clique. E toda alteração no banco pede confirmação explícita antes de
gravar — mesmo com 99% de certeza.

### O aprendizado dentro da loja

O clique que corrige o gerente **não é só um registro**: é um passo de
retropropagação de verdade, no tronco, na cabeça de intenção e na tabela de
embutimento, executado no navegador.

O gradiente do C# foi conferido contra a derivada numérica
`(C(w+ε) − C(w−ε)) / 2ε`, com um gradiente propositalmente errado (sem a derivada
da sigmoide) como controle — sem um controle, um teste de gradiente só mostra que
a conta é consistente consigo mesma. O método `RedeTreinavel.ConferirNumericamente`
está em
[`Services/Aprendizado/Retropropagacao.cs`](AutonomousStore/AutonomousStore.Gerente/Services/Aprendizado/Retropropagacao.cs)
e é o mesmo `conferir_numericamente` do projeto em Python.

**E aprender sozinho quebra o que já se sabia.** Ensinar uma frase com cinco
passos fortes corrige essa frase e quebra mais de uma dúzia de outras. Isso é
esquecimento catastrófico, e numa rede de 84 mil parâmetros ele não é sutil.

A trava que resolve não proíbe — mede:

1. tenta aprender com taxa forte (0,5) e vai baixando até 0,01
2. depois de cada tentativa, mede o acerto num conjunto de guarda de **764
   frases** que o Python gerou
3. só aceita o passo se a frase ensinada passou a ser respondida certo **e** o
   acerto na guarda não caiu **e** continua ≥ ao do modelo original
4. se nenhuma taxa servir, desfaz e diz que não deu

O terceiro critério foi aprendido apanhando: comparar só com o estado atual
deixou passar cinco correções que, uma a uma, "não pioravam" — e juntas quebraram
onze frases. Medir a inclinação e ignorar a altura deixa um modelo descer um
degrau de cada vez para sempre.

**O modelo do Python nunca é alterado.** Fica guardado inteiro, e o botão de
reiniciar volta a ele. Sistema que aprende sem ter como voltar atrás é sistema que
ninguém liga.

### A ponte com o Python

| sentido | rota | o que leva |
|---|---|---|
| loja → Python | `POST /api/correcao` | a correção, o veredito da trava, a taxa usada e **de qual app veio** |
| loja → Python | `POST /api/modelo` | os pesos que a loja aprendeu + o boletim do que mudou |
| Python → loja | `GET /api/modelo` | o `intencao.json` recalibrado |

O monitor roda em `localhost:8760`, no repositório
[Rede-Neural](https://github.com/Duduedulopes/Rede-Neural). **A loja aprende mesmo
com ele desligado** — o passo de gradiente acontece no navegador, e o envio é uma
tentativa que pode falhar em silêncio. Um monitor fora do ar não pode custar uma
tarde de correções.

### O mesmo cérebro, quatro conversas

O gerente atende quatro perfis — dono da loja, comprador, técnico de suporte e
dono da plataforma — e sabe a diferença. Hoje todos são tratados pelo primeiro
nome: o gerente já chamou o dono da loja (e o Criador) de "Chefe", e a decisão
foi passar a chamar todo mundo pelo nome, sem título.

|  | Chefe (AdminApp) | Cliente (ClientApp) | Técnico (SuporteApp) | Criador (CriadorApp) |
|---|---|---|---|---|
| intenções | as 42 | 12 | 8 | 8 — por enquanto, a mesma lista do técnico |
| pode alterar o sistema | sim (limitações) | **não** | sim | sim |
| enxerga várias empresas | não | não  | sim | sim |

Técnico e Criador **não herdam a lista do Chefe**: "quanto faturamos hoje?"
respondido a quem vê todas as empresas somaria o dinheiro de empresas
diferentes, e nenhum dos dois lê caixa de ninguém. O que é só do Criador
(quantas empresas, quais suspensas, o que o suporte fez) ainda vai virar
intenção nova — precisa de corpus e treino próprios no Rede-Neural.

A separação é **lista de permissão, não de bloqueio** — e isso decide o futuro:
com lista de bloqueio, toda intenção nova que for treinada nasce liberada para o
cliente e alguém precisa lembrar de fechá-la. Com lista de permissão, nasce
fechada. Errar esquecendo é inevitável; o que se escolhe aqui é para que lado o
esquecimento erra.

A barreira fica **no serviço, não na tela**: filtrar só os botões deixaria a porta
aberta para quem digita a frase certa.

---

## O SO-Espacial

O RFID diz **o quê** e **quantos**. Não diz quem pegou, de qual prateleira, nem o
que foi pego e devolvido antes de a pessoa ir embora. Câmeras respondem isso — mas
uma câmera comum entrega uma imagem plana: ao projetar o mundo em pixels, a
profundidade se perde.

O caminho usual é comprar o sensor de volta — câmera de profundidade, LiDAR,
estéreo calibrado. Isso resolve a geometria e destrói o custo por loja. O
[SO-Espacial](https://github.com/Duduedulopes/SO-Espacial) devolve a dimensão
perdida **com restrições, não com hardware**: os pés estão no chão (um plano, logo
homografia dá posição em metros) e a razão altura/horizonte é invariante à
distância (logo metrologia de vista única dá a altura da mão). Três webcams comuns
e geometria.

**Cada câmera tem um papel, e nenhuma faz tudo:**

| papel | responde |
|---|---|
| **alto** (cenital) | posição no piso, rumo do corpo, estatura |
| **frontal** | qual braço se move, a que altura a mão chega |
| **lateral** | o quanto o braço avança para a gôndola — separa *pegar* de *passar perto* |

A decisão que faz o sistema funcionar é como as três se combinam:

> **A fusão não é média — é voto.**
> Cada câmera publica um valor de um **vocabulário fechado**, e a decisão é
> discreta. A média herda o erro de todas as fontes; o voto sobrevive ao erro da
> pior delas.
>
> Um bit sobrevive ao ruído que destrói um ângulo.

E quando uma câmera não enxerga, o campo chega `None` e **simplesmente não vota**.
Nada é inventado para preencher a lacuna: abster-se é um resultado de primeira
classe em todo o sistema — a mesma regra que o gerente segue quando fica abaixo do
limiar.

| camada | tecnologia |
|---|---|
| visão | OpenCV, YOLO11-pose (Ultralytics), MediaPipe Pose Landmarker |
| geometria | homografia por DLT, metrologia de vista única, SVD para registro |
| rastreamento | filtro de Kalman 2D em metros, recostura de identidade |
| fusão | por eixo e por mérito — cada vista responde o que enxerga melhor |
| câmeras | USB (DirectShow/MJPG) e remotas por MJPEG sobre HTTP |
| saída | JSON atômico, JSONL de eventos, cena 3D em OpenCV |

O gerente lê o estado espacial pelo monitor, em `/api/gerente/espacial`, e é assim
que ele responde "quantas pessoas estão na loja agora?". **Sem o monitor no ar, o
gerente diz que não conseguiu olhar** — em vez de responder que está tudo certo.

---

## Chamados e ocorrências

Um alarme que só aparece na tela e some com a resposta HTTP não deixa responder
"tivemos algum furo essa semana?" — a pergunta exigiria inventar. Por isso todo
detector do backend, do Agente de IA ou do SO-Espacial que percebe algo errado
grava uma `Ocorrencia`, com o fato e o palpite sobre a causa em campos
**separados** — confundir os dois faria o dono da loja agir com uma certeza que
ninguém mediu.

O mesmo fato repetido não vira linha nova: uma chave (`"assunto:id"`) identifica
o fato, e a repetição só soma um contador — sem isso, um defeito que dispara cem
vezes por minuto empurraria para fora da tela tudo o que veio antes. Uma
`CorrelationId` amarra as três ou quatro ocorrências que a mesma sessão
problemática costuma gerar, para o suporte não investigar quatro vezes o mesmo
caso.

Nem todo chamado nasce de detector: um comprador pode pedir ajuda e um Admin
pode relatar um problema — os dois viram **donos** do próprio chamado, e o Admin
**não é "da casa"**: no painel dele aparecem só os alertas que os detectores
acharam e os pedidos que ele mesmo escreveu, nunca os de outra pessoa. Quem
enxerga tudo — os pedidos de qualquer comprador, de qualquer empresa — é o
Suporte e o Criador, porque o Suporte é serviço do Criador, não da empresa.

A conversa decide o estado sozinha, sem ninguém precisar lembrar de mexer:
quando o técnico responde, o chamado vira "em análise"; se alguém escreve de
novo num chamado já dado como resolvido, ele não estava resolvido — volta para
o suporte. E se o mesmo fato voltar a acontecer depois de "resolvido", a
ocorrência reabre: "resolvida" é uma afirmação sobre o mundo, e se o problema
voltou, a afirmação estava errada.

---

## Pilha tecnológica

| camada | tecnologia |
|---|---|
| backend | .NET 8, ASP.NET Core, Entity Framework Core 8 |
| banco | SQL Server |
| front-end | Blazor WebAssembly (PWA), WPF |
| autenticação | JWT, login com Google |
| rede neural | C# e NumPy, escritas do zero |
| visão (câmera de estoque) | Google Gemini Vision |
| firmware | C# sobre .NET nanoFramework |
| hardware | ESP32, RC522 (SPI), OLED SSD1306 (I²C) |

## Estrutura

```
AutonomousStore/
├─ AutonomousStore.Domain/          entidades e regras — não depende de nada
├─ AutonomousStore.Application/     casos de uso
├─ AutonomousStore.Infrastructure/  EF Core 8, SQL Server, repositórios
├─ AutonomousStore.WebApi/          ASP.NET Core 8, JWT, Gemini (visão)
├─ AutonomousStore.Gerente/          a rede neural, o chat e o aprendizado
│  ├─ Services/ClassificadorDeIntencao.cs
│  ├─ Services/Aprendizado/         retropropagação, trava, ponte com o Python
│  ├─ Services/Agente/              conversa, permissões, leitura de valores
│  ├─ Componentes/GerenteChat.razor
│  ├─ PerfilDeQuemFala.cs           quem fala, e o que pode ouvir
│  └─ wwwroot/modelos/              intencao.json · guarda.json
├─ AutonomousStore.Comum/           chamados de suporte, compartilhado
├─ AutonomousStore.ClientApp/       Blazor WASM — o comprador
├─ AutonomousStore.AdminApp/        Blazor WASM — o dono
├─ AutonomousStore.SuporteApp/      Blazor WASM — o suporte
├─ AutonomousStore.CriadorApp/      Blazor WASM — o Criador (multiempresa)
├─ AutonomousStore.EdgeDesktop/     WPF — a máquina da loja
├─ AutonomousStore.Hardware/        RFID, relé, serial, TCP
├─ Smart-store-PWA/                 protótipo anterior, HTML puro
└─ *.Tests/                         xUnit
firmware/                           ESP32 + RC522, .NET nanoFramework
├─ Etapa2Pisca/  Etapa3Rfid2/  Etapa4Oled/
└─ Etapa6Wifi/   Etapa7Api/    Etapa8Saida/
```


## Estado atual

**MVP funcional**, com o caminho crítico rodando em hardware real.

Pronto: cadastro e login (senha e Google) · QR code de entrada com validade ·
confirmação de entrada por token · catálogo com estoque · vinculação de etiqueta
RFID a produto · leitor de saída em ESP32 · painel administrativo · atendimento e
chamados de suporte · assistente e visão com Gemini · expiração de sessão
abandonada · plataforma multiempresa com isolamento por `Tenant`, quatro papéis
(Criador, Suporte, Admin, Comprador) e limite de lojas por plano · painel central
do Criador com o Mapa da Rede · o gerente virtual atendendo os quatro perfis com
barreiras separadas · a rede neural treinando dentro do navegador.

## Roadmap

- [ ] **Persistir o que a loja aprendeu** — hoje o modelo treinado vive na memória
      da aba e morre no F5; enviar ao Python salva, mas só com o monitor ligado
- [ ] **Reconciliação RFID × câmera** — os dois sistemas já se falam; cruzar as
      duas leituras do mesmo gesto é a próxima decisão de arquitetura
- [ ] **Entrada autônoma** — hoje a liberação depende de ação do admin; o cliente
      deve poder abrir a loja sozinho
- [ ] **Fechamento da compra** — `checkout` e `confirm-payment` acionados pelo
      ClientApp
- [ ] **Texto no OLED** com produto e preço a cada leitura
- [ ] **Sensor de presença** na zona de entrada
- [ ] **Gateway de pagamento real**
- [ ] **Migração para RFID UHF** — ver "MVP × produto real"
- [ ] **Publicar a plataforma multiempresa** — cadastro self-service de empresa;
      cobrança da assinatura é manual por enquanto

---

## Licença

MIT — ver [`LICENSE`](LICENSE).

---

## Projetos relacionados

Este repositório é **parte do ecossistema completo** de varejo autônomo. A loja
sabe **o quê** saiu da prateleira e quanto custa, mas sozinha não sabe quem
levou, de qual prateleira, nem como interpretar uma pergunta em português torto.

| Repositório | O quê |
|---|---|
| [Rede-Neural](https://github.com/Duduedulopes/Rede-Neural) | onde a rede nasce — corpus, treino, validação cruzada e o monitor |
| [SO-Espacial](https://github.com/Duduedulopes/SO-Espacial) | a percepção espacial por câmeras — quem pegou, e de qual prateleira |
| **este** | a loja em .NET 8 — API multiempresa, apps Blazor (loja, dono, suporte e plataforma), firmware ESP32, e o gerente em execução |

O gerente e a rede neural já trocam dados nos dois sentidos: a loja treina no
navegador e devolve ao Python o que aprendeu. **Cruzar a leitura do RFID com a da
câmera ainda não acontece automaticamente** — os dois sistemas se falam, e a
reconciliação entre eles é a próxima decisão de arquitetura, não um trabalho de
encanamento pendente.

Site do projeto: **[smart-store.contato-dudulopes.workers.dev](https://smart-store.contato-dudulopes.workers.dev)**
