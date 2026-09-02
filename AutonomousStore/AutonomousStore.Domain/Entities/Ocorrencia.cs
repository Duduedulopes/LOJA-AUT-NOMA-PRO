using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Uma coisa errada que o sistema percebeu, guardada.
/// </summary>
/// <remarks>
/// POR QUE ISTO PRECISA EXISTIR.
///
/// O sistema ja detectava e ja esquecia. O `VerifyExit` le a tag na porta,
/// descobre que o produto esta saindo sem pagamento, devolve "ALARME" para a
/// leitora — e acaba ali. O `DetectShelfChange` percebe que o Gemini viu um
/// produto que nao esta no catalogo e devolve a mensagem — e acaba ali.
/// Nenhum dos dois grava.
///
/// Enquanto o alarme evapora com a resposta HTTP, a pergunta "tivemos algum
/// furo?" nao tem como ser respondida com verdade: o gerente teria de
/// inventar. Esta tabela e o que transforma um alarme em algo que se pode
/// contar, comparar e cobrar.
///
/// DOIS RELOGIOS, DE PROPOSITO. `CreatedAt` (da base `Entity`) e quando a
/// LINHA foi criada. `QuandoUtc` e quando o FATO aconteceu. Quase sempre sao
/// o mesmo instante, mas um detector que varre historico — como o do
/// `Cancel` — registra um fato de ontem hoje. Misturar os dois faria o
/// relatorio de ontem mudar de tamanho conforme o dia em que alguem rodasse
/// a varredura.
///
/// TUDO EM UTC. Guardar hora local e como guardar preco sem moeda.
/// </remarks>
public class Ocorrencia : Entity
{
    /// <summary>Quando o FATO aconteceu (nao quando a linha foi criada).</summary>
    public DateTime QuandoUtc { get; private set; }

    /// <summary>"AutonomousStore", "Sistema Espacial SO" ou "Agente de IA".</summary>
    public string Sistema { get; private set; } = "";

    /// <summary>Onde: "SessionsController", "GerenteService", ...</summary>
    public string Modulo { get; private set; } = "";

    /// <summary>O que estava sendo feito: "Cancel", "VerifyExit", ...</summary>
    public string Operacao { get; private set; } = "";

    public TipoDeOcorrencia Tipo { get; private set; }
    public Severidade Severidade { get; private set; }

    /// <summary>
    /// O que aconteceu, em portugues, sem jargao. FATO OBSERVADO — nunca
    /// palpite; palpite mora em <see cref="CausaProvavel"/>.
    /// </summary>
    public string Descricao { get; private set; } = "";

    /// <summary>Ids, valores, o que foi lido. JSON.</summary>
    public string? DadosEnvolvidosJson { get; private set; }

    /// <summary>As operacoes que levaram ate aqui. JSON.</summary>
    public string? SequenciaJson { get; private set; }

    /// <summary>
    /// INFERENCIA, e por isso mora em campo separado.
    /// </summary>
    /// <remarks>
    /// Um palpite dentro da `Descricao` fica com a mesma cara de fato
    /// observado, e ai o Chefe age com uma certeza que ninguem mediu. A tela
    /// tambem tem de mostrar os dois diferente — esta separacao aqui nao
    /// serve de nada se do outro lado os dois virarem o mesmo paragrafo.
    /// </remarks>
    public string? CausaProvavel { get; private set; }

    /// <summary>So depois que alguem CONFIRMOU. Nao e preenchida por detector.</summary>
    public string? CausaRaiz { get; private set; }

    /// <summary>Em unidades e em reais, quando der para calcular.</summary>
    public string? Impacto { get; private set; }

    public AcaoRecomendada Recomendacao { get; private set; }

    /// <summary>Nulo enquanto nada foi feito. Detector nao preenche.</summary>
    public string? AcaoExecutada { get; private set; }

    public string? Resultado { get; private set; }

    public EstadoDaOcorrencia Estado { get; private set; }

    /// <summary>
    /// Amarra as ocorrencias de uma mesma sessao ou pedido.
    /// </summary>
    /// <remarks>
    /// Uma sessao que deu errado costuma gerar tres ou quatro ocorrencias em
    /// modulos diferentes. Sem isto, cada uma parece um problema solto, e o
    /// suporte investiga quatro vezes o mesmo caso.
    /// </remarks>
    public Guid CorrelationId { get; private set; }

    public DateTime? VistaEm { get; private set; }
    public DateTime? ResolvidaEm { get; private set; }
    public string? ResolvidaPor { get; private set; }
    public string? NotaDoAdmin { get; private set; }

    /// <summary>
    /// Identifica O FATO, para nao gravar o mesmo duas vezes. Interna: nao
    /// sai no JSON da API.
    /// </summary>
    /// <remarks>
    /// O varredor do `Cancel` roda a cada consulta. Sem esta chave, a mesma
    /// sessao cancelada viraria uma linha nova toda vez que alguem
    /// perguntasse "tivemos algum furo?", e o sino acusaria cem alertas que
    /// sao um so — que e o jeito mais rapido de ensinar o Chefe a ignorar o
    /// sino.
    ///
    /// Formato: "assunto:id". Ex.: "sessao-cancelada:{guid}",
    /// "saida-sem-pagamento:{tag}:{ticks}".
    /// </remarks>
    public string? Chave { get; private set; }

    /// <summary>Quantas vezes este MESMO fato ja aconteceu.</summary>
    /// <remarks>
    /// UM ERRO EM LOOP NAO PODE ENTERRAR O RESTO DO HISTORICO.
    ///
    /// Um defeito numa renderizacao dispara cem vezes por minuto. Cem linhas
    /// iguais empurram para fora da tela tudo o que veio antes, e o suporte
    /// para de abrir a lista — que e o mesmo que nao ter lista.
    ///
    /// Entao o mesmo fato vira UMA linha que conta. E a contagem nao e so
    /// arrumacao: "aconteceu 1 vez" e "aconteceu 340 vezes desde ontem" sao
    /// diagnosticos diferentes do mesmo erro, e a segunda informacao so
    /// existe porque alguem somou.
    /// </remarks>
    public int VezesVistas { get; private set; } = 1;

    /// <summary>A ultima vez que este fato se repetiu. Nulo se aconteceu uma vez so.</summary>
    /// <remarks>
    /// `QuandoUtc` continua sendo a PRIMEIRA vez, de proposito: e a que
    /// responde "desde quando isto esta acontecendo?". Sem as duas pontas, um
    /// erro que parou ontem e um que ainda esta acontecendo agora ficam com a
    /// mesma cara na lista.
    /// </remarks>
    public DateTime? UltimaVezUtc { get; private set; }

    /// <summary>
    /// O e-mail de quem ABRIU o chamado. Nulo em tudo que veio de detector.
    /// </summary>
    /// <remarks>
    /// É o que responde "posso ver esta conversa?". Admin e suporte veem
    /// todas; qualquer outra pessoa só vê aquelas em que este campo é o
    /// e-mail dela. Sem isto, ou a conversa seria pública, ou o cliente não
    /// conseguiria ler a resposta do próprio pedido.
    /// </remarks>
    public string? AbertoPor { get; private set; }

    private readonly List<MensagemDeSuporte> _mensagens = new();

    /// <summary>A conversa deste chamado, da mais antiga para a mais nova.</summary>
    public IReadOnlyCollection<MensagemDeSuporte> Mensagens => _mensagens;

    /// <summary>Para o EF.</summary>
    protected Ocorrencia() { }

    public Ocorrencia(
        DateTime quandoUtc,
        string sistema,
        string modulo,
        string operacao,
        TipoDeOcorrencia tipo,
        Severidade severidade,
        string descricao,
        AcaoRecomendada recomendacao,
        Guid? correlationId = null,
        string? dadosEnvolvidosJson = null,
        string? sequenciaJson = null,
        string? causaProvavel = null,
        string? impacto = null,
        string? chave = null)
    {
        if (string.IsNullOrWhiteSpace(sistema))
            throw new ArgumentException("A ocorrência precisa dizer de qual sistema veio.", nameof(sistema));
        if (string.IsNullOrWhiteSpace(modulo))
            throw new ArgumentException("A ocorrência precisa dizer de qual módulo veio.", nameof(modulo));
        if (string.IsNullOrWhiteSpace(operacao))
            throw new ArgumentException("A ocorrência precisa dizer qual operação estava rodando.", nameof(operacao));
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Uma ocorrência sem descrição não serve para nada.", nameof(descricao));

        // NENHUM DETECTOR CORRIGE SOZINHO NA VERSAO 1. A trava fica aqui, no
        // dominio, e nao na boa vontade de quem escreve o detector: e uma
        // linha para tirar quando houver taxa de falso positivo medida, e ate
        // la ninguem consegue passar por engano.
        if (recomendacao == AcaoRecomendada.CorrigirAutomaticamente)
            throw new ArgumentException(
                "CorrigirAutomaticamente está reservado: nenhum detector tem taxa de falso " +
                "positivo medida ainda. Use SugerirCorrecao ou SolicitarAprovacao.",
                nameof(recomendacao));

        QuandoUtc = quandoUtc.Kind == DateTimeKind.Utc
            ? quandoUtc
            : quandoUtc.ToUniversalTime();

        Sistema = sistema;
        Modulo = modulo;
        Operacao = operacao;
        Tipo = tipo;
        Severidade = severidade;
        Descricao = descricao;
        Recomendacao = recomendacao;
        CorrelationId = correlationId ?? Guid.NewGuid();
        DadosEnvolvidosJson = dadosEnvolvidosJson;
        SequenciaJson = sequenciaJson;
        CausaProvavel = causaProvavel;
        Impacto = impacto;
        Chave = chave;
        Estado = EstadoDaOcorrencia.Nova;
    }

    public void MarcarVista()
    {
        // Ja resolvida nao volta a ser "vista": o estado so anda para frente.
        if (Estado != EstadoDaOcorrencia.Nova) return;
        Estado = EstadoDaOcorrencia.Vista;
        VistaEm = DateTime.UtcNow;
    }

    public void EmAnalise()
    {
        if (Estado is EstadoDaOcorrencia.Resolvida or EstadoDaOcorrencia.Ignorada) return;
        Estado = EstadoDaOcorrencia.EmAnalise;
        VistaEm ??= DateTime.UtcNow;
    }

    public void Resolver(string? quem, string? nota, string? resultado = null)
    {
        Estado = EstadoDaOcorrencia.Resolvida;
        ResolvidaEm = DateTime.UtcNow;
        ResolvidaPor = quem;
        NotaDoAdmin = nota;
        Resultado = resultado;
        VistaEm ??= DateTime.UtcNow;
    }

    public void Ignorar(string? quem, string? nota)
    {
        Estado = EstadoDaOcorrencia.Ignorada;
        ResolvidaEm = DateTime.UtcNow;
        ResolvidaPor = quem;
        NotaDoAdmin = nota;
        VistaEm ??= DateTime.UtcNow;
    }

    public void EnviarAoSuporte(string? descricaoDoAdmin)
    {
        Estado = EstadoDaOcorrencia.NoSuporte;
        NotaDoAdmin = descricaoDoAdmin;
        VistaEm ??= DateTime.UtcNow;
    }

    /// <summary>Marca de quem é este chamado. Só o pedido ao suporte usa.</summary>
    public void AbertoPorAlguem(string email) => AbertoPor = email?.Trim();

    /// <summary>Acrescenta uma fala à conversa deste chamado.</summary>
    /// <remarks>
    /// O ESTADO ANDA COM A CONVERSA, SEM NINGUEM PRECISAR LEMBRAR DE MEXER.
    ///
    /// Se o tecnico responde, alguem esta cuidando: vira `EmAnalise`. Se a
    /// pessoa escreve num chamado ja dado como resolvido, ele nao estava
    /// resolvido: volta para `NoSuporte`.
    ///
    /// Deixar isso a cargo de quem chama seria garantir que um dia um chamado
    /// ficaria parado em "Nova" com quatro respostas dentro — e a fila
    /// deixaria de dizer a verdade sobre o que precisa de gente.
    /// </remarks>
    public MensagemDeSuporte AdicionarMensagem(
        AutorDaMensagem autor, string quemNome, string? quemEmail, string texto, DateTime quandoUtc)
    {
        var m = new MensagemDeSuporte(Id, autor, quemNome, quemEmail, texto, quandoUtc);
        _mensagens.Add(m);

        if (autor == AutorDaMensagem.Suporte)
        {
            if (Estado is EstadoDaOcorrencia.Nova or EstadoDaOcorrencia.Vista or EstadoDaOcorrencia.NoSuporte)
                Estado = EstadoDaOcorrencia.EmAnalise;
            VistaEm ??= DateTime.UtcNow;
        }
        else if (Estado is EstadoDaOcorrencia.Resolvida or EstadoDaOcorrencia.Ignorada)
        {
            Estado = EstadoDaOcorrencia.NoSuporte;
        }

        return m;
    }

    /// <summary>Quem pode ler e escrever nesta conversa.</summary>
    /// <remarks>
    /// A regra mora aqui, e nao no controlador, por um motivo simples: se
    /// morasse la, a segunda rota que mexesse em mensagem teria de repetir a
    /// mesma condicao — e no dia em que as duas discordassem, a que estivesse
    /// errada seria a que vaza.
    /// </remarks>
    public bool PodeConversar(string? email, bool ehDaCasa)
        => ehDaCasa
           || (AbertoPor is { Length: > 0 } dono
               && email is { Length: > 0 }
               && string.Equals(dono, email.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>O mesmo fato aconteceu de novo: soma em vez de virar linha nova.</summary>
    /// <remarks>
    /// REPETIR DEPOIS DE RESOLVIDO REABRE. IGNORADO CONTINUA IGNORADO.
    ///
    /// Sao duas intencoes diferentes, e tratar as duas igual erra dos dois
    /// lados. "Resolvida" e uma afirmacao sobre o mundo — o problema acabou.
    /// Se ele voltou, a afirmacao estava errada, e esconder isso e o pior que
    /// esta tabela poderia fazer: o Chefe acharia que consertou.
    ///
    /// "Ignorada" e uma afirmacao sobre a ATENCAO dele — nao me avise disto.
    /// Reabrir o que ele mandou ignorar seria desobedecer, e em uma semana ele
    /// desligaria o sino.
    ///
    /// `ResolvidaEm` e `ResolvidaPor` ficam onde estao mesmo na reabertura:
    /// "foi dado como resolvido no dia X por fulano E VOLTOU" e a historia
    /// inteira. Apagar quem resolveu apagaria a metade util dela.
    /// </remarks>
    public void RegistrarRepeticao(DateTime quandoUtc)
    {
        VezesVistas++;
        UltimaVezUtc = quandoUtc.Kind == DateTimeKind.Utc
            ? quandoUtc
            : quandoUtc.ToUniversalTime();

        if (Estado == EstadoDaOcorrencia.Resolvida)
            Estado = EstadoDaOcorrencia.Nova;
    }

    /// <summary>Registra o que foi feito e o que deu.</summary>
    public void RegistrarAcao(string acaoExecutada, string? resultado)
    {
        AcaoExecutada = acaoExecutada;
        Resultado = resultado;
    }

    /// <summary>
    /// A causa-raiz so entra depois de CONFIRMADA por alguem — por isso e
    /// metodo, e nao parametro de construtor: detector nenhum a preenche.
    /// </summary>
    public void ConfirmarCausaRaiz(string causaRaiz) => CausaRaiz = causaRaiz;
}
