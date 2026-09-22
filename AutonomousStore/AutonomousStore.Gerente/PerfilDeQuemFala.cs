namespace AutonomousStore.Gerente;

/// <summary>
/// Com quem o gerente está falando — e o que ele pode dizer a essa pessoa.
/// </summary>
/// <remarks>
/// O MESMO CÉREBRO, QUATRO CONVERSAS DIFERENTES.
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
/// AS QUATRO: o Chefe (dono da loja, no AdminApp), o Cliente (comprador, no
/// ClientApp), o Técnico (suporte da plataforma, no SuporteApp) e o Criador
/// (dono da plataforma, no CriadorApp). Os dois últimos são de QUEM OPERA A
/// PLATAFORMA e enxergam várias empresas de uma vez — por isso nenhum dos dois
/// herda a lista do Chefe: "quanto faturamos hoje?" respondido a quem vê todas
/// as empresas somaria o dinheiro de empresas diferentes, e o técnico não tem
/// por que ler o faturamento de ninguém.
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
    Guid? ClienteId = null,
    TipoDePerfil Tipo = TipoDePerfil.Chefe)
{
    /// <summary>Conjunto vazio significa "tudo". O Chefe não tem lista.</summary>
    private static readonly IReadOnlySet<string> TudoLiberado = new HashSet<string>();

    // ── TODOS SÃO CHAMADOS PELO PRIMEIRO NOME ────────────────────────────
    //
    // Chefe, comprador, técnico e Criador: quem fala com o gerente é tratado pelo nome que tem, e nunca por um título.
    // O gerente já chamou o dono da loja (e o Criador) de "Chefe"; a decisão do Eduardo foi chamar todos pelo nome,
    // indiferente de quem seja. Sem nome — ou com um que não dá para usar —, cai em "Olá" (ver PrimeiroNome).

    /// <summary>O dono da loja (o Admin): vê tudo e pode mandar mudar.</summary>
    public static PerfilDeQuemFala Chefe(string? nome) =>
        new(PrimeiroNome(nome), TudoLiberado, PodeEscrever: true, Origem: "AdminApp", Tipo: TipoDePerfil.Chefe);

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
        new(PrimeiroNome(nome), DoCliente, PodeEscrever: false, Origem: "ClientApp", ClienteId: id,
            Tipo: TipoDePerfil.Cliente);

    /// <summary>O técnico de suporte da plataforma: o lado técnico do sistema, e nada de negócio.</summary>
    /// <remarks>
    /// Tratado pelo primeiro nome, como todos.
    /// Não escreve, e não lê dinheiro, estoque nem cadastro de pessoa — o técnico atende
    /// TODAS as empresas, e uma resposta sobre "a loja" misturaria as de todas elas.
    /// </remarks>
    public static PerfilDeQuemFala Tecnico(string? nome) =>
        new(PrimeiroNome(nome), DoSuporteTecnico, PodeEscrever: false, Origem: "SuporteApp",
            Tipo: TipoDePerfil.Tecnico);

    /// <summary>O dono da plataforma (o Criador): tratado pelo primeiro nome, como todos.</summary>
    /// <remarks>
    /// POR ENQUANTO ELE ENXERGA O MESMO QUE O TÉCNICO. Nada de dinheiro nem de escrita: com
    /// várias empresas, "o faturamento" não é uma pergunta com resposta única. O que é dele —
    /// quantas empresas, quais suspensas, o que o suporte fez — são intenções NOVAS, que a rede
    /// ainda não conhece: precisam de corpus, treino e medição no Rede-Neural, e entram junto com
    /// o CriadorApp. Até lá, a lista dele só cresce por decisão, nunca por esquecimento.
    /// </remarks>
    public static PerfilDeQuemFala Criador(string? nome) =>
        new(PrimeiroNome(nome), DoSuporteTecnico, PodeEscrever: false, Origem: "CriadorApp",
            Tipo: TipoDePerfil.Criador);

    public bool EhCliente => Tipo == TipoDePerfil.Cliente;

    /// <summary>Quem opera a plataforma (técnico e Criador): enxerga várias empresas.</summary>
    public bool EhDaPlataforma => Tipo is TipoDePerfil.Tecnico or TipoDePerfil.Criador;

    public bool Pode(string intencao) =>
        Permitidas.Count == 0 || Permitidas.Contains(intencao);

    /// <summary>O que o gerente responde quando a pergunta não é para esta pessoa.</summary>
    /// <remarks>
    /// NÃO DIZ O QUE ELE SABE E NÃO VAI CONTAR. "Não posso te falar do
    /// faturamento" já entrega que existe um número de faturamento ali
    /// dentro. A recusa é curta e aponta o caminho de quem pode ajudar.
    /// </remarks>
    public string Recusa() => EhDaPlataforma
        ? $"{Tratamento}, isso eu não consigo te responder por aqui — não é da parte técnica. "
        + "Posso ajudar com o estado do sistema, as ocorrências e como as peças funcionam."
        : $"{Tratamento}, isso eu não consigo te responder por aqui — é coisa da administração da loja. "
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

    /// <summary>
    /// O que o suporte técnico olha: se a API responde, onde ficam os logs, se teve furo de
    /// sistema, e como o sistema funciona — mais a conversa de sempre. Nada que leia dinheiro,
    /// estoque, carrinho ou cadastro de pessoa, e nada que escreva.
    /// </summary>
    /// <remarks>
    /// Só entra o que TEM tratamento no `GerenteService`. `diagnostico_problema` é uma classe
    /// da rede, mas não tem quem a atenda (cai na ajuda): liberá-la seria prometer o que não existe.
    /// </remarks>
    private static readonly IReadOnlySet<string> DoSuporteTecnico = new HashSet<string>
    {
        "saudacao",
        "agradecimento",
        "ajuda",
        "duvida_sistema",
        "fora_de_escopo",
        "status_api",
        "logs_sistema",
        "furo_sistema",
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

/// <summary>
/// Com quem o gerente fala, em quatro palavras. Existe porque "pode escrever?" não diz quem
/// é a pessoa: o técnico e o comprador NÃO escrevem, e nem por isso os dois recebem o mesmo texto.
/// </summary>
public enum TipoDePerfil
{
    /// <summary>O dono da loja, no AdminApp.</summary>
    Chefe,

    /// <summary>O comprador, no ClientApp.</summary>
    Cliente,

    /// <summary>O suporte da plataforma, no SuporteApp.</summary>
    Tecnico,

    /// <summary>O dono da plataforma, no CriadorApp.</summary>
    Criador,
}
