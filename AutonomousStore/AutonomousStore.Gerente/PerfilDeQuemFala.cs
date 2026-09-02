namespace AutonomousStore.Gerente;

/// <summary>
/// Com quem o gerente está falando — e o que ele pode dizer a essa pessoa.
/// </summary>
/// <remarks>
/// O MESMO CÉREBRO, DUAS CONVERSAS DIFERENTES.
///
/// O gerente sabe responder 42 coisas. Doze são do mundo do comprador — as do
/// conjunto `DoCliente` logo abaixo: preço, o que tem na prateleira, o carrinho
/// dele, como a loja funciona. As outras trinta são da administração —
/// faturamento, furos de sistema, quantas pessoas estão no chão, as câmeras — e
/// algumas MUDAM o banco.
///
/// (Este parágrafo já disse "umas oito" e "as outras trinta e quatro" enquanto
/// o conjunto tinha doze. Número escrito na prosa não acompanha a lista que ele
/// descreve; quem for mexer no `DoCliente` conte de novo aqui.)
///
/// Sem este arquivo, o cliente que perguntasse "quanto vocês faturaram hoje?"
/// receberia a resposta. Não por falha de segurança da API: o gerente roda no
/// navegador dele e simplesmente não teria como saber que não devia.
///
/// LISTA DE PERMISSÃO, NÃO DE BLOQUEIO. A diferença decide o futuro: com
/// lista de bloqueio, toda intenção nova que a gente treinar nasce LIBERADA
/// para o cliente, e alguém precisa lembrar de bloquear. Com lista de
/// permissão, nasce fechada. Errar esquecendo é inevitável; o que se escolhe
/// aqui é para que lado o esquecimento erra.
/// </remarks>
public sealed record PerfilDeQuemFala(
    string Tratamento,
    IReadOnlySet<string> Permitidas,
    bool PodeEscrever,
    string Origem,
    Guid? ClienteId = null)
{
    /// <summary>Conjunto vazio significa "tudo". O Chefe não tem lista.</summary>
    /// <remarks>
    /// DECLARADO ANTES DO `Chefe`, e isso não é estilo. Campo estático
    /// inicializa na ordem em que está escrito: com a declaração embaixo, o
    /// `Chefe` receberia `null` e a lista de permissão dele nasceria vazia.
    /// O compilador só avisa; em execução seria um null silencioso.
    /// </remarks>
    private static readonly IReadOnlySet<string> TudoLiberado = new HashSet<string>();

    /// <summary>O dono da loja: vê tudo e pode mandar mudar.</summary>
    public static readonly PerfilDeQuemFala Chefe =
        new("Chefe", TudoLiberado, PodeEscrever: true, Origem: "AdminApp");

    /// <summary>
    /// O comprador: só o que é do mundo dele, e nada que escreva.
    /// </summary>
    /// <remarks>
    /// Tratado pelo PRIMEIRO NOME. "Chefe" dito a um cliente soa a
    /// atendimento de vendedor insistente; o nome dele é o que a loja já
    /// sabe e o que ele espera ouvir.
    /// </remarks>
    /// <param name="id">
    /// Quem ele é, não só como se chama. Sem isto, "o que tem no meu
    /// carrinho?" só sabia perguntar pelo carrinho da sessão aberta da LOJA
    /// — que é de quem estiver comprando naquele instante, e não dele.
    /// </param>
    public static PerfilDeQuemFala Cliente(string? nome, Guid? id = null) =>
        new(PrimeiroNome(nome), DoCliente, PodeEscrever: false, Origem: "ClientApp", ClienteId: id);

    public bool Pode(string intencao) =>
        Permitidas.Count == 0 || Permitidas.Contains(intencao);

    /// <summary>O que o gerente responde quando a pergunta não é para esta pessoa.</summary>
    /// <remarks>
    /// NÃO DIZ O QUE ELE SABE E NÃO VAI CONTAR. "Não posso te falar do
    /// faturamento" já entrega que existe um número de faturamento ali
    /// dentro. A recusa é curta e aponta o caminho de quem pode ajudar.
    /// </remarks>
    public string Recusa() =>
        $"{Tratamento}, isso eu não consigo te responder por aqui — é coisa da administração da loja. "
        + "Se você precisa de ajuda com uma compra ou com o app, o suporte responde: "
        + "é só abrir um chamado em **Suporte**, no menu de cima.";

    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// As oito que são do comprador. Nada aqui lê dinheiro, câmera, pessoa
    /// no chão ou falha do sistema, e nada aqui escreve.
    /// </summary>
    /// <remarks>
    /// `estoque` e `listar_produtos` saem do `GET /api/products`, que é
    /// anônimo e é o mesmo dado que ele vê andando pela loja — negar isso
    /// seria esconder a prateleira de quem está na frente dela.
    ///
    /// `faturamento`, `mais_vendidos` e `relatorio_periodo` ficam de fora
    /// mesmo sendo "só consulta": quanto a loja vende é informação do dono,
    /// não do freguês.
    /// </remarks>
    private static readonly IReadOnlySet<string> DoCliente = new HashSet<string>
    {
        "saudacao",
        "agradecimento",
        "ajuda",
        "duvida_sistema",
        "preco",
        "listar_produtos",
        "estoque",
        "pagamento",
        "entrada_loja",
        "meu_carrinho",
        "carrinho",
        "fora_de_escopo",
    };

    /// <summary>"Maria Eduarda Souza" → "Maria".</summary>
    /// <remarks>
    /// Nome inteiro numa conversa soa a cobrança de banco. E cai para
    /// "Olá" em vez de para uma string vazia: um gerente que começa a frase
    /// com vírgula é pior que um que não usa o nome.
    /// </remarks>
    private static string PrimeiroNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "Olá";
        var primeiro = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return primeiro.Length is > 1 and <= 20 ? primeiro : "Olá";
    }
}
