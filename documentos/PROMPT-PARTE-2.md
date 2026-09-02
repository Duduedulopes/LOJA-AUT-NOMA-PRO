# Prompt para a outra IA — Parte 2 completa

Cole tudo o que está entre as linhas. Escrito em 01/09/2026.

---

Você vai trabalhar no **AutonomousStore**, um sistema de loja autônoma em
.NET 8 / Blazor WebAssembly. Fale português comigo.

**Nomes que não mudam:** o sistema da loja é **AutonomousStore**; a visão
computacional é o **Sistema Espacial SO**; o gerente virtual é o **Agente de
IA**, uma rede neural feita do zero. Nunca use outro nome para nenhum dos
três.

---

## 1. ANTES DE QUALQUER COISA — o caminho

A IA anterior gastou a cota inteira procurando um arquivo que existe, porque
procurou no lugar errado. **A solução fica numa pasta `AutonomousStore`
ANINHADA dentro do repositório:**

```
LOJA AUTÔNOMA PRO/                          <- raiz do repositório
├── documentos/
└── AutonomousStore/                        <- A SOLUÇÃO ESTÁ AQUI
    ├── AutonomousStore.sln
    ├── AutonomousStore.AdminApp/
    │   ├── AppState.cs
    │   ├── AuthHeaderHandler.cs
    │   ├── Layout/MainLayout.razor
    │   ├── Models/          (OcorrenciaDto.cs já existe)
    │   ├── Pages/           (Home, Login, Produtos, Vendas, MonitorPrateleira)
    │   ├── Services/        (OcorrenciaApiService.cs já existe)
    │   ├── Shared/          (GerenteChat, NotificationBar, SeletorDeTema)
    │   └── wwwroot/css/tema.css
    ├── AutonomousStore.ClientApp/
    ├── AutonomousStore.WebApi/
    ├── AutonomousStore.Domain/
    └── AutonomousStore.Infrastructure/
```

Tudo roda de dentro de `LOJA AUTÔNOMA PRO/AutonomousStore`:

```
dotnet build
```

**Se um arquivo que eu citar parecer não existir, me pergunte antes de criar
outro.** Quase certamente é caminho, não ausência.

---

## 2. O que JÁ ESTÁ PRONTO — não refaça nada disto

A **Parte 1** está no ar: detecção, classificação e gravação de ocorrência. A
tabela `Ocorrencias` existe no banco (migração
`20260901132235_AdicionarOcorrencias`, já aplicada) e **a API responde de
verdade. Você não precisa de stub.**

Cinco detectores gravam sozinhos:

| Quando acontece | Vira | Onde |
|---|---|---|
| RFID da porta lê tag saindo sem pagamento | `Roubo`, severidade `Critica` | `SessionsController.VerifyExit` |
| Tag na porta sem produto cadastrado | `ErroDados`, `Alta` | idem |
| Sessão cancelada com carrinho cheio | `FuroDeSistema`, `Media`/`Alta` | `SessionsController.Cancel` |
| Câmera não viu mudança, com sessão aberta | `FuroDeCobertura`, `Informativa` | `VisionController` |
| Câmera viu produto fora do catálogo | `FuroDeCobertura`, `Media` | idem |

O gerente virtual já responde "tivemos algum furo?" lendo essa tabela.

No AdminApp já existem, prontos e registrados no `Program.cs`:

- `Models/OcorrenciaDto.cs` — o DTO, com os enums como `string`
- `Services/OcorrenciaApiService.cs` — `BuscarAsync` e `NaoVistasAsync`

**Os endpoints já aceitam o papel `Suporte`** (`[Authorize(Roles =
"Admin,Suporte")]`). O papel ainda não é emitido por ninguém — quem cria isso
é você, no item 5. Deixei o nome lá para você não ficar parada esperando uma
linha num arquivo meu.

---

## 3. O que é SEU

### 3.1 Sininho de alerta no AdminApp

Já existe `AutonomousStore.AdminApp/Shared/NotificationBar.razor`, com poll de
20 segundos e pílulas de aviso, renderizado em `Layout/MainLayout.razor`
linha 45. **Estenda esse componente, não crie outro.**

Adicione um sino com o contador de `GET /api/ocorrencias/nao-vistas`.
Vermelho quando `Criticas > 0`, âmbar quando houver não vistas sem críticas,
discreto quando zero. Clicar abre o painel.

Três coisas dele para aproveitar, não refazer:

- **O `PeriodicTimer` de 20s com `CancellationTokenSource` e
  `IAsyncDisposable` já está certo.** Pendure a chamada de `nao-vistas`
  dentro do `RefreshAsync` que já existe. **Não crie um segundo laço** — dois
  timers no mesmo layout é o dobro de requisição para o mesmo fim.
- **O `catch` vazio no `RefreshAsync` é deliberado:** API fora do ar mantém
  os últimos valores em vez de zerar. Um sino que marca **0** porque a rede
  caiu é pior que um sino desatualizado — ele diz "está tudo bem" quando
  ninguém olhou. Se mexer nisso, mostre estado de "não sei", nunca zero.
- `_ready` só vira `true` depois do primeiro `RefreshAsync`, para a barra não
  piscar vazia na entrada.

### 3.2 Painel de notificações no AdminApp

Lista com filtro por tipo, severidade, estado e período. Detalhe mostrando
todos os campos, com `DadosEnvolvidosJson` e `SequenciaJson` formatados de
forma legível (não despeje JSON cru na tela). Ações: marcar como vista,
resolver com nota, chamar suporte.

**`CausaProvavel` é INFERÊNCIA e tem de aparecer como tal** — nunca com a
mesma aparência de `Descricao`, que é fato observado. Um palpite exibido como
laudo faz o admin agir com uma certeza que ninguém mediu.

### 3.3 Botão "chamar suporte técnico"

No detalhe da ocorrência. Manda o que o admin já tentou para
`POST /api/ocorrencias/{id}/suporte`. A ocorrência passa a
`Estado = "NoSuporte"`.

### 3.4 `AutonomousStore.SuporteApp`

Projeto Blazor WebAssembly novo na solução, ao lado do AdminApp. É o
ambiente do técnico, que atende várias lojas:

- histórico completo de ocorrências, com filtros
- **rastro por `CorrelationId`** — todas as ocorrências de uma mesma sessão em
  ordem cronológica. É o que evita investigar quatro vezes o mesmo caso: uma
  sessão que deu errado costuma gerar três ou quatro ocorrências em módulos
  diferentes
- a fila de chamados (ocorrências com `Estado = "NoSuporte"`)
- o chat de suporte entre admin e técnico

### 3.5 Autenticação própria do suporte — **e ela vem PRIMEIRO**

Você perguntou se dava para deixar a autenticação por último e subir o app
atrás do login de admin. **Não deixe.** Faça a autenticação antes do app, e
o motivo é concreto:

- **O app de suporte existe justamente para ver o que o dono da loja não vê.**
  Rodar atrás do login de admin inverte isso — ele passaria a enxergar tudo
  *como* o dono, que é o oposto da separação que ele deveria criar.
- **Retrofit custa mais do que parece.** Depois seria refazer o handler de
  token, o `AppState`, o fluxo de login e todas as páginas.
- **Aqui é barato.** A tubulação já existe e espelha 1:1 o que há para admin.

O que existe hoje, para você copiar o formato:

- `Domain/Entities/AdminUser.cs` — Nome, Email, PasswordHash, IsActive
- `WebApi/Controllers/AdminAuthController.cs` — o login
- `WebApi/Services/JwtTokenService.cs` — `GenerateAdminToken` põe uma claim
  `ClaimTypes.Role = "Admin"` no token. É essa claim que o
  `[Authorize(Roles = ...)]` exige.
- `AdminApp/AuthHeaderHandler.cs` — anexa o token nas chamadas

O que fazer, espelhando:

1. `Domain/Entities/SuporteUser.cs` — **tabela separada, não um campo `Role`
   no `AdminUser`.** Admin e suporte são populações diferentes: o dono da
   loja não pode criar um usuário de suporte pela tela dele.
2. Migração nova para a tabela.
3. `IJwtTokenService.GenerateSuporteToken(SuporteUser)` — mesma estrutura do
   admin, com `ClaimTypes.Role = "Suporte"`.
4. `WebApi/Controllers/SuporteAuthController.cs`, espelhando o
   `AdminAuthController`.
5. O SuporteApp com o próprio login e o próprio handler de token.

**Não me peça para inventar senha nem para colar credencial no código.** Crie
o caminho de cadastro e me diga como eu crio o primeiro usuário.

---

## 4. O contrato

Fechado. Se precisar mudar, **pare e me pergunte.**

```csharp
public record OcorrenciaResponse(
    Guid Id,
    DateTime QuandoUtc,       // SEMPRE UTC — converta só na hora de mostrar
    string Sistema,           // "AutonomousStore" | "Sistema Espacial SO" | "Agente de IA"
    string Modulo,            // "SessionsController", "VisionController", ...
    string Operacao,          // "Cancel", "VerifyExit", "DetectShelfChange"
    string Tipo,
    string Severidade,
    string Descricao,         // FATO OBSERVADO
    string? DadosEnvolvidosJson,
    string? SequenciaJson,
    string? CausaProvavel,    // INFERÊNCIA — outra aparência na tela
    string? CausaRaiz,        // só quando alguém confirmou
    string? Impacto,
    string? Recomendacao,
    string? AcaoExecutada,
    string? Resultado,
    string Estado,
    Guid CorrelationId,       // amarra as ocorrências de uma mesma sessão
    DateTime? VistaEm,
    DateTime? ResolvidaEm,
    string? ResolvidaPor,
    string? NotaDoAdmin);

public record NaoVistasResponse(int Total, int Criticas, DateTime? MaisRecente);
```

**Os enums chegam como STRING, não número.** Valores possíveis:

```
Tipo          ErroExecucao | ErroDados | Anomalia | Contradicao | FalhaApi |
              FalhaWorkflow | FalhaIntegracao | FuroDeCobertura | Roubo | FuroDeSistema
Severidade    Informativa | Baixa | Media | Alta | Critica
Recomendacao  ApenasRegistrar | SugerirCorrecao | SolicitarAprovacao |
              BloquearOperacao | CorrigirAutomaticamente
Estado        Nova | Vista | EmAnalise | Resolvida | Ignorada | NoSuporte
```

**No seu lado guarde esses campos como `string`, nunca como enum tipado.** Se
amanhã nascer um tipo novo no servidor, um enum tipado estoura na
desserialização com um valor que não conhece, e a tela inteira cai por causa
de uma categoria nova. O `OcorrenciaDto.cs` que já existe é assim — reuse.

`CorrigirAutomaticamente` existe no enum e **nenhum detector usa**: o
construtor no servidor recusa. Se aparecer na sua tela é bug meu, mas trate o
valor sem quebrar.

### Endpoints

```
GET    /api/ocorrencias?desde=&ate=&tipo=&severidade=&estado=&correlationId=&limite=
GET    /api/ocorrencias/nao-vistas         -> NaoVistasResponse
GET    /api/ocorrencias/{id}
POST   /api/ocorrencias/{id}/vista
POST   /api/ocorrencias/{id}/resolver      { nota }
POST   /api/ocorrencias/{id}/suporte       { descricaoDoAdmin }
GET    /api/ocorrencias/resumo?desde=&ate=
```

Todos exigem `Admin` **ou** `Suporte`. `GET /api/ocorrencias` devolve
`OcorrenciaResponse[]` do mais recente para o mais antigo. Os POST devolvem o
`OcorrenciaResponse` atualizado. **`severidade` é PISO, não igualdade:** pedir
`Alta` traz alta e crítica.

O `IOcorrenciaApiService` do AdminApp **devolve `null` quando não conseguiu
perguntar — o que é diferente de lista vazia.** Trate os dois: lista vazia é
"não há ocorrência", `null` é "não consegui olhar". Nunca mostre um como o
outro.

---

## 5. Como trabalhar

- **Não invente dado na tela.** Campo `null` aparece como vazio, com o rótulo
  dizendo que está vazio.
- **Tema claro e escuro.** Existem os dois, por variáveis CSS em
  `wwwroot/css/tema.css`, com `[data-tema="escuro"]`. Toda cor nova sai de
  variável, nunca escrita direto. Contraste mínimo WCAG AA: 4,5:1 em texto
  normal, 3:1 em texto grande e em elemento de interface. **Meça, não
  estime.**
- **Cultura pt-BR.** Toda conversão de número e data leva `CultureInfo`
  explícito. Sem isso `"3.00"` vira `300` em pt-BR — aconteceu neste projeto,
  gravou R$ 300,00 no lugar de R$ 3,00, e o teste passava porque o ambiente
  de teste rodava em cultura invariante.
- **Comentário explica POR QUE, não O QUE.** Se o código já diz o que faz, o
  comentário só vale se contar a decisão que ele registra ou o erro que
  evita.
- **Compile antes de entregar:** `dotnet build` de dentro de
  `LOJA AUTÔNOMA PRO/AutonomousStore`. Não me entregue nada que você não viu
  compilar.
- **Não decida escopo sozinho.** Antes de criar arquivo, tabela, tela ou
  dependência que eu não pedi, me pergunte.

---

## 6. O que NÃO é seu

Detecção, classificação e gravação de ocorrência; o `SessionsController`, o
`VisionController` e o `OcorrenciasController`; e **tudo dentro do Agente de
IA** — a rede neural em `Rede-Neural`, o `GerenteService`, o
`LeitorDePeriodo`, o `ClassificadorDeIntencao`.

Se topar com algo disso, **me avise em vez de resolver.** A outra metade pode
estar no mesmo arquivo, e dois editando o mesmo arquivo é conflito garantido.

---

## 7. Ordem sugerida

1. Autenticação de suporte (item 3.5) — entidade, migração, token, controller
2. Sininho (3.1) — é pequeno e você já vê funcionando
3. Painel de notificações (3.2) e botão de suporte (3.3)
4. `SuporteApp` (3.4)

---

## Comece assim

Me diga o que você entendeu, o que faria primeiro, e o que faltou nesta
descrição. **Não escreva código antes de combinarmos.**
