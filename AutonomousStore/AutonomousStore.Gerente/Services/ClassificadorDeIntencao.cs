using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace AutonomousStore.Gerente.Services;

/// <summary>O modelo treinado, como ele sai do Python.</summary>
public record ModeloIntencao(
    [property: JsonPropertyName("intencoes")] List<string> Intencoes,
    [property: JsonPropertyName("pecas")] List<string> Pecas,
    [property: JsonPropertyName("dimensao")] int Dimensao,
    [property: JsonPropertyName("tabela")] List<List<double>> Tabela,
    [property: JsonPropertyName("camadas")] List<CamadaJson> Camadas,
    [property: JsonPropertyName("limiar")] double Limiar,

    /// <summary>O corte mais alto que o "sim" precisa passar para GRAVAR.</summary>
    /// <remarks>
    /// DUAS DECISOES DIFERENTES NAO COMPARTILHAM NUMERO.
    ///
    /// `Limiar` (0,95) responde "posso responder esta pergunta?". Errar ali
    /// mostra um numero errado, e a pessoa confere.
    ///
    /// Este responde "posso ESCREVER no banco?". Errar aqui grava uma
    /// alteracao que o Chefe recusou. Nao valem o mesmo, entao nao usam o
    /// mesmo corte.
    ///
    /// Medido no modelo atual, em 34 frases de controle que NAO sao "sim":
    ///
    ///     no limiar geral (0,95)   2 falsos:
    ///         "para por favor"  -> confirmar_acao  99,4%   (quer dizer PARE)
    ///         "refaz por favor" -> confirmar_acao  99,1%   (quer dizer REFACA)
    ///     em 0,995                 nenhum falso, e ainda reconhece
    ///                              15 das 20 formas novas de dizer sim
    ///
    /// As 5 que sobram nao viram engano: viram uma pergunta repetida.
    ///
    /// Se o modelo nao trouxer o campo, o valor cai para 1.0 — impossivel de
    /// alcancar, entao o gerente pergunta de novo em vez de gravar. Um
    /// modelo velho tem de FALHAR FECHADO.
    /// </remarks>
    [property: JsonPropertyName("limiar_confirmacao")] double? LimiarConfirmacao,

    [property: JsonPropertyName("botoes")] int Botoes,
    [property: JsonPropertyName("medido")] MedidoJson? Medido,

    /// <summary>Os 7 tons. Estavam no arquivo desde sempre e o C# ignorava.</summary>
    /// <remarks>
    /// POR QUE ISTO PRECISOU ENTRAR.
    ///
    /// O tronco é COMPARTILHADO entre a cabeça de intenção e a de tom. O
    /// treino em Python soma os dois gradientes nele de propósito — nas
    /// palavras do `classificador_duplo.py`, "é o que faz as duas tarefas
    /// aprenderem juntas em vez de uma desfazer a outra".
    ///
    /// Enquanto o C# só lia, ignorar metade do modelo era só desperdício.
    /// A partir do momento em que ele TREINA, deixar o tom de fora é mexer
    /// no tronco sem saber o que a outra cabeça esperava dele — e devolver
    /// ao Python um modelo com as duas metades desalinhadas.
    ///
    /// Opcionais para trás: um `intencao.json` antigo, sem estes campos,
    /// continua carregando.
    /// </remarks>
    [property: JsonPropertyName("tons")] List<string>? Tons = null,

    [property: JsonPropertyName("camada_tom")] CamadaJson? CamadaTom = null)
{
    /// <summary>O corte do "sim", ou 1.0 se o modelo for velho demais para ter um.</summary>
    public double CorteDoSim => LimiarConfirmacao ?? 1.0;
}

public record CamadaJson(
    [property: JsonPropertyName("ativacao")] string Ativacao,
    [property: JsonPropertyName("pesos")] List<List<double>> Pesos,
    [property: JsonPropertyName("vies")] List<double> Vies);

/// <summary>
/// Os numeros MEDIDOS, que viajam junto com os pesos.
/// </summary>
/// <remarks>
/// Ja errei isto uma vez: o gabarito da conferencia ficou para tras quando
/// o modelo mudou, e a tela passou a mentir em silencio. Por isso toda
/// medida mora DENTRO do arquivo do modelo — os dois envelhecem juntos ou
/// nao envelhecem.
/// </remarks>
public record MedidoJson(
    [property: JsonPropertyName("epocas")] int Epocas,
    [property: JsonPropertyName("corpus")] int Corpus,
    [property: JsonPropertyName("intencoes")] int Intencoes,
    [property: JsonPropertyName("acerto_validacao_cruzada")] double Acerto,
    [property: JsonPropertyName("acerto_nos_botoes")] double AcertoNosBotoes,
    [property: JsonPropertyName("precisao_no_limiar")] double Precisao,
    [property: JsonPropertyName("cobertura_no_limiar")] double Cobertura,
    [property: JsonPropertyName("termina_certo_com_clique")] double TerminaCerto,
    [property: JsonPropertyName("erro_silencioso")] double ErroSilencioso);

/// <summary>Uma palavra da pergunta e se a rede a conhecia.</summary>
public record PecaLida(string Texto, bool Conhecida);

/// <summary>Uma intencao candidata e a probabilidade que a rede deu a ela.</summary>
public record Candidato(string Nome, double Probabilidade);

/// <summary>
/// TUDO o que a rede calculou para uma pergunta — nao so quem ganhou.
/// </summary>
/// <remarks>
/// Ate agora `Classificar` devolvia o vencedor e jogava fora o resto. Isso
/// custava caro em dois lugares:
///
///   1. A TELA nao tinha o que mostrar. "Pensamento dos neuronios" precisa
///      das ativacoes e da disputa, e elas eram descartadas na saida.
///
///   2. O SEGUNDO PALPITE se perdia. Medido em validacao cruzada: a rede
///      acerta 67,6% no primeiro, mas a resposta certa esta entre os tres
///      primeiros em 84,1% das vezes. Descartar o 2o e o 3o era jogar fora
///      dezesseis pontos de acerto que a rede JA TINHA calculado.
///
/// Por isso agora sai o pensamento inteiro, e quem chama decide o que usar.
/// </remarks>
public record Pensamento(
    string Pergunta,
    IReadOnlyList<PecaLida> Palavras,
    int TotalPecas,
    int PecasConhecidas,
    IReadOnlyList<double> Oculta,
    IReadOnlyList<Candidato> Candidatos,
    double Limiar)
{
    public Candidato Vencedor => Candidatos[0];
    public double Confianca => Candidatos[0].Probabilidade;
    public bool Confiavel => Confianca >= Limiar;

    /// <summary>Os candidatos que vale a pena oferecer num botao.</summary>
    /// <remarks>
    /// Corta em 1% porque oferecer uma opcao com 0,3% de probabilidade nao
    /// ajuda ninguem a escolher — so enche a tela.
    /// </remarks>
    public IReadOnlyList<Candidato> Botoes(int quantos)
        => Candidatos.Take(quantos).Where(c => c.Probabilidade >= 0.01).ToList();
}

public record Intencao(string Nome, double Confianca, bool Confiavel);

public interface IClassificadorDeIntencao
{
    Task<bool> CarregarAsync();
    bool Pronto { get; }
    ModeloIntencao? Modelo { get; }
    Intencao Classificar(string pergunta);
    Pensamento Pensar(string pergunta);
    Task<string> ConferirContraOPythonAsync();

    /// <summary>As peças de uma frase, do jeito exato que a rede as vê.</summary>
    IReadOnlyList<int> PecasDe(string texto);

    /// <summary>Troca os pesos que respondem pelos que a loja aprendeu.</summary>
    /// <remarks>
    /// SEM ISTO O APRENDIZADO NÃO APARECE. O `Aprendiz` treina uma cópia; se
    /// ninguém trouxer essa cópia para cá, o Chefe corrige, o sistema aprende
    /// de verdade — e o chat continua errando a mesma frase. É a forma mais
    /// rápida de ele parar de clicar, e a mais difícil de diagnosticar,
    /// porque tudo "funciona".
    /// </remarks>
    void UsarRede(Aprendizado.RedeTreinavel rede);
}

/// <summary>
/// A rede que le a pergunta do administrador e diz o que ele quer.
/// </summary>
/// <remarks>
/// PORTE FIEL DE `rede/texto.py` E `rede/classificador.py`.
///
/// A mesma conta existe duas vezes: treinada em Python, executada aqui. Se
/// as duas divergirem, o chat responde uma coisa e o modelo aprendeu outra —
/// o defeito mais dificil de achar num sistema de duas linguagens.
///
/// Por isso o porte e literal, na mesma ordem, com os mesmos nomes. E por
/// isso existe `ConferirContraOPythonAsync`, que refaz 20 frases e compara
/// com o que o Python produziu. A conferencia nao fica num teste que
/// ninguem roda: ela e um comando do proprio chat.
///
/// O LIMIAR VEM DO ARQUIVO, E NAO DAQUI
///
/// `Limiar` foi medido no modelo que esta sendo carregado. Cravar um numero
/// neste codigo faria o limiar ficar para tras no dia em que o modelo
/// mudasse — e o gerente passaria a responder onde deveria calar.
/// </remarks>
public class ClassificadorDeIntencao : IClassificadorDeIntencao
{
    private const int TamanhoNgrama = 3;

    /// <summary>Onde o modelo treinado mora, visto de dentro de qualquer app.</summary>
    /// <remarks>
    /// O `_content/AutonomousStore.Gerente/` NÃO é uma pasta que alguém criou:
    /// é o endereço que o SDK do Razor dá ao `wwwroot` de uma biblioteca dentro
    /// de todo aplicativo que a referencia. O arquivo existe uma vez só, aqui —
    /// e tanto o painel do admin quanto o do suporte o enxergam nesse caminho.
    ///
    /// Antes ele morava no `wwwroot` do AdminApp. Dar uma cópia ao suporte
    /// seria a mesma armadilha das duas cópias de código, com um agravante:
    /// um retreino atualizaria uma cópia e a outra ficaria para trás, e aí os
    /// dois gerentes passariam a discordar sobre a mesma pergunta.
    /// </remarks>
    private const string Pasta = "_content/AutonomousStore.Gerente/modelos/";

    /// <summary>
    /// O cliente HTTP que aponta para a própria origem do aplicativo.
    /// </summary>
    /// <remarks>
    /// Chamava-se "AdminAppEstatico" de quando o gerente morava lá dentro.
    /// Nome de app dentro da biblioteca é dívida: o suporte teria de registrar
    /// um cliente chamado "AdminApp" para o chat funcionar.
    /// </remarks>
    public const string ClienteEstatico = "GerenteEstatico";

    private readonly IHttpClientFactory _fabrica;
    private double[][]? _tabela;
    private double[][]? _pesos0, _pesos1;
    private double[]? _vies0, _vies1;
    private Dictionary<string, int>? _indice;

    public ModeloIntencao? Modelo { get; private set; }
    public bool Pronto => Modelo is not null;

    public ClassificadorDeIntencao(IHttpClientFactory fabrica) => _fabrica = fabrica;

    // ------------------------------------------------------------------
    // texto -> pecas   (porte de rede/texto.py)
    // ------------------------------------------------------------------

    /// <summary>Minusculas, sem acento, so letras e numeros.</summary>
    /// <remarks>
    /// "câmera", "camera", "CAMERA" tem que virar a mesma coisa. Quem digita
    /// com pressa escreve a segunda, e um modelo que trata as tres como
    /// palavras diferentes precisa de tres vezes mais exemplos.
    /// </remarks>
    public static string Normalizar(string texto)
    {
        var d = (texto ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        // Espacos colapsados: "a   b" e "a b" tem que dar as mesmas pecas.
        return string.Join(" ", sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>`agua` -> `&lt;ag`, `agu`, `gua`, `ua&gt;`.</summary>
    /// <remarks>
    /// Os sinais de inicio e fim importam: sem eles, "gua" apareceria igual
    /// em "agua" e em "guarda", e o modelo perderia a informacao de que num
    /// caso o pedaco esta no fim da palavra e no outro no comeco.
    ///
    /// E sao os trigramas que fazem "faturmento" (com erro de digitacao)
    /// ainda alcancar "faturamento": eles dividem 8 das 12 pecas.
    /// </remarks>
    public static IEnumerable<string> Trigramas(string palavra)
    {
        var cercada = "<" + palavra + ">";
        if (cercada.Length <= TamanhoNgrama)
        {
            yield return cercada;
            yield break;
        }
        for (var i = 0; i <= cercada.Length - TamanhoNgrama; i++)
            yield return cercada.Substring(i, TamanhoNgrama);
    }

    public static List<string> Pedacos(string texto)
    {
        var palavras = Normalizar(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var saida = new List<string>(palavras);
        foreach (var p in palavras) saida.AddRange(Trigramas(p));
        return saida;
    }

    private List<int> Indices(string texto) => Ler(texto).Indices;

    /// <summary>As peças de uma frase, do jeito exato que a rede as vê.</summary>
    /// <remarks>
    /// O treino precisa disto. E precisa que seja ESTA função, não uma cópia:
    /// duas tokenizações que discordam em um acento produzem duas redes
    /// diferentes usando a mesma tabela — o defeito mais difícil de enxergar
    /// que este arquivo poderia ter.
    /// </remarks>
    public IReadOnlyList<int> PecasDe(string texto) => Indices(texto);

    /// <summary>Passa a responder com os pesos que a loja aprendeu.</summary>
    /// <remarks>
    /// Sem cópia: o `Aprendiz` já é dono destes vetores e só entrega os que
    /// passaram na trava. Copiar aqui dobraria 725 KB na memória da aba a
    /// cada correção aceita, sem proteger nada — quem protege é a trava.
    /// </remarks>
    public void UsarRede(Aprendizado.RedeTreinavel rede)
    {
        _tabela = rede.Tabela;
        _pesos0 = rede.TroncoPesos;
        _vies0 = rede.TroncoVies;
        _pesos1 = rede.IntencaoPesos;
        _vies1 = rede.IntencaoVies;
    }

    /// <summary>O que a rede enxergou na frase, alem dos indices.</summary>
    private (List<int> Indices, List<PecaLida> Palavras, int Total) Ler(string texto)
    {
        var vistos = new List<int>();
        foreach (var p in Pedacos(texto))
            if (_indice!.TryGetValue(p, out var i)) vistos.Add(i);

        // Frase inteiramente desconhecida devolve o indice do `<?>`, e nao
        // lista vazia — vazia dividiria por zero na media.
        if (vistos.Count == 0) vistos.Add(0);

        // Para a TELA basta a palavra: mostrar os vinte e tantos trigramas
        // encheria o painel sem dizer nada a quem le. O contador de pecas
        // guarda o tamanho real da leitura.
        var palavras = Normalizar(texto)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new PecaLida(w, _indice!.ContainsKey(w)))
            .ToList();

        return (vistos, palavras, Pedacos(texto).Count);
    }

    // ------------------------------------------------------------------
    // a rede   (porte de rede/classificador.py)
    // ------------------------------------------------------------------

    private static double Sigmoid(double z)
        => z >= 0 ? 1.0 / (1.0 + Math.Exp(-z)) : Math.Exp(z) / (1.0 + Math.Exp(z));

    /// <summary>Resumo do pensamento: so quem ganhou.</summary>
    public Intencao Classificar(string pergunta)
    {
        var p = Pensar(pergunta);
        return new Intencao(p.Vencedor.Nome, p.Confianca, p.Confiavel);
    }

    /// <summary>A conta inteira, do jeito que a rede a faz.</summary>
    public Pensamento Pensar(string pergunta)
    {
        if (!Pronto)
            return new Pensamento(pergunta, Array.Empty<PecaLida>(), 0, 0,
                                  Array.Empty<double>(),
                                  new[] { new Candidato("nao_entendi", 0) }, 1.0);

        var (indices, palavras, total) = Ler(pergunta);
        var dim = Modelo!.Dimensao;

        // MEDIA dos vetores, e nao concatenacao: a pergunta tem tamanho
        // variavel e a ordem das palavras nao muda a intencao.
        var x = new double[dim];
        foreach (var i in indices)
        {
            var linha = _tabela![i];
            for (var d = 0; d < dim; d++) x[d] += linha[d];
        }
        for (var d = 0; d < dim; d++) x[d] /= indices.Count;

        var oculta = new double[_vies0!.Length];
        for (var j = 0; j < oculta.Length; j++)
        {
            var s = _vies0[j];
            var w = _pesos0![j];
            for (var d = 0; d < dim; d++) s += w[d] * x[d];
            oculta[j] = Sigmoid(s);
        }

        var n = _vies1!.Length;
        var z = new double[n];
        var maior = double.NegativeInfinity;
        for (var k = 0; k < n; k++)
        {
            var s = _vies1[k];
            var w = _pesos1![k];
            for (var j = 0; j < oculta.Length; j++) s += w[j] * oculta[j];
            z[k] = s;
            if (s > maior) maior = s;
        }

        // Subtrair o maximo antes de exponenciar: nao muda o resultado e
        // impede que e^z estoure. Mesma protecao do Python.
        double soma = 0;
        for (var k = 0; k < n; k++) { z[k] = Math.Exp(z[k] - maior); soma += z[k]; }

        var candidatos = new List<Candidato>(n);
        for (var k = 0; k < n; k++)
            candidatos.Add(new Candidato(Modelo.Intencoes[k], z[k] / soma));
        candidatos.Sort((a, b) => b.Probabilidade.CompareTo(a.Probabilidade));

        return new Pensamento(pergunta, palavras, total,
                              palavras.Count(w => w.Conhecida),
                              oculta, candidatos, Modelo.Limiar);
    }

    // ------------------------------------------------------------------

    public async Task<bool> CarregarAsync()
    {
        if (Pronto) return true;
        try
        {
            var http = _fabrica.CreateClient(ClienteEstatico);
            var m = await http.GetFromJsonAsync<ModeloIntencao>(Pasta + "intencao.json");
            if (m is null) return false;

            Modelo = m;
            _indice = new Dictionary<string, int>(m.Pecas.Count);
            for (var i = 0; i < m.Pecas.Count; i++) _indice[m.Pecas[i]] = i;

            _tabela = m.Tabela.Select(l => l.ToArray()).ToArray();
            _pesos0 = m.Camadas[0].Pesos.Select(l => l.ToArray()).ToArray();
            _vies0 = m.Camadas[0].Vies.ToArray();
            _pesos1 = m.Camadas[1].Pesos.Select(l => l.ToArray()).ToArray();
            _vies1 = m.Camadas[1].Vies.ToArray();
            return true;
        }
        catch
        {
            // Sem modelo o chat continua funcionando: ele responde que o
            // classificador nao carregou, em vez de quebrar a tela.
            return false;
        }
    }

    private record CasoDeConferencia(
        [property: JsonPropertyName("pergunta")] string Pergunta,
        [property: JsonPropertyName("intencao")] string Intencao,
        [property: JsonPropertyName("confianca")] double Confianca,
        [property: JsonPropertyName("n_pecas")] int NPecas);

    /// <summary>
    /// Refaz em C# as frases que o Python ja calculou, e compara.
    /// </summary>
    /// <remarks>
    /// Duas implementacoes da mesma matematica. Se discordarem, o chat
    /// responde uma coisa e o modelo aprendeu outra — e nada no sistema
    /// avisaria sozinho.
    ///
    /// Fica como comando do chat, e nao como teste num projeto separado,
    /// porque teste que precisa de outro projeto para rodar e teste que
    /// ninguem roda.
    /// </remarks>
    public async Task<string> ConferirContraOPythonAsync()
    {
        if (!Pronto) return "O classificador não carregou — não há o que conferir.";

        List<CasoDeConferencia>? casos;
        try
        {
            var http = _fabrica.CreateClient(ClienteEstatico);
            casos = await http.GetFromJsonAsync<List<CasoDeConferencia>>(Pasta + "conferencia.json");
        }
        catch
        {
            return "Não achei `AutonomousStore.Gerente/wwwroot/modelos/conferencia.json`.";
        }
        if (casos is null || casos.Count == 0) return "Nenhum caso de conferência.";

        var divergentes = new List<string>();
        double piorDiferenca = 0;

        foreach (var caso in casos)
        {
            var r = Classificar(caso.Pergunta);
            var diferenca = Math.Abs(r.Confianca - caso.Confianca);
            piorDiferenca = Math.Max(piorDiferenca, diferenca);

            if (r.Nome != caso.Intencao || diferenca > 1e-4)
                divergentes.Add($"- \"{caso.Pergunta}\": Python **{caso.Intencao}** " +
                                $"{caso.Confianca:P1} · C# **{r.Nome}** {r.Confianca:P1}");
        }

        if (divergentes.Count == 0)
            return $"**{casos.Count} casos conferidos, nenhuma divergência.**\n" +
                   $"Maior diferença de confiança entre Python e C#: {piorDiferenca:E1}.\n\n" +
                   "As duas implementações fazem a mesma conta.";

        return $"⚠ **{divergentes.Count} de {casos.Count} divergiram.**\n" +
               string.Join("\n", divergentes) +
               "\n\nO porte para C# não está fiel ao Python. O chat está respondendo " +
               "com uma conta diferente da que foi treinada.";
    }
}
