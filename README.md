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

Solução .NET 8 em camadas, 13 projetos. O `Domain` concentra as regras de negócio
nas entidades e não conhece a infraestrutura; a persistência entra por interfaces
de repositório.

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐    ┌──────────────┐
│  ClientApp   │  │  AdminApp    │  │  SuporteApp  │    │ EdgeDesktop  │
│ Blazor WASM  │  │ Blazor WASM  │  │ Blazor WASM  │    │     WPF      │
│   :7280      │  │   :7290      │  │   :7291      │    │              │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘    └──────┬───────┘
       │                 │                 │                   │
       └──── AutonomousStore.Gerente (o mesmo cérebro) ─────┘   │
       │                 │                 │                   │
       └───────────── HTTP / JSON · JWT ───┴───────────────────┘
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
| `Comum` | chamados de suporte, compartilhado pelos três apps |
| `ClientApp` | o comprador (Blazor WebAssembly) |
| `AdminApp` | o painel do dono (Blazor WebAssembly) |
| `SuporteApp` | o atendimento (Blazor WebAssembly) |
| `EdgeDesktop` | a máquina que fica na loja (WPF) |
| `Hardware` | abstrações dos dispositivos — RFID, relé, serial, TCP |
| `firmware/` | as soluções do ESP32 (.NET nanoFramework) |

**`AutonomousStore.Gerente` é uma biblioteca de componentes Razor**, e é o que faz
os três aplicativos web compartilharem o mesmo gerente: o classificador, o modelo
treinado, o chat e o motor de aprendizado ficam num lugar só. Cada app passa
apenas **quem está falando**.

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

### O mesmo cérebro, duas conversas

O gerente atende o dono e o comprador, e sabe a diferença.

|  | administração | cliente |
|---|---|---|
| tratamento | "Chefe" | o primeiro nome de quem está logado |
| intenções | as 42 | 12 |
| pode alterar o sistema | sim, sempre confirmando | **não** |
| sugestões na tela | 16 | 8 |

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
abandonada · o gerente virtual atendendo dono e cliente com barreiras separadas ·
a rede neural treinando dentro do navegador.

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

---

## Licença

MIT — ver [`LICENSE`](LICENSE).
