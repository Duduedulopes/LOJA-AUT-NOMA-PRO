# Smart Store

**Sistema autônomo de varejo e controle de estoque 24h**

IoT × Inteligência Artificial × Varejo sem fricção

Eduardo Lopes, 2026 — código aberto para leitura, ver `LICENSE`

---

## O problema

O varejo tradicional perde receita por atrito. Consumidores abandonam a compra ao
enfrentar filas, o controle manual de estoque erra, e manter uma loja 24 horas com
operadores é proibitivo para o pequeno investidor.

## A solução

Ecossistema autônomo que funciona sem operador de caixa:

1. **Entrada sem atrito** — acesso liberado por QR code no app do cliente
2. **Compra autônoma** — tags RFID identificam os produtos retirados
3. **Pagamento transparente** — checkout automático, sem fila

## Jornada do cliente

```
Entrada          Interação        Processamento      Checkout         Saída
QR code    →     tag RFID    →    API + SQL     →    total no    →    validação
no app           lida             carrinho           app              carrinho × pago
```

## Arquitetura

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  ClientApp   │  │  AdminApp    │  │ EdgeDesktop  │
│ Blazor WASM  │  │ Blazor WASM  │  │     WPF      │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       └────────── HTTP / JSON ────────────┘
                         │
              ┌──────────▼──────────┐        ┌──────────────┐
              │       WebApi        │◄─HTTP──┤    ESP32     │
              │   ASP.NET Core 8    │        │  RC522 RFID  │
              └──────────┬──────────┘        └──────────────┘
                         │
       ┌─────────────────┼─────────────────┐
┌──────▼──────┐  ┌───────▼──────┐  ┌───────▼──────┐
│ Application │  │Infrastructure│  │   Hardware   │
│             │  │  EF Core 8   │  │              │
└──────┬──────┘  └───────┬──────┘  └──────────────┘
       └────────┬────────┘
         ┌──────▼──────┐        ┌──────────────┐
         │   Domain    │        │  SQL Server  │
         └─────────────┘        └──────────────┘
```

Solução .NET 8 em camadas, 10 projetos. O `Domain` concentra as regras de negócio nas
entidades e não conhece a infraestrutura; a persistência entra por interfaces de
repositório.

| Projeto | Papel |
|---|---|
| `Domain` | entidades e regras de negócio |
| `Application` | casos de uso |
| `Infrastructure` | EF Core, repositórios, SQL Server |
| `WebApi` | ASP.NET Core, REST, JWT, Swagger |
| `ClientApp` | app do cliente (Blazor WebAssembly) |
| `AdminApp` | painel administrativo (Blazor WebAssembly) |
| `EdgeDesktop` | aplicação local da loja (WPF) |
| `Hardware` | abstrações dos dispositivos |
| `firmware/` | soluções do ESP32 (.NET nanoFramework) |

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 8, ASP.NET Core, Entity Framework Core 8 |
| Banco | SQL Server |
| Front-end | Blazor WebAssembly, WPF |
| Autenticação | JWT, login Google |
| IA | Google Gemini — assistente e visão computacional |
| Firmware | C# sobre .NET nanoFramework |
| Hardware | ESP32 DevKit V1, RC522 (SPI), OLED SSD1306 (I2C), PIR HC-SR501 |

## Estado atual

MVP funcional. O caminho crítico opera em hardware real: tag RFID lida pelo ESP32,
enviada por Wi-Fi à API, produto resolvido no SQL Server, item somado à sessão e estoque
decrementado.

**Pronto:** cadastro e login (senha e Google), QR code de entrada com validade,
confirmação de entrada por token, catálogo com estoque, vinculação de tag RFID a produto,
leitor de saída em ESP32, painel administrativo, assistente e visão com Gemini, expiração
de sessão abandonada.

## Roadmap

- [x] **Segredos fora do código** — ver "Configuração local" abaixo
- [ ] **Entrada autônoma por código de barras** — hoje a liberação depende de ação do admin; o cliente deve poder abrir a loja sozinho
- [ ] Fechamento da compra: `checkout` e `confirm-payment` acionados pelo ClientApp
- [ ] Texto no OLED com produto e preço a cada leitura
- [ ] Sensor PIR integrado ao fluxo de entrada
- [ ] Testes em `Domain.Tests` e `Application.Tests`
- [ ] Gateway de pagamento real
- [ ] **Migração para RFID UHF** — ver abaixo

## MVP × produto real

Este MVP usa **RFID 13,56 MHz (RC522)**, que exige aproximar cada produto do leitor. É
suficiente para validar o fluxo completo com hardware de baixo custo.

O produto real usaria **RFID UHF (860–960 MHz)**, com alcance de metros e leitura de vários
itens simultaneamente. O cliente sairia da loja com os produtos e a compra seria fechada
sem passar nada por leitor nenhum — o que é o objetivo do conceito de varejo sem fricção.

A arquitetura de software não muda: o leitor UHF continua enviando tags para o mesmo
endpoint. Muda o hardware de borda e o custo por etiqueta.

## Documentação

| Arquivo | Conteúdo |
|---|---|
| `PROXIMOS-PASSOS.md` | pinagem, ciclo de trabalho do firmware e armadilhas conhecidas |
| `ESTUDO-DE-CASO.md` | o que foi construído, passo a passo |
| `documentos/` | apresentação executiva e documento de projeto |
| `diagrama/` | montagem da protoboard, uma página por etapa |

## Como rodar

Requisitos: .NET 8 SDK, SQL Server, Visual Studio 2022. Para o firmware: extensão .NET
nanoFramework e a ferramenta `nanoff`.

```bash
dotnet ef database update --project AutonomousStore.Infrastructure ^
                          --startup-project AutonomousStore.WebApi
```

No Visual Studio, definir WebApi, ClientApp e AdminApp como projetos de inicialização.

A API escuta em `https://localhost:7167` e em `http://0.0.0.0:5071` — o segundo para o
ESP32 alcançar o PC pela rede local.

## Configuração local

Nenhum segredo está versionado. Para rodar o projeto é preciso criar dois arquivos e uma
variável de ambiente.

**1. API** — criar `AutonomousStore.WebApi/appsettings.Development.json` com os valores
reais. O `appsettings.json` versionado tem a estrutura completa com placeholders; copie e
substitua. Precisa de: connection string, chave do JWT, Client ID do Google, chave do
Gemini e credenciais SMTP.

**2. Migrations do EF Core** — a fábrica de design-time lê a connection string de uma
variável de ambiente. Definir uma vez, no PowerShell:

```powershell
[Environment]::SetEnvironmentVariable(
    "AUTONOMOUSSTORE_CONNECTION",
    "Server=SEU_SERVIDOR\SQLEXPRESS;Database=AutonomousStoreDb;Trusted_Connection=True;TrustServerCertificate=True;",
    "User")
```

Feche e reabra o Visual Studio depois de definir.

**3. Firmware** — em cada projeto de `firmware/`, copiar `Segredos.exemplo.cs` para
`Segredos.cs` e preencher `Ssid`, `SenhaWifi` e `BaseUrl`. O `Segredos.cs` está no
`.gitignore`.

---

## Licença

**MIT** — veja [`LICENSE`](LICENSE). Use, copie, modifique e venda; basta manter o
aviso de copyright.

A licença cobre o código deste repositório. As bibliotecas restauradas pelo NuGet e
os pacotes do nanoFramework têm licenças próprias.

---

## Projetos relacionados

Este repositório é **metade do sistema**. A etiqueta RFID identifica o item e conta
o que sai pelo portal — ela sabe **o quê** e **quantos**, e não sabe **quem** pegou
nem **de qual prateleira**. Essa outra metade é um projeto separado, em Python.

| Repositório | O quê |
|---|---|
| **este** | a loja autônoma em .NET 8 — API, apps Blazor, firmware ESP32 |
| [SO-Espacial](https://github.com/Duduedulopes/SO-Espacial) | a percepção espacial por câmeras — o gêmeo digital da loja |

As duas leituras do mesmo gesto ainda **não se falam**: cada sistema opera sozinho,
e a reconciliação entre elas é a próxima decisão de arquitetura, não um trabalho de
encanamento pendente.

Site do projeto: **[smart-store.contato-dudulopes.workers.dev](https://smart-store.contato-dudulopes.workers.dev)**
