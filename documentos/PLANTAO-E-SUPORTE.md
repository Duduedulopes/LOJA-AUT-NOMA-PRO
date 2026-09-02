# Plantão, ocorrências e suporte técnico — divisão do trabalho

Documento de combinação entre as duas IAs que trabalham no **AutonomousStore**.
Escrito em 01/09/2026.

O sistema tem três partes com nome próprio, e elas não se misturam:

- **AutonomousStore** — a loja autônoma (WebApi, AdminApp, ClientApp, EdgeDesktop).
- **Sistema Espacial SO** — a visão espacial (câmeras alto/frontal/lateral, rastreio de pessoas).
- **Agente de IA** — o gerente virtual, uma rede neural feita do zero (`Rede-Neural`).

---

## 1. O que já existe, e é onde as duas partes encaixam

Antes de propor qualquer coisa, o que foi conferido no código em 01/09/2026:

| Já existe | Onde | Estado |
|---|---|---|
| Barra de plantão com poll de 20s | `AdminApp/Shared/NotificationBar.razor` | **funciona** — pílulas de estoque baixo, sessão aberta, cliente aguardando |
| Alarme de saída por RFID | `WebApi/Controllers/SessionsController.cs` → `VerifyExit` | detecta e **não grava em lugar nenhum** |
| Câmera de prateleira | `WebApi/Controllers/VisionController.cs` → `DetectShelfChange` | detecta e **não grava em lugar nenhum** |
| Pasta de log | `Infrastructure/Logging/` | **vazia** |
| Pasta de serviços | `Infrastructure/Services/` | **vazia** |
| Autenticação de admin | `WebApi/Controllers/AdminAuthController.cs` | funciona (JWT) |
| Tabelas no banco | `Infrastructure/Persistence/AutonomousDbContext.cs` | 8 DbSets, **nenhuma de ocorrência** |

O `NotificationBar` já é o plantão pedido. Falta ele ter o que mostrar.

---

## 2. Furo não é uma coisa só — são três, e só uma é roubo

A distinção que o Eduardo fez está certa e vale separar em três, porque cada
uma tem uma evidência diferente e um dono diferente:

### FURO_DE_COBERTURA — o sistema não viu
O produto saiu da prateleira e nenhuma câmera pegou, ou ele está fora do
alcance das câmeras. O RFID da porta não acusou nada.

- **Evidência:** `DetectShelfChange` devolveu `"nenhuma"` com sessão aberta, ou
  identificou um produto que não está entre os monitorados.
- **Não é crime.** É limitação de instalação.
- **Só aparece na contagem física.** O sistema não sabe o que não viu.
- **Ação:** `APENAS_REGISTRAR`. Vira estatística de onde faltam olhos.

### ROUBO — o RFID viu sair sem pagamento
O leitor da porta identificou a tag saindo e não há compra registrada, ou há
compra sem pagamento confirmado.

- **Evidência:** `VerifyExit` devolveu `IsPaid = false`. **Não é ambíguo** — a
  tag passou pela porta.
- **É a única das três que acusa uma pessoa.** Por isso o registro precisa ser
  frio: o que a leitora leu, quando, qual sessão estava aberta. Nunca uma
  conclusão sobre quem.
- **Ação:** `BLOQUEAR_OPERACAO` + alerta imediato ao administrador.

### FURO_DE_SISTEMA — o software perdeu a conta
Nenhum cliente fez nada errado; o programa é que errou.

- **Evidência hoje, medível sem mudar nada:** toda sessão `Cancelada` que ainda
  tem item. O estoque baixa no `AddItem`, o `RemoveItem` devolve, e **o
  `Cancel` não devolve** (`SessionsController.cs`). O produto está na
  prateleira e o sistema jura que saiu.
- **Ação:** `SUGERIR_CORRECAO` — e, neste caso específico, consertar o `Cancel`.

**Esta terceira é a única que dá para contar hoje**, porque é a única cuja
evidência fica gravada. As outras duas evaporam no momento em que a resposta
HTTP volta. Fazer as duas primeiras existirem é a Parte 1.

---

## 3. As 13 categorias, medidas contra o que o sistema pode provar

O Eduardo listou 13 tipos de problema. Nem todos têm como ser detectados por
um sistema em execução, e uma categoria sem detector cria uma tabela sempre
vazia — que ensina a confiar no que nunca foi conferido. Este projeto já teve
esse erro: o botão de desambiguação diz *"seu clique me ensina"*, o clique é
gravado em `perguntas_reais.jsonl`, e **nenhum programa lê esse arquivo de
volta para o treino**.

| Categoria | Dá para detectar? | Com o quê |
|---|---|---|
| Erros de execução | **sim, com um middleware** | exceção não tratada na WebApi |
| Dados inconsistentes | **sim, hoje** | sessão cancelada com item (o bug do `Cancel`) |
| Contradições entre dados | **sim, hoje** | sessão paga com item que nunca saiu do estoque |
| Operações fora de sequência | **sim, hoje** | `SessionStatus` pulando etapa (`AguardandoEntrada` → `Concluida`) |
| Falhas de API | **sim** — já acontecem e são engolidas | `SegurarAsync` no `GerenteService` engole toda falha em silêncio |
| Falhas de comunicação entre APIs | **sim** | timeout de 2s do monitor do Sistema Espacial |
| Dados duplicados ou ausentes | **sim** | dois produtos com o mesmo código de barras; sessão sem cliente |
| Anomalias estatísticas | **só depois de haver histórico** | precisa de uma linha de base antes de chamar algo de anomalia |
| Falhas de integração entre módulos | **em parte** | contrato quebrado entre WebApi e AdminApp aparece como falha de API |
| Alterações suspeitas | **só com trilha de auditoria** | hoje ninguém grava quem mudou o quê |
| Contradições entre regras de negócio | **não, ainda** | as regras não estão escritas como dado, só como código |
| Erros de lógica | **não** | um programa em execução não sabe que a lógica dele está errada — quem acha isso é teste e revisão |
| Erros sintáticos | **não, e nunca** | o sistema não compilaria. Esta linha nasce morta |

**Sugestão:** `ERRO_SINTATICO` sai da lista de tipos. As duas últimas de baixo
viram trabalho de teste, não de plantão. As oito de cima são a Parte 1.

---

## 4. Autonomia: nada corrige sozinho na versão 1

Das cinco decisões (`CORRIGIR_AUTOMATICAMENTE`, `SUGERIR_CORRECAO`,
`BLOQUEAR_OPERACAO`, `SOLICITAR_APROVACAO`, `APENAS_REGISTRAR`), quatro entram
agora. **`CORRIGIR_AUTOMATICAMENTE` fica no enum e não é usado por nenhum
detector.**

O motivo é medido, não teórico. Neste projeto, `decimal.TryParse` sem
`CultureInfo.InvariantCulture` leu `"3.00"` como `300` em pt-BR: o gerente
mostrou "R$ 3,00" na confirmação e gravou R$ 300,00 no banco, duas vezes
seguidas, com confiança total. O teste passava porque o contêiner roda em
cultura invariante.

Um detector ganha o direito de corrigir sozinho quando tiver **taxa de falso
positivo medida** — igual ao que o gerente já faz com os dois limiares
(`limiar` para responder, `limiar_confirmacao` para gravar, os dois escritos
pelo calibrador dentro do próprio modelo, nunca à mão).

E `CAUSA_RAIZ` é **inferência, não fato**. Vai em campo separado, com o texto
que a justifica, nunca misturada na descrição — senão vira um palpite com cara
de laudo.

---

## 5. O contrato: `Ocorrencia`

**É aqui que as duas partes se encontram.** Quem constrói a tela constrói
contra este registro; quem constrói o detector preenche este registro. Nenhum
dos dois lados muda isto sozinho.

```csharp
namespace AutonomousStore.Domain.Entities;

public enum TipoDeOcorrencia
{
    ErroExecucao = 1,      // exceção não tratada
    ErroDados = 2,         // dado inconsistente, duplicado ou ausente
    Anomalia = 3,          // fora da linha de base
    Contradicao = 4,       // dois dados que não podem ser verdade juntos
    FalhaApi = 5,          // chamada que não voltou, ou voltou erro
    FalhaWorkflow = 6,     // operação fora da sequência esperada
    FalhaIntegracao = 7,   // contrato quebrado entre módulos
    FuroDeCobertura = 8,   // o sistema não viu
    Roubo = 9,             // RFID viu sair sem pagamento
    FuroDeSistema = 10,    // o software perdeu a conta
}

public enum Severidade { Informativa = 1, Baixa = 2, Media = 3, Alta = 4, Critica = 5 }

public enum AcaoRecomendada
{
    ApenasRegistrar = 1,
    SugerirCorrecao = 2,
    SolicitarAprovacao = 3,
    BloquearOperacao = 4,
    CorrigirAutomaticamente = 5,   // reservado — nenhum detector usa na v1
}

public enum EstadoDaOcorrencia { Nova = 1, Vista = 2, EmAnalise = 3, Resolvida = 4, Ignorada = 5, NoSuporte = 6 }

public class Ocorrencia
{
    public Guid Id { get; private set; }

    public DateTime QuandoUtc { get; private set; }
    public string Sistema { get; private set; }        // "AutonomousStore" | "Sistema Espacial SO" | "Agente de IA"
    public string Modulo { get; private set; }         // "SessionsController" | "GerenteService" | ...
    public string Operacao { get; private set; }       // "Cancel" | "VerifyExit" | "DetectShelfChange"

    public TipoDeOcorrencia Tipo { get; private set; }
    public Severidade Severidade { get; private set; }

    public string Descricao { get; private set; }      // o que aconteceu, em português, sem jargão
    public string? DadosEnvolvidosJson { get; private set; }   // ids, valores, o que foi lido
    public string? SequenciaJson { get; private set; }         // as operações que levaram até aqui

    public string? CausaProvavel { get; private set; }  // INFERÊNCIA — sempre marcada como tal
    public string? CausaRaiz { get; private set; }      // só quando confirmada por alguém
    public string? Impacto { get; private set; }        // em unidades e em reais, quando der

    public AcaoRecomendada Recomendacao { get; private set; }
    public string? AcaoExecutada { get; private set; }  // null enquanto nada foi feito
    public string? Resultado { get; private set; }

    public EstadoDaOcorrencia Estado { get; private set; }
    public Guid CorrelationId { get; private set; }     // amarra as ocorrências de uma mesma sessão/pedido

    public DateTime? VistaEm { get; private set; }
    public DateTime? ResolvidaEm { get; private set; }
    public string? ResolvidaPor { get; private set; }
    public string? NotaDoAdmin { get; private set; }
}
```

### Endpoints (Parte 1 entrega, Parte 2 consome)

```
GET    /api/ocorrencias?desde=&ate=&tipo=&severidade=&estado=&correlationId=
GET    /api/ocorrencias/nao-vistas         -> { total, criticas, maisRecente }
GET    /api/ocorrencias/{id}
POST   /api/ocorrencias/{id}/vista
POST   /api/ocorrencias/{id}/resolver      { nota }
POST   /api/ocorrencias/{id}/suporte       { descricaoDoAdmin }   -> abre chamado
GET    /api/ocorrencias/resumo?periodo=    -> contagem por tipo e severidade
```

**Enquanto a Parte 1 não estiver pronta**, a Parte 2 constrói contra um stub
que devolve ocorrências de mentira nesse mesmo formato. Nada trava esperando.

---

## 6. A divisão

### PARTE 1 — detectar, classificar, gravar *(fica comigo)*

1. `Ocorrencia` (entidade, enums, `DbSet`, migração, `IOcorrenciaRepository`).
2. `OcorrenciasController` com os endpoints acima.
3. Os detectores, **só os que têm evidência**:
   - `Cancel` com carrinho cheio → `FuroDeSistema`, `SugerirCorrecao`
   - `VerifyExit` com `IsPaid = false` → `Roubo`, `BloquearOperacao`, severidade `Critica`
   - `DetectShelfChange` sem detecção, ou produto fora do catálogo → `FuroDeCobertura`
   - `SessionStatus` fora de sequência → `FalhaWorkflow`
   - exceção não tratada na WebApi → `ErroExecucao`
   - falha de chamada que hoje o `SegurarAsync` engole → `FalhaApi`
   - código de barras duplicado, sessão sem cliente → `ErroDados`
4. Conserto do `Cancel` (devolver estoque), com prova.
5. O gerente de plantão: `furo_sistema` passa a ler ocorrências gravadas em vez
   de inferir na hora, e responde com números de verdade.
6. Prova em `Rede-Neural/provas/Casos.cs`, nas duas culturas — pt-BR e
   invariante. Data e número são armadilha de cultura.

### PARTE 2 — mostrar, avisar, atender *(a outra IA)*

1. **Sininho no AdminApp** — estende `NotificationBar.razor`, que já faz poll de
   20s. Contador de não vistas, vermelho quando houver `Critica`.
2. **Painel de notificações** — lista, filtro, detalhe, marcar como vista,
   resolver com nota.
3. **Botão "chamar suporte técnico"** — no detalhe da ocorrência, abre chamado
   com o que o admin já tentou.
4. **`AutonomousStore.SuporteApp`** — aplicação Blazor nova na solução:
   histórico completo, filtros, rastro por `CorrelationId`, e o chat de suporte.
5. **Autenticação própria do suporte.** O app de suporte enxerga tudo de todas
   as lojas; não pode entrar com a credencial de admin de uma loja.

---

## 7. Prompt para a outra IA

O texto abaixo é para colar inteiro.

---

> Você vai trabalhar no **AutonomousStore**, um sistema de loja autônoma em
> .NET 8 / Blazor WebAssembly. Fale português comigo.
>
> **Nomes que não mudam:** o sistema da loja é **AutonomousStore**; a visão
> computacional é o **Sistema Espacial SO**; o gerente virtual é o **Agente de
> IA**. Nunca use outro nome para nenhum dos três.
>
> **Sua tarefa é a PARTE 2 de um trabalho dividido em duas.** A Parte 1 (a
> outra IA) constrói a detecção e a gravação de ocorrências no servidor. Você
> constrói o que MOSTRA e o que ATENDE. O contrato entre as duas partes é a
> entidade `Ocorrencia` e sete endpoints, e está fechado — se você precisar
> mudar alguma coisa nele, **pare e me pergunte** em vez de mudar por conta.
>
> ### O contrato
>
> [colar aqui a seção 5 deste documento — a classe `Ocorrencia`, os quatro
> enums e a lista de endpoints]
>
> ### O que você constrói
>
> **1. Sininho de alerta no AdminApp.**
> Já existe `AutonomousStore.AdminApp/Shared/NotificationBar.razor`, com poll
> de 20 segundos e pílulas de aviso. **Estenda esse componente, não crie
> outro.** Adicione um sino com o contador de `/api/ocorrencias/nao-vistas`.
> Vermelho quando houver severidade `Critica`, âmbar para `Alta`, discreto no
> resto. Clicar abre o painel.
>
> **2. Painel de notificações no AdminApp.**
> Lista com filtro por tipo, severidade, estado e período. Detalhe de uma
> ocorrência mostrando todos os campos, com `DadosEnvolvidosJson` e
> `SequenciaJson` formatados de forma legível. Ações: marcar como vista,
> resolver com nota, chamar o suporte.
>
> **`CausaProvavel` é inferência e tem de aparecer como tal na tela** — nunca
> com a mesma cara de `Descricao`, que é fato observado. Um palpite exibido
> como laudo faz o admin agir com uma certeza que ninguém mediu.
>
> **3. Botão "chamar suporte técnico".**
> No detalhe da ocorrência. Abre um chamado com o que o admin já tentou. Chama
> `POST /api/ocorrencias/{id}/suporte`.
>
> **4. `AutonomousStore.SuporteApp`** — projeto Blazor WebAssembly novo na
> solução, ao lado de `AutonomousStore.AdminApp`. Histórico completo de
> ocorrências, filtros, rastro por `CorrelationId` (todas as ocorrências de uma
> mesma sessão em ordem), e o chat de suporte entre admin e técnico.
>
> **5. Autenticação própria.** O SuporteApp enxerga dados de todas as lojas.
> Existe `AdminAuthController` com JWT para o admin; o suporte precisa de
> credencial e papel separados. **Não reaproveite o login de admin.**
>
> ### Como trabalhar
>
> - **Enquanto a Parte 1 não existir**, construa contra um stub que devolva
>   ocorrências de mentira no formato do contrato. Deixe o stub num arquivo só,
>   fácil de trocar por `HttpClient` depois.
> - **Não invente dado na tela.** Se um campo vier `null`, mostre que está
>   vazio. Um "tudo certo" que na verdade quer dizer "não tem o que olhar" é
>   pior que não responder.
> - **Tema claro e escuro.** O sistema tem os dois, por variáveis CSS em
>   `wwwroot/css/tema.css`, com `[data-tema="escuro"]`. Toda cor nova sai de
>   variável. Contraste mínimo WCAG AA: 4,5:1 em texto normal, 3:1 em texto
>   grande e em elemento de interface. Meça, não estime.
> - **Cultura pt-BR.** Toda conversão de número e data leva
>   `CultureInfo` explícito. Sem isso, `"3.00"` vira `300` em pt-BR — já
>   aconteceu neste projeto e gravou R$ 300,00 no lugar de R$ 3,00.
> - **Comentário explica POR QUE, não O QUE.** Se o código já diz o que faz, o
>   comentário só vale se contar a decisão ou o erro que ele evita.
> - **Não decida escopo sozinho.** Antes de criar arquivo, tabela, tela ou
>   dependência que eu não pedi, me pergunte.
>
> ### O que NÃO é seu
>
> Detecção, classificação, gravação, migração de banco, conserto do `Cancel` e
> qualquer coisa dentro do `Agente de IA` (a rede neural). Se topar com algo
> disso, **me avise em vez de resolver** — a outra metade pode já estar
> mexendo no mesmo arquivo.
>
> Comece me dizendo o que entendeu e o que você faria primeiro. Não escreva
> código antes de combinarmos.

---

## 8. O que ainda não foi decidido

- **Onde o alerta chega quando o admin não está com o painel aberto.** Sininho
  só funciona com a tela aberta. Push, e-mail e WhatsApp são cada um uma
  decisão sua — e a WebApi já tem `SenderPassword` configurado para e-mail.
- **Quanto tempo a ocorrência fica guardada.** Uma tabela que só cresce fica
  lenta e cara. Vale combinar um prazo por severidade.
- **A linha de base para `Anomalia`.** Antes de chamar algo de anômalo é
  preciso ter o normal medido. Sem histórico, essa categoria fica parada.
