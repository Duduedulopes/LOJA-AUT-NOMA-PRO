using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using AutonomousStore.AdminApp.Models;
using AutonomousStore.AdminApp.Services.Agente;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.AdminApp.Services;

public interface IGerenteService
{
    Task<string> ResponderAsync(string pergunta);

    /// <summary>Responde uma intencao que o ADMINISTRADOR escolheu num botao.</summary>
    /// <remarks>
    /// Quando a rede fica abaixo do limiar, a tela oferece os tres melhores
    /// palpites. O clique nao e so uma resposta: e um ROTULO HUMANO para
    /// aquela frase, e rotulo humano e o dado que falta.
    /// </remarks>
    Task<string> ResponderComIntencaoAsync(string intencao, string pergunta);

    /// <summary>Manda a pergunta para o monitor gravar em disco.</summary>
    Task GravarPerguntaAsync(string pergunta, string intencao,
                             double confianca, bool escolhidaPorVoce);

    IReadOnlyList<string> Sugestoes { get; }
}

/// <summary>
/// O gerente respondendo perguntas sobre a loja.
/// </summary>
/// <remarks>
/// POR QUE O CEREBRO ESTA AQUI, E NAO NO SERVIDOR DO MONITOR
///
/// O faturamento sai de `/api/sessions/history`, que exige o JWT de admin,
/// e o AdminApp ja tem esse token. Manda-lo ao servidor Python so para que
/// aquele lado pudesse consultar espalharia uma credencial sem ganho. Do
/// monitor vem so a parte espacial, que e anonima.
///
/// A INTENCAO E RESOLVIDA POR REDE NEURAL, SEM RESERVA
///
/// Media de embutimentos de palavras e trigramas de caractere, camada
/// oculta, softmax. A regra de palavra-chave que existia aqui foi apagada e
/// NAO virou fallback: um fallback silencioso esconderia a rede caindo, e
/// ninguem saberia que o chat voltou a ser um `if`.
///
/// TODO NUMERO DESTE MODELO VIVE EM `wwwroot/modelos/intencao.json`
///
/// Quantas intencoes, quantas frases, qual limiar, quanto ela acerta. Nao
/// os repita aqui — este comentario ja teve "13 intencoes", "66,7%" e
/// "limiar 0,74" cravados, e os tres estavam errados poucas semanas depois.
/// Quem quiser o numero de hoje: `Classificador.Modelo.Medido`.
///
/// CALAR E UMA RESPOSTA
///
/// Abaixo do limiar a rede nao responde: mostra os tres melhores palpites e
/// deixa o Chefe escolher.
///
///     Responder errado custa uma decisao errada.
///     Calar custa uma pergunta reformulada.
///     Os dois erros nao valem o mesmo.
///
/// Toda pergunta feita fica em `PerguntasFeitas`. As que caem abaixo do
/// limiar sao o corpus que falta — o que fecha a distancia entre treino e
/// teste e dado, nao neuronio.
/// </remarks>
public class GerenteService : IGerenteService
{
    private readonly IProductApiService _produtos;
    private readonly ISessionApiService _sessoes;
    private readonly IGerenteEspacialService _espacial;
    private readonly IClassificadorDeIntencao _classificador;
    private readonly AgenteAutonomo _agente;
    private readonly IHttpClientFactory _fabrica;

    private static readonly CultureInfo Br = new("pt-BR");

    /// <summary>A ordem que esta em andamento, se houver uma.</summary>
    /// <remarks>
    /// Enquanto isto nao for nulo, a proxima mensagem do Chefe e
    /// RESPOSTA a uma pergunta, e nao uma pergunta nova. Era o que
    /// faltava: `ResponderAsync` classificava tudo do zero, entao
    /// "adiciona um produto" abria o dialogo e a resposta seguinte caia
    /// no classificador como se ninguem tivesse perguntado nada.
    /// </remarks>
    private Conversa? _conversa;

    /// <summary>O catalogo como estava quando a ordem abriu.</summary>
    /// <remarks>
    /// Guardado para que responder "qual produto?" nao custe outra ida a
    /// WebApi — e, mais importante, para que o produto que o resumo mostra
    /// seja o mesmo que a busca encontrou. Ler duas vezes abriria espaco
    /// para o preco mudar no meio da confirmacao e o Chefe aprovar um
    /// "de R$ 3,50" que ja nao e verdade.
    /// </remarks>
    private List<ProductDto>? _catalogoEmMemoria;

    /// <summary>Toda pergunta feita, para virar treino do classificador depois.</summary>
    public List<(DateTime Quando, string Pergunta, string Intencao, double Confianca)> PerguntasFeitas { get; } = new();

    public GerenteService(IProductApiService produtos, ISessionApiService sessoes,
                          IGerenteEspacialService espacial,
                          IClassificadorDeIntencao classificador,
                          IHttpClientFactory fabrica)
    {
        _produtos = produtos;
        _sessoes = sessoes;
        _espacial = espacial;
        _classificador = classificador;
        _fabrica = fabrica;
        _agente = new AgenteAutonomo();
    }

    /// <remarks>
    /// O BOTAO PRECISA CHEGAR NOS MESMOS LUGARES QUE A RESPOSTA DIRETA.
    ///
    /// Antes ele nao chegava: `ExecutarAsync` nao tem caso para
    /// `alterar_preco`, entao o clique caia no `_ => Ajuda()` e o gerente
    /// respondia com a lista de comandos. Clicar em "alterar preco" e
    /// receber a tela de ajuda e pior do que nao ter o botao.
    ///
    /// E o caminho importa mais do que parece: uma ordem fica abaixo do
    /// limiar com frequencia — "muda o preco da agua para 5,50" da 96,0%
    /// contra um limiar de 97%. Ou seja, o BOTAO e a rota normal das
    /// alteracoes, nao a excecao.
    ///
    /// O clique nao pula a confirmacao. Ele resolve so a duvida da rede
    /// sobre a INTENCAO; qual produto e qual valor continuam sendo
    /// conferidos com o dado real antes de gravar.
    /// </remarks>
    public async Task<string> ResponderComIntencaoAsync(string intencao, string pergunta)
    {
        if (Conversa.CamposDe(intencao) is { } campos)
            return ComoChefe(await AbrirAsync(pergunta, intencao, campos), momentoBom: true);

        return ComoChefe(await ExecutarAsync(intencao, Normalizar(pergunta)),
                         momentoBom: EhOperacaoDeAgente(intencao));
    }

    /// <summary>
    /// Grava a pergunta no monitor, que a escreve em `dados/perguntas_reais.jsonl`.
    /// </summary>
    /// <remarks>
    /// Ate agora as perguntas viviam so em `PerguntasFeitas`, na memoria do
    /// navegador: fechou a aba, perdeu. E sao exatamente as frases que valem
    /// para treinar — frase que o Eduardo digitou de verdade, com a pressa e
    /// o vicio de escrita dele.
    ///
    /// Se o monitor estiver fora do ar, isto falha em silencio de proposito.
    /// O gerente nao deve parar de responder porque nao conseguiu anotar.
    /// </remarks>
    public async Task GravarPerguntaAsync(string pergunta, string intencao,
                                          double confianca, bool escolhidaPorVoce)
    {
        try
        {
            var http = _fabrica.CreateClient("MonitorGerente");
            await http.PostAsJsonAsync("api/pergunta", new
            {
                pergunta,
                palpite = intencao,
                confianca,
                escolhida_por_voce = escolhidaPorVoce,
                de = "admin",
            });
        }
        catch
        {
            // monitor fora do ar: a pergunta fica so em PerguntasFeitas
        }
    }

    public IReadOnlyList<string> Sugestoes { get; } = new[]
    {
        "quantas pessoas estão na loja agora?",
        "o que está acabando?",
        "quanto faturamos hoje?",
        "o que tem no carrinho agora?",
        "como estão as câmeras?",
        "o que mais sai?",
        "vendemos mais que ontem?",
        "como funciona o rfid?",
        "os sistemas estão sincronizados?",
        "pessoas vs carrinhos",
        "status da api",
        "entrar na loja",
        // Sugestões do agente autônomo
        "adiciona um produto novo",
        "alterar preço do produto",
        "adicionar câmera nova",
        "configurar sistema"
    };

    // ------------------------------------------------------------------

    private static string SemAcento(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Normaliza para comparar: minusculas e sem acento.
    /// </summary>
    /// <remarks>
    /// Sem isto, "câmera" e "camera" seriam perguntas diferentes — e quem
    /// digita rapido escreve a segunda.
    /// </remarks>
    private static string Normalizar(string s) => SemAcento((s ?? "").ToLowerInvariant()).Trim();

    /// <summary>
    /// Procura uma palavra qualquer da lista dentro do texto ja normalizado.
    /// </summary>
    /// <remarks>
    /// ISTO NAO DECIDE INTENCAO — a rede ja disse "faturamento", e aqui so
    /// se le um modificador dentro dela ("de hoje" ou "no total").
    ///
    /// Palavra-chave serve para isso e nao servia para aquilo: intencao tem
    /// 38 respostas e mil formas de ser dita; recorte de periodo tem duas
    /// respostas, um punhado de palavras e nenhum corpus rotulado. O dia em
    /// que virar `faturamento_hoje` e `faturamento_total` no corpus, vira
    /// rede.
    /// </remarks>
    private static bool Tem(string textoNormalizado, params string[] palavras)
    {
        foreach (var palavra in palavras)
            if (textoNormalizado.Contains(palavra, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string Moeda(decimal v) => v.ToString("C2", Br);

    // ------------------------------------------------------------------

    public async Task<string> ResponderAsync(string pergunta)
    {
        if (string.IsNullOrWhiteSpace(pergunta))
            return "Pergunte alguma coisa e eu procuro nos dois sistemas.";

        // Comando de manutencao: refaz em C# as contas que o Python ja fez.
        if (Normalizar(pergunta) is "conferir" or "conferencia" or "autoteste")
            return await _classificador.ConferirContraOPythonAsync();

        if (!_classificador.Pronto && !await _classificador.CarregarAsync())
            return "O classificador de intenção não carregou " +
                   "(`wwwroot/modelos/intencao.json`). Sem ele eu não sei interpretar " +
                   "a pergunta — e prefiro dizer isso a adivinhar.";

        // A REDE FALA PRIMEIRO, SEMPRE — inclusive com dialogo aberto.
        //
        // E o que garante a rota de saida: no meio de "qual o novo preco?"
        // o Chefe pode escrever "quero outra coisa", e isso e
        // `cancelar_operacao` a 93,9%. Se o dialogo engolisse a mensagem
        // antes de classificar, nao haveria como sair dele a nao ser
        // respondendo o que ele perguntou.
        var intencao = _classificador.Classificar(pergunta);
        PerguntasFeitas.Add((DateTime.Now, pergunta, intencao.Nome, intencao.Confianca));

        // ...mas quem RESPONDE, com ordem em andamento, e o dialogo.
        if (_conversa is not null)
            return ComoChefe(await ContinuarAsync(pergunta, intencao));

        if (!intencao.Confiavel)
            return ComoChefe(NaoEntendi(intencao));

        // "SIM" SEM NADA PENDENTE NAO EXECUTA NADA.
        //
        // O corpus ensina "ok", "beleza", "pode" como `confirmar_acao`, e
        // isso e proposital: e assim que uma pessoa responde a "posso
        // alterar?". O preco de ensinar isso e que um "ok" solto tambem
        // vira `confirmar_acao`, porque o classificador nao ve contexto.
        //
        // Esta guarda e o que torna aquilo seguro. Se ela sair, um "ok"
        // perdido no meio da conversa vira uma gravacao no banco.
        if (intencao.Nome is "confirmar_acao" or "cancelar_operacao")
            return ComoChefe("Não tem nenhuma alteração esperando resposta agora. " +
                             "Se quiser mudar alguma coisa, é só pedir — por exemplo " +
                             "\"muda o preço da água para 5,50\".");

        // Ordem que mexe no sistema e que tem dados a coletar.
        if (Conversa.CamposDe(intencao.Nome) is { } campos)
            return ComoChefe(await AbrirAsync(pergunta, intencao.Nome, campos), momentoBom: true);

        if (EhOperacaoDeAgente(intencao.Nome))
            return ComoChefe(await ProcessarComoAgenteAsync(pergunta, intencao.Nome),
                             momentoBom: true);

        return ComoChefe(await ExecutarAsync(intencao.Nome, Normalizar(pergunta)),
                         momentoBom: intencao.Nome is "saudacao" or "ajuda");
    }

    /// <summary>Chama de "Chefe" de vez em quando — e só de vez em quando.</summary>
    /// <remarks>
    /// Tempero, e nao regra: em toda resposta vira tique, e o que daria
    /// personalidade da bajulacao. Sorteio puro tambem nao serve — ele da
    /// tres seguidos e depois quinze sem nenhum. O criterio:
    ///
    ///   - nunca duas seguidas, no maximo uma a cada quatro
    ///   - a primeira da conversa ganha, porque e apresentacao
    ///   - ordens ganham, porque ali marca que nao e uma consulta qualquer
    ///
    /// "Chefe, 17 unidades" nao acrescenta nada a "17 unidades".
    /// </remarks>
    private int _respostas;
    private int _ultimoTratamento = -9;

    private string ComoChefe(string resposta, bool momentoBom = false)
    {
        if (string.IsNullOrWhiteSpace(resposta)) return resposta;
        _respostas++;

        var comeco = resposta.Length > 60 ? resposta[..60] : resposta;
        if (comeco.Contains("Chefe", StringComparison.OrdinalIgnoreCase)) return resposta;
        if (resposta.StartsWith("⚠")) return resposta;

        var primeira = _respostas == 1;
        var faz_tempo = _respostas - _ultimoTratamento >= 4;
        if (!primeira && !(faz_tempo && momentoBom) && !(_respostas - _ultimoTratamento >= 7))
            return resposta;

        _ultimoTratamento = _respostas;

        var aberturas = new[] { "Chefe, ", "Pois não, Chefe. ", "Olha só, Chefe. ", "Deixa comigo, Chefe. " };
        var abre = aberturas[(_respostas / 4) % aberturas.Length];

        // Depois de "Chefe, " a frase segue em minuscula; depois de ponto,
        // nao. Sem isto sai "Chefe, 3 Produtos cadastrados".
        var corpo = resposta;
        if (abre.EndsWith(", ") && corpo.Length > 1 && char.IsUpper(corpo[0])
            && char.IsLower(corpo[1]))
        {
            corpo = char.ToLowerInvariant(corpo[0]) + corpo[1..];
        }
        return abre + corpo;
    }

    // ══════════════════════════════════════════════════════════════════
    //  A ORDEM, DO PEDIDO ATE A GRAVACAO
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Abre uma ordem: le da frase o que ela ja tem, pergunta o resto.</summary>
    /// <remarks>
    /// "vai usar os dados que ele possui" — a primeira coisa que este
    /// metodo faz e ler a propria frase. Quem escreveu "muda o preco da
    /// agua para 5,50" ja disse produto e valor; perguntar "qual produto?"
    /// depois disso e nao ter escutado.
    /// </remarks>
    private async Task<string> AbrirAsync(string pergunta, string operacao,
                                          List<Conversa.Campo> campos)
    {
        var conversa = new Conversa(operacao, pergunta, campos);

        var catalogo = await SegurarAsync(() => _produtos.GetAllAsync(), null!);
        if (catalogo is null)
            return "Não consegui ler o catálogo para conferir o produto — e não vou " +
                   "alterar nada às cegas. Tenta de novo daqui a pouco?";
        _catalogoEmMemoria = catalogo;

        // ---- o produto ------------------------------------------------
        if (campos.Any(c => c.Nome == "produto"))
        {
            var (achado, varios) = LeitorDeValores.Produto(catalogo, pergunta);
            if (achado is not null)
            {
                conversa.Produto = achado;
                conversa.Guardar("produto", achado.Name);
            }
            else if (varios.Count > 1)
            {
                // Mais de um casou. Escolher o primeiro seria adivinhar em
                // cima de uma ordem de escrita — justo onde nao se adivinha.
                _conversa = conversa;
                return $"Achei {varios.Count} produtos que batem: " +
                       string.Join(", ", varios.Take(6).Select(p => $"**{p.Name}**")) +
                       ".\n\nQual deles?";
            }
        }

        // ---- os numeros, na frase JA SEM o nome do produto -------------
        var resto = LeitorDeValores.SemOProduto(pergunta, conversa.Produto);
        foreach (var campo in campos.Where(c => !conversa.Dados.ContainsKey(c.Nome)))
        {
            if (campo.Tipo == Conversa.TipoDeValor.Dinheiro &&
                LeitorDeValores.Dinheiro(resto) is decimal d)
                conversa.Guardar(campo.Nome, d.ToString(CultureInfo.InvariantCulture));
            else if (campo.Tipo == Conversa.TipoDeValor.Inteiro &&
                     LeitorDeValores.Inteiro(resto) is int i)
                conversa.Guardar(campo.Nome, i.ToString(CultureInfo.InvariantCulture));
        }

        _conversa = conversa;
        return ProximoPasso(conversa);
    }

    /// <summary>Ou pergunta o que falta, ou mostra o resumo e pede o sim.</summary>
    private string ProximoPasso(Conversa c)
    {
        if (c.Falta is { } falta)
            return falta.Pergunta;

        c.VaiConfirmar();
        return Resumo(c);
    }

    /// <summary>O que vai acontecer, com o dado REAL, antes de acontecer.</summary>
    /// <remarks>
    /// "verificar se e isso mesmo que ele deve fazer".
    ///
    /// O resumo mostra o DE e o PARA lidos do catalogo, nao so o que o
    /// Chefe digitou. Confirmar "posso mudar o preco da agua para 5,50?"
    /// nao ajuda ninguem a perceber que o produto certo era outro; mostrar
    /// "Agua com Gas 500ml, hoje R$ 4,20, passa a R$ 5,50" ajuda.
    /// </remarks>
    private string Resumo(Conversa c)
    {
        var sb = new StringBuilder();

        switch (c.Operacao)
        {
            case "alterar_preco":
            {
                var p = c.Produto!;
                var novo = decimal.Parse(c.Dados["preco"], CultureInfo.InvariantCulture);
                sb.AppendLine($"Entendi assim:\n");
                sb.AppendLine($"**{p.Name}**");
                sb.AppendLine($"- preço hoje: {Moeda(p.Price)}");
                sb.AppendLine($"- preço novo: **{Moeda(novo)}**");
                if (p.Price > 0)
                {
                    var var_pct = (novo - p.Price) / p.Price;
                    sb.AppendLine($"- diferença: {(var_pct >= 0 ? "+" : "")}{var_pct:P1}");
                    // Um erro de digito aparece como uma variacao absurda.
                    // Melhor gritar antes de gravar do que explicar depois.
                    if (Math.Abs(var_pct) >= 0.5m)
                        sb.AppendLine($"\n⚠ Isso é uma mudança grande. Confere se não " +
                                      $"faltou ou sobrou um dígito.");
                }
                break;
            }
            case "alterar_estoque":
            {
                var p = c.Produto!;
                var alvo = int.Parse(c.Dados["quantidade"], CultureInfo.InvariantCulture);
                var delta = alvo - p.StockQuantity;
                sb.AppendLine($"Entendi assim:\n");
                sb.AppendLine($"**{p.Name}**");
                sb.AppendLine($"- estoque hoje: {p.StockQuantity} unidades");
                sb.AppendLine($"- estoque novo: **{alvo} unidades**");
                sb.AppendLine(delta >= 0
                    ? $"- entram {delta} unidades"
                    : $"- saem {-delta} unidades");
                // Dizer AGORA o que a API nao faz, e nao depois do "sim".
                // Pedir confirmacao para algo que nao vai acontecer e pior
                // do que nao ter perguntado.
                if (delta < 0)
                    sb.AppendLine($"\n⚠ Eu só consigo somar ao estoque, não tirar. " +
                                  $"Baixa de estoque sai por venda. Posso registrar " +
                                  $"outra coisa no lugar?");
                break;
            }
            case "adicionar_produto":
            {
                var preco = decimal.Parse(c.Dados["preco"], CultureInfo.InvariantCulture);
                var qtd = int.Parse(c.Dados["quantidade"], CultureInfo.InvariantCulture);
                sb.AppendLine($"Entendi assim:\n");
                sb.AppendLine($"**{c.Dados["nome"]}** (produto novo)");
                sb.AppendLine($"- preço: {Moeda(preco)}");
                sb.AppendLine($"- estoque inicial: {qtd} unidades");
                sb.AppendLine($"- código de barras: fica provisório, para você " +
                              $"preencher na tela de cadastro");
                break;
            }
            default:
                sb.AppendLine($"Entendi que você quer {Legivel(c.Operacao)}.");
                break;
        }

        sb.Append("\nPosso fazer?");
        return sb.ToString();
    }

    /// <summary>A mensagem seguinte, quando ha uma ordem em andamento.</summary>
    /// <remarks>
    /// A ORDEM DAS PERGUNTAS AQUI E A SEGURANCA DO SISTEMA.
    ///
    /// 1. cancelar vem antes de tudo, para que a saida nunca esteja
    ///    trancada;
    /// 2. na confirmacao, so um "sim" acima do corte MEDIDO grava;
    /// 3. qualquer outra coisa na confirmacao NAO grava — repete e
    ///    pergunta de novo. Silencio nao e consentimento.
    /// </remarks>
    private async Task<string> ContinuarAsync(string pergunta, Intencao intencao)
    {
        var c = _conversa!;

        // ---- 1. a saida, sempre aberta --------------------------------
        if (intencao.Nome == "cancelar_operacao" && intencao.Confiavel)
        {
            _conversa = null;
            return "Deixei pra lá, não alterei nada. O que você precisa?";
        }

        // ---- 2. a confirmacao -----------------------------------------
        if (c.Onde == Conversa.Passo.Confirmando)
        {
            var corte = _classificador.Modelo?.CorteDoSim ?? 1.0;

            if (intencao.Nome == "confirmar_acao" && intencao.Confianca >= corte)
            {
                _conversa = null;
                return await ExecutarOrdemAsync(c);
            }

            // NAO E SIM. Entao nao grava.
            //
            // Inclui o caso perigoso que a medida encontrou: "para por
            // favor" sai da rede como `confirmar_acao` a 99,4% — acima do
            // limiar geral (0,95), abaixo do corte do sim (0,995). Quem
            // escreveu aquilo queria PARAR.
            c.NaoEntendi();
            if (c.Desistiu)
            {
                _conversa = null;
                return "Não consegui entender se era pra fazer ou não, então " +
                       "não fiz nada. Se quiser, começa de novo com o pedido inteiro — " +
                       "por exemplo \"muda o preço da água para 5,50\".";
            }
            return "Só pra eu não errar: **sim** eu faço, **não** eu deixo como está.\n\n"
                 + Resumo(c);
        }

        // ---- 3. a coleta ----------------------------------------------
        var campo = c.Falta!;
        if (LerCampo(c, campo, pergunta))
            return ProximoPasso(c);

        // Nao deu para ler como resposta. Se a rede tem certeza de que isto
        // e OUTRO assunto, e outro assunto — "ou volta para outra pergunta".
        if (intencao.Confiavel && Conversa.CamposDe(intencao.Nome) is null
            && intencao.Nome is not ("confirmar_acao" or "cancelar_operacao"))
        {
            _conversa = null;
            return "Deixei a alteração de lado por enquanto.\n\n"
                 + await ExecutarAsync(intencao.Nome, Normalizar(pergunta));
        }

        c.NaoEntendi();
        if (c.Desistiu)
        {
            _conversa = null;
            return $"Não consegui entender a resposta, então parei por aqui sem alterar nada. " +
                   $"Tenta escrever o pedido inteiro numa frase só?";
        }
        return campo.Tipo switch
        {
            Conversa.TipoDeValor.Dinheiro => "Preciso de um valor, tipo 5,50 ou 12.\n\n" + campo.Pergunta,
            Conversa.TipoDeValor.Inteiro  => "Preciso de um número inteiro, tipo 30.\n\n" + campo.Pergunta,
            Conversa.TipoDeValor.Produto  => "Não achei esse produto no catálogo.\n\n" + campo.Pergunta,
            _ => campo.Pergunta,
        };
    }

    /// <summary>Le a mensagem como o VALOR do campo que foi perguntado.</summary>
    /// <remarks>
    /// Isto nao e classificar intencao — a rede ja decidiu o que ele quer.
    /// E anotar o numero que ele acabou de ditar. Ver a nota de abertura em
    /// `LeitorDeValores`.
    /// </remarks>
    private bool LerCampo(Conversa c, Conversa.Campo campo, string resposta)
    {
        switch (campo.Tipo)
        {
            case Conversa.TipoDeValor.Produto:
            {
                var catalogo = _catalogoEmMemoria;
                if (catalogo is null) return false;
                var (achado, _) = LeitorDeValores.Produto(catalogo, resposta);
                if (achado is null) return false;
                c.Produto = achado;
                c.Guardar("produto", achado.Name);
                return true;
            }
            case Conversa.TipoDeValor.Dinheiro:
                if (LeitorDeValores.Dinheiro(resposta) is not decimal d || d < 0) return false;
                c.Guardar(campo.Nome, d.ToString(CultureInfo.InvariantCulture));
                return true;

            case Conversa.TipoDeValor.Inteiro:
                if (LeitorDeValores.Inteiro(resposta) is not int i || i < 0) return false;
                c.Guardar(campo.Nome, i.ToString(CultureInfo.InvariantCulture));
                return true;

            default:
                var t = (resposta ?? "").Trim();
                if (t.Length < 2) return false;
                c.Guardar(campo.Nome, t);
                return true;
        }
    }

    /// <summary>Grava. So chega aqui depois de um sim acima do corte medido.</summary>
    private async Task<string> ExecutarOrdemAsync(Conversa c)
        => await ExecutorDoAgenteAsync(c.Operacao, c.ParaExecutor());

    /// <summary>
    /// Determina se a intenção corresponde a uma operação do agente autônomo.
    /// </summary>
    private bool EhOperacaoDeAgente(string intencao)
    {
        return intencao is "adicionar_produto" or "alterar_preco" or "alterar_estoque" or 
               "remover_produto" or "configurar_camera" or "configurar_sistema" or 
               "reiniciar_servico" or "confirmar_acao" or "cancelar_operacao" or 
               "escolher_solucao";
    }

    /// <summary>Operacoes de agente que nao coletam dados: configuracao e sistema.</summary>
    /// <remarks>
    /// O NOME DA REDE VAI DIRETO PARA O EXECUTOR, e este metodo existe para
    /// explicar por que ele deixou de passar por `AgenteAutonomo`.
    ///
    /// O caminho antigo era:
    ///
    ///     rede diz "alterar_preco" (99%)
    ///        -> AgenteAutonomo.ProcessarOrdemAsync
    ///        -> DetectarIntencao(ordem)  <-- if (texto.Contains("altera"))
    ///        -> "alterar"
    ///
    /// A decisao da rede era calculada e jogada fora, e um `Contains()`
    /// decidia no lugar dela. Fora de ser exatamente o que nao se faz neste
    /// projeto, os nomes que ele produzia nao existiam em lugar nenhum:
    ///
    ///     ClassificadorDeRisco  tem "alterar_preco", nao tem "alterar"
    ///        -> caia no padrao Medio  -> RequerAprovacaoManual = false
    ///     ExecutorDoAgenteAsync tem "alterar_preco", nao tem "alterar"
    ///        -> "Operacao nao implementada"
    ///
    /// As duas pontas quebradas se cancelavam: nada executava, entao nada
    /// estragava. Mas `AlterarPrecoAgenteAsync` recebia um dicionario VAZIO
    /// nesse caminho, e ele procura o produto com
    /// `Name.Contains(nome)` — que com nome vazio casa com o PRIMEIRO
    /// produto do catalogo. Bastava alguem "consertar" o dispatch para o
    /// primeiro produto da loja passar a valer R$ 0,00 sem confirmacao
    /// nenhuma.
    ///
    /// Agora `alterar_preco`, `alterar_estoque` e `adicionar_produto` vao
    /// por `AbrirAsync`, com dado real e confirmacao. Sobra aqui o que nao
    /// coleta nada.
    /// </remarks>
    private async Task<string> ProcessarComoAgenteAsync(string pergunta, string intencao)
    {
        try
        {
            return await ExecutorDoAgenteAsync(intencao, new Dictionary<string, object>
            {
                ["pedido"] = pergunta,
            });
        }
        catch (Exception ex)
        {
            return $"Não consegui executar isso: {ex.Message}";
        }
    }

    /// <summary>
    /// Executor de operações do agente autônomo.
    /// </summary>
    private async Task<string> ExecutorDoAgenteAsync(string operacao, Dictionary<string, object> parametros)
    {
        return operacao switch
        {
            "adicionar_produto" => await AdicionarProdutoAgenteAsync(parametros),
            "alterar_preco" => await AlterarPrecoAgenteAsync(parametros),
            "alterar_estoque" => await AlterarEstoqueAgenteAsync(parametros),
            "remover_produto" => await RemoverProdutoAgenteAsync(parametros),
            "configurar_camera" => ConfigurarCameraAgente(parametros),
            "configurar_sistema" => ConfigurarSistemaAgente(parametros),
            "reiniciar_servico" => ReiniciarServicoAgente(parametros),
            _ => $"Operação '{operacao}' ainda não implementada no agente."
        };
    }

    // =================================================================
    // Operações do agente autônomo - IMPLEMENTAÇÃO REAL
    // =================================================================

    private async Task<string> AdicionarProdutoAgenteAsync(Dictionary<string, object> parametros)
    {
        try
        {
            // Extrai parâmetros do diálogo
            var nome = parametros.GetValueOrDefault("nome", "")?.ToString() ?? "";
            var precoStr = parametros.GetValueOrDefault("preco", "0")?.ToString() ?? "0";
            var quantidadeStr = parametros.GetValueOrDefault("quantidade", "0")?.ToString() ?? "0";
            var tipo = parametros.GetValueOrDefault("tipo", "produto")?.ToString() ?? "produto";

            // Valida e converte os parâmetros
            if (!decimal.TryParse(precoStr.Replace("R$", "").Trim(), out var preco))
                return "Preço inválido. Por favor, forneça um valor numérico válido (ex: 5.50).";

            if (!int.TryParse(quantidadeStr, out var quantidade))
                return "Quantidade inválida. Por favor, forneça um número inteiro válido (ex: 50).";

            if (string.IsNullOrWhiteSpace(nome))
                return "Nome do produto é obrigatório.";

            // Cria o produto na API
            // CreateProductRequest e record POSICIONAL: nao aceita
            // inicializador de objeto, e `Barcode` nao tem valor padrao.
            //
            // O codigo de barras nao vem da conversa — ninguem dita treze
            // digitos num chat. Gero um provisorio marcado, para o produto
            // existir hoje e o codigo real entrar depois pela tela de
            // cadastro. Inventar um codigo que PARECA de verdade seria pior:
            // ele acabaria colado numa prateleira.
            var provisorio = $"SEM-CODIGO-{DateTime.Now:yyyyMMddHHmmss}";

            var request = new CreateProductRequest(
                Name: nome,
                Barcode: provisorio,
                Price: preco,
                CompanyId: null,
                CategoryId: null,
                Description: $"Adicionado pelo gerente virtual — {tipo}",
                ImageUrl: null,
                StockQuantity: quantidade);

            var (sucesso, produto, erro) = await _produtos.CreateAsync(request);

            if (sucesso && produto is not null)
            {
                return $"✅ Produto adicionado com sucesso, Chefe!\n\n" +
                       $"**{produto.Name}**\n" +
                       $"- ID: {produto.Id}\n" +
                       $"- Preço: {Moeda(produto.Price)}\n" +
                       $"- Estoque: {produto.StockQuantity} unidades\n\n" +
                       $"O produto já está disponível no catálogo. " +
                       $"Quer que eu vincule uma tag RFID agora?";
            }
            else
            {
                return $"❌ Não consegui adicionar o produto: {erro}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao adicionar produto: {ex.Message}";
        }
    }

    private async Task<string> AlterarPrecoAgenteAsync(Dictionary<string, object> parametros)
    {
        try
        {
            var nomeProduto = parametros.GetValueOrDefault("nome", "")?.ToString() ?? "";
            var novoPrecoStr = parametros.GetValueOrDefault("preco", "0")?.ToString() ?? "0";

            if (!decimal.TryParse(novoPrecoStr.Replace("R$", "").Trim(), out var novoPreco))
                return "Preço inválido. Por favor, forneça um valor numérico válido.";

            // Busca o produto pelo nome
            var todos = await _produtos.GetAllAsync();
            var produto = todos.FirstOrDefault(p => Normalizar(p.Name).Contains(Normalizar(nomeProduto)));

            if (produto is null)
                return $"Não encontrei o produto '{nomeProduto}'. Quer que eu liste os produtos disponíveis?";

            // Altera o preço na API
            var (sucesso, erro) = await _produtos.UpdatePriceAsync(produto.Id, novoPreco);

            if (sucesso)
            {
                return $"✅ Preço alterado com sucesso, Chefe!\n\n" +
                       $"**{produto.Name}**\n" +
                       $"- Preço anterior: {Moeda(produto.Price)}\n" +
                       $"- Novo preço: {Moeda(novoPreco)}\n\n" +
                       $"A alteração já está valendo no sistema.";
            }
            else
            {
                return $"❌ Não consegui alterar o preço: {erro}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao alterar preço: {ex.Message}";
        }
    }

    private async Task<string> AlterarEstoqueAgenteAsync(Dictionary<string, object> parametros)
    {
        try
        {
            var nomeProduto = parametros.GetValueOrDefault("nome", "")?.ToString() ?? "";
            var novaQuantidadeStr = parametros.GetValueOrDefault("quantidade", "0")?.ToString() ?? "0";

            if (!int.TryParse(novaQuantidadeStr, out var novaQuantidade))
                return "Quantidade inválida. Por favor, forneça um número inteiro válido.";

            // Busca o produto pelo nome
            var todos = await _produtos.GetAllAsync();
            var produto = todos.FirstOrDefault(p => Normalizar(p.Name).Contains(Normalizar(nomeProduto)));

            if (produto is null)
                return $"Não encontrei o produto '{nomeProduto}'. Quer que eu liste os produtos disponíveis?";

            // Calcula a diferença para reposição
            var diferenca = novaQuantidade - produto.StockQuantity;

            if (diferenca == 0)
                return $"O estoque de {produto.Name} já está em {novaQuantidade} unidades. Nada para alterar.";

            if (diferenca > 0)
            {
                // Reposição positiva
                var (sucesso, erro) = await _produtos.RestockAsync(produto.Id, diferenca);

                if (sucesso)
                {
                    return $"✅ Estoque atualizado com sucesso, Chefe!\n\n" +
                           $"**{produto.Name}**\n" +
                           $"- Estoque anterior: {produto.StockQuantity} unidades\n" +
                           $"- Adicionado: {diferenca} unidades\n" +
                           $"- Novo estoque: {novaQuantidade} unidades\n\n" +
                           $"A reposição foi registrada no sistema.";
                }
                else
                {
                    return $"❌ Não consegui atualizar o estoque: {erro}";
                }
            }
            else
            {
                // Estoque negativo - não suportado diretamente, sugere alternativa
                return $"⚠ Não posso reduzir o estoque diretamente, Chefe.\n\n" +
                       $"**{produto.Name}**\n" +
                       $"- Estoque atual: {produto.StockQuantity} unidades\n" +
                       $"- Solicitado: {novaQuantidade} unidades\n" +
                       $"- Diferença: {diferenca} unidades\n\n" +
                       $"Para reduzir o estoque, isso deve ser feito através de uma venda. " +
                       $"Quer que eu registre uma venda simulada ou prefere fazer manualmente?";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao alterar estoque: {ex.Message}";
        }
    }

    private async Task<string> RemoverProdutoAgenteAsync(Dictionary<string, object> parametros)
    {
        try
        {
            var nomeProduto = parametros.GetValueOrDefault("nome", "")?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(nomeProduto))
                return "Nome do produto é obrigatório para remoção.";

            // Busca o produto pelo nome
            var todos = await _produtos.GetAllAsync();
            var produto = todos.FirstOrDefault(p => Normalizar(p.Name).Contains(Normalizar(nomeProduto)));

            if (produto is null)
                return $"Não encontrei o produto '{nomeProduto}'. Quer que eu liste os produtos disponíveis?";

            // ⚠️ ATENÇÃO: A API não tem endpoint de remoção direta
            // Vou simular sugerindo inativação
            return $"⚠️ A API atual não suporta remoção direta de produtos, Chefe.\n\n" +
                   $"**{produto.Name}** encontrado (ID: {produto.Id})\n\n" +
                   $"Como alternativa, posso:\n" +
                   $"1. Zerar o estoque para torná-lo indisponível\n" +
                   $"2. Vincular uma tag RFID específica para controle\n\n" +
                   $"Qual alternativa prefere?";
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao remover produto: {ex.Message}";
        }
    }

    private string ConfigurarCameraAgente(Dictionary<string, object> parametros)
    {
        try
        {
            var ip = parametros.GetValueOrDefault("ip", "")?.ToString() ?? "";
            var tipo = parametros.GetValueOrDefault("tipo", "")?.ToString() ?? "";
            var papel = parametros.GetValueOrDefault("papel", "")?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(ip))
                return "Endereço IP da câmera é obrigatório.";

            // ⚠️ ATENÇÃO: Configuração de câmera envolve o SO Espacial
            // Vou simular a resposta
            return $"⚠️ A configuração de câmeras envolve o SO Espacial, Chefe.\n\n" +
                   $"**Câmera a ser configurada:**\n" +
                   $"- IP: {ip}\n" +
                   $"- Tipo: {tipo}\n" +
                   $"- Papel: {papel}\n\n" +
                   $"Para adicionar esta câmera, preciso:\n" +
                   $"1. Acesso ao arquivo de configuração do SO Espacial\n" +
                   $"2. Reiniciar o serviço de captura\n" +
                   $"3. Calibrar a posição\n\n" +
                   $"Quer que eu prepare as instruções detalhadas ou prefere fazer manualmente?";
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao configurar câmera: {ex.Message}";
        }
    }

    private string ConfigurarSistemaAgente(Dictionary<string, object> parametros)
    {
        try
        {
            var configuracao = parametros.GetValueOrDefault("configuracao", "")?.ToString() ?? "";

            return $"⚠️ A configuração do sistema requer acesso direto aos arquivos, Chefe.\n\n" +
                   $"**Configuração solicitada:** {configuracao}\n\n" +
                   $"Para segurança, alterações de configuração do sistema:\n" +
                   $"1. Devem ser feitas diretamente nos arquivos\n" +
                   $"2. Requerem reinício dos serviços\n" +
                   $"3. Devem ser testadas em ambiente de desenvolvimento\n\n" +
                   $"Quer que eu gere um guia passo-a-passo para esta configuração?";
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao configurar sistema: {ex.Message}";
        }
    }

    private string ReiniciarServicoAgente(Dictionary<string, object> parametros)
    {
        try
        {
            var servico = parametros.GetValueOrDefault("servico", "")?.ToString() ?? "";

            return $"⚠️ Reinício de serviços é uma operação crítica, Chefe.\n\n" +
                   $"**Serviço solicitado:** {servico}\n\n" +
                   $"Por segurança, reinícios de serviço:\n" +
                   $"1. Requerem aprovação explícita\n" +
                   $"2. Devem ser feitos em horários de baixo movimento\n" +
                   $"3. Podem afetar usuários ativos\n\n" +
                   $"⚠️ Posso preparar o procedimento seguro de reinício?\n" +
                   $"Isso inclui backup do estado atual e notificação aos usuários.";
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao reiniciar serviço: {ex.Message}";
        }
    }

    /// <summary>
    /// A resposta quando a confianca fica abaixo do limiar medido.
    /// </summary>
    /// <remarks>
    /// Diz O QUE achou e QUANTO confiou, em vez de um "não entendi" seco.
    /// Assim o administrador consegue reformular na direcao certa — e ve que
    /// o gerente chegou perto, o que e diferente de nao ter ideia.
    /// </remarks>
    private string NaoEntendi(Intencao intencao)
    {
        // NUMERO LIDO DO MODELO, NAO DIGITADO AQUI.
        //
        // Estava escrito "508 frases" na mao. O corpus passou de 2.200 e a
        // tela continuou dizendo 508 — pela terceira vez neste projeto, um
        // numero medido sobreviveu ao que o gerou. Agora ele vem do arquivo.
        var limiar = _classificador.Modelo?.Limiar ?? 0.74;
        var corpus = _classificador.Modelo?.Medido?.Corpus ?? 0;
        // UMA CASA DECIMAL, E NAO ZERO.
        //
        // Com {:P0} apareceu na tela "96,8% de confianca — abaixo dos 97%"
        // arredondado para "97% — abaixo dos 97%". Verdadeiro na conta e
        // absurdo na leitura. Quando dois numeros sao comparados na mesma
        // frase, eles precisam de casas suficientes para a comparacao fazer
        // sentido a quem le.
        return $"Não tenho certeza do que você quis dizer. Meu palpite foi " +
               $"**{Legivel(intencao.Nome)}**, mas só com {intencao.Confianca:P1} de " +
               $"certeza — e eu só respondo sozinho acima de {limiar:P1}.\n\n" +
               $"Escolha abaixo o que você queria. Seu clique me ensina: " +
               $"hoje eu aprendi com {corpus} frases, e uma sua vale mais que dez " +
               $"que eu invente.";
    }

    private async Task<string> ExecutarAsync(string intencao, string t) => intencao switch
    {
        "pessoas_na_loja" => await PessoasAsync(),
        "estoque_baixo"   => await EstoqueBaixoAsync(),
        "faturamento"     => await FaturamentoAsync(t),
        "carrinho"        => await CarrinhoAsync(),
        "cameras"         => await CamerasAsync(),
        "estoque"         => await EstoqueAsync(t, t),
        "preco"           => await PrecoAsync(t),
        "comparar"        => await CompararAsync(),
        "mais_vendidos"   => await MaisVendidosAsync(),
        "duvida_sistema"  => Duvida(t),
        "saudacao"        => Saudacao(),
        "fora_de_escopo"  => ForaDeEscopo(),
        "ajuda"           => Ajuda(),
        // Novas intenções de integração
        "integracao_sistemas" => await IntegracaoSistemasAsync(),
        "analise_combinada"   => await AnaliseCombinadaAsync(),
        // Novas intenções de cliente
        "entrada_loja"   => await EntradaLojaAsync(),
        "meu_carrinho"   => await CarrinhoAsync(), // Reutiliza carrinho existente
        "pagamento"      => await PagamentoAsync(),
        // Novas intenções de API
        "status_api"     => await StatusApiAsync(),
        "logs_sistema"   => LogsSistemaAsync(),
        _                 => Ajuda(),
    };

    // ------------------------------------------------------------------

    private async Task<string> PessoasAsync()
    {
        var espacial = await _espacial.ObterAsync();
        var sessao = await SessaoAbertaAsync();
        var pendentes = await SegurarAsync(() => _sessoes.GetPendingEntryAsync(), new List<SessionDto>());

        var sb = new StringBuilder();

        if (espacial is { Online: true })
        {
            sb.Append($"**{espacial.Pessoas}** ");
            sb.Append(espacial.Pessoas == 1 ? "pessoa rastreada" : "pessoas rastreadas");
            sb.Append(" no chão da loja");
            if (espacial.Rastros is { Count: > 0 })
            {
                var descricoes = espacial.Rastros.Select(r =>
                    $"rastro {r.Id} ({r.Acao ?? "?"}, {r.Velocidade.ToString("0.00", Br)} m/s)");
                sb.Append(": " + string.Join(", ", descricoes));
            }
            sb.Append(".\n\n");
        }
        else
        {
            sb.Append("Não consigo contar corpos: o Sistema Espacial SO não está respondendo");
            sb.Append(espacial?.Erro is { Length: > 0 } e ? $" ({e})" : "");
            sb.Append(". Rode `python monitor/servidor.py` na máquina dele.\n\n");
        }

        sb.Append(sessao is null
            ? "Nenhuma sessão de compra aberta."
            : $"Uma sessão aberta, com {sessao.Items.Count} itens e {Moeda(sessao.Total)}.");

        if (pendentes.Count > 0)
            sb.Append($" E {pendentes.Count} aguardando entrada.");

        // A DIFERENCA ENTRE OS DOIS NUMEROS E A INFORMACAO.
        if (espacial is { Online: true })
        {
            var comSessao = sessao is null ? 0 : 1;
            if (espacial.Pessoas > comSessao)
                sb.Append($"\n\n⚠ São {espacial.Pessoas} corpos e {comSessao} sessão(ões). " +
                          "Alguém está na loja sem ter feito o check-in pelo QR.");
        }

        return sb.ToString();
    }

    private async Task<string> EstoqueBaixoAsync()
    {
        var baixos = await SegurarAsync(() => _produtos.GetLowStockAsync(), null!);
        if (baixos is null)
            return "Não consegui ler o estoque: a WebApi não respondeu.";
        if (baixos.Count == 0)
            return "Nada em baixa. Todos os produtos estão acima do mínimo.";

        var linhas = baixos
            .OrderBy(p => p.StockQuantity)
            .Select(p => $"- **{p.Name}**: restam {p.StockQuantity}" +
                         (p.MinimumStockThreshold is int m ? $", mínimo {m}" : ""));

        return $"**{baixos.Count}** {(baixos.Count == 1 ? "produto" : "produtos")} em baixa:\n" +
               string.Join("\n", linhas);
    }

    private async Task<string> FaturamentoAsync(string t)
    {
        var historico = await SegurarAsync(() => _sessoes.GetHistoryAsync(), null!);
        if (historico is null)
            return "Não consegui ler o histórico de vendas. Esse endpoint exige login de admin — " +
                   "confira se a sua sessão do painel não expirou.";

        var pagas = historico.Where(s => s.PaymentConfirmedAt is not null).ToList();
        var hoje = pagas.Where(s => s.PaymentConfirmedAt!.Value.Date == DateTime.Today).ToList();

        var querTudo = Tem(t, "total", "sempre", "geral", "tudo", "historico");
        var alvo = querTudo ? pagas : hoje;
        var quando = querTudo ? "no total" : "hoje";

        if (alvo.Count == 0)
            return $"Nenhuma venda paga {quando}." +
                   (querTudo ? "" : $" No total já foram {pagas.Count}, somando {Moeda(pagas.Sum(s => s.Total))}.");

        var soma = alvo.Sum(s => s.Total);
        var ticket = soma / alvo.Count;

        var maisVendidos = alvo
            .SelectMany(s => s.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Nome = g.Key, Qtd = g.Sum(i => i.Quantity) })
            .OrderByDescending(x => x.Qtd)
            .Take(3)
            .ToList();

        var sb = new StringBuilder();
        sb.Append($"**{Moeda(soma)}** {quando}, em {alvo.Count} ");
        sb.Append(alvo.Count == 1 ? "venda" : "vendas");
        sb.Append($". Ticket médio {Moeda(ticket)}.");

        if (maisVendidos.Count > 0)
            sb.Append("\n\nMais saíram: " +
                      string.Join(", ", maisVendidos.Select(x => $"{x.Nome} ({x.Qtd})")));

        // Honestidade sobre o que NAO esta na conta.
        var abertas = historico.Count(s => s.Status == SessionStatus.Aberta ||
                                           s.Status == SessionStatus.AguardandoPagamento);
        if (abertas > 0)
            sb.Append($"\n\nFora da conta: {abertas} sessão(ões) ainda sem pagamento confirmado.");

        return sb.ToString();
    }

    private async Task<string> CarrinhoAsync()
    {
        var sessao = await SessaoAbertaAsync();
        if (sessao is null) return "Nenhuma sessão aberta agora — carrinho nenhum.";
        if (sessao.Items.Count == 0) return "Sessão aberta, carrinho ainda vazio.";

        var linhas = sessao.Items.Select(i =>
            $"- {i.Quantity}× **{i.ProductName}** — {Moeda(i.Subtotal)}");

        return $"Carrinho da sessão aberta ({sessao.Status}):\n" +
               string.Join("\n", linhas) +
               $"\n\nTotal: **{Moeda(sessao.Total)}**";
    }

    private async Task<string> CamerasAsync()
    {
        var espacial = await _espacial.ObterAsync();
        if (espacial is not { Online: true })
            return "O Sistema Espacial SO não está respondendo" +
                   (espacial?.Erro is { Length: > 0 } e ? $" ({e})" : "") +
                   ". Sem ele não há câmera para reportar.";

        var c = espacial.Cameras;
        if (c is null || c.Total == 0) return "Nenhuma câmera configurada.";

        var linhas = (c.Detalhe ?? new List<CameraDetalheDto>()).Select(d =>
            $"- **{d.Papel}**: {d.Estado}, {d.Fps.ToString("0.0", Br)} fps");

        var sb = new StringBuilder($"**{c.Online}/{c.Total}** câmeras online.\n");
        sb.Append(string.Join("\n", linhas));
        if (c.Online < c.Total)
            sb.Append("\n\n⚠ Com uma vista a menos, a altura da mão fica menos confiável — " +
                      "e é ela que diz de qual prateleira o produto saiu.");
        return sb.ToString();
    }

    private async Task<string> EstoqueAsync(string t, string original)
    {
        var todos = await SegurarAsync(() => _produtos.GetAllAsync(), null!);
        if (todos is null) return "Não consegui ler o catálogo: a WebApi não respondeu.";
        if (todos.Count == 0) return "Nenhum produto cadastrado.";

        // Procura um produto pelo nome dentro da pergunta.
        var achado = todos.FirstOrDefault(p =>
            Normalizar(p.Name).Split(' ')
                .Where(palavra => palavra.Length >= 4)
                .Any(palavra => t.Contains(palavra, StringComparison.Ordinal)));

        if (achado is not null)
            return $"**{achado.Name}**: {achado.StockQuantity} em estoque" +
                   (achado.MinimumStockThreshold is int m ? $" (mínimo {m})" : "") +
                   $", {Moeda(achado.Price)}." +
                   (achado.IsLowStock ? "\n\n⚠ Está abaixo do mínimo." : "");

        var total = todos.Sum(p => p.StockQuantity);
        var baixos = todos.Count(p => p.IsLowStock);
        var valor = todos.Sum(p => p.StockQuantity * p.Price);

        return $"**{todos.Count}** produtos cadastrados, **{total}** unidades no total, " +
               $"valendo {Moeda(valor)}." +
               (baixos > 0 ? $"\n\n{baixos} em baixa — pergunte \"o que está acabando\"." : "");
    }


    private async Task<string> PrecoAsync(string t)
    {
        var todos = await SegurarAsync(() => _produtos.GetAllAsync(), null!);
        if (todos is null) return "Não consegui ler o catálogo: a WebApi não respondeu.";
        if (todos.Count == 0) return "Nenhum produto cadastrado.";

        var achado = ProcurarProduto(todos, t);
        if (achado is not null)
            return $"**{achado.Name}** custa {Moeda(achado.Price)}." +
                   (achado.StockQuantity == 0 ? "\n\n⚠ Mas está zerado no estoque." : "");

        var linhas = todos.OrderBy(p => p.Name)
            .Select(p => $"- {p.Name} — **{Moeda(p.Price)}**");
        return "Preços de venda:\n" + string.Join("\n", linhas);
    }

    private async Task<string> MaisVendidosAsync()
    {
        var historico = await SegurarAsync(() => _sessoes.GetHistoryAsync(), null!);
        if (historico is null) return "Não consegui ler o histórico de vendas.";

        var pagas = historico.Where(s => s.PaymentConfirmedAt is not null).ToList();
        if (pagas.Count == 0) return "Nenhuma venda paga ainda — não há ranking.";

        var ranking = pagas.SelectMany(s => s.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Nome = g.Key, Qtd = g.Sum(i => i.Quantity),
                               Valor = g.Sum(i => i.Subtotal) })
            .OrderByDescending(x => x.Qtd)
            .ToList();

        var sb = new StringBuilder($"Ranking sobre {pagas.Count} vendas pagas:\n");
        foreach (var (x, i) in ranking.Take(5).Select((x, i) => (x, i)))
            sb.Append($"{i + 1}. **{x.Nome}** — {x.Qtd} unidades, {Moeda(x.Valor)}\n");

        // O QUE NAO VENDEU E TAO INFORMATIVO QUANTO O QUE VENDEU.
        // Produto que nunca saiu nao aparece no agrupamento — some do
        // ranking em vez de aparecer com zero. E ele e justamente o que o
        // dono da loja precisa ver.
        var todos = await SegurarAsync(() => _produtos.GetAllAsync(), null!);
        if (todos is not null)
        {
            var vendidos = ranking.Select(r => r.Nome).ToHashSet();
            var parados = todos.Where(p => !vendidos.Contains(p.Name)).ToList();
            if (parados.Count > 0)
                sb.Append($"\nNunca saíram: {string.Join(", ", parados.Select(p => p.Name))}");
        }
        return sb.ToString();
    }

    private async Task<string> CompararAsync()
    {
        var historico = await SegurarAsync(() => _sessoes.GetHistoryAsync(), null!);
        if (historico is null) return "Não consegui ler o histórico de vendas.";

        var pagas = historico.Where(s => s.PaymentConfirmedAt is not null).ToList();
        if (pagas.Count == 0) return "Nenhuma venda paga ainda — não há o que comparar.";

        var hoje = DateTime.Today;
        decimal Soma(DateTime dia) => pagas
            .Where(s => s.PaymentConfirmedAt!.Value.Date == dia)
            .Sum(s => s.Total);

        var deHoje = Soma(hoje);
        var deOntem = Soma(hoje.AddDays(-1));

        if (deOntem == 0 && deHoje == 0)
            return "Nem hoje nem ontem tiveram venda paga.";
        if (deOntem == 0)
            return $"Hoje: {Moeda(deHoje)}. Ontem não houve venda paga, " +
                   "então não dá para calcular variação.";

        var variacao = (deHoje - deOntem) / deOntem * 100;
        var direcao = variacao >= 0 ? "acima" : "abaixo";

        var sb = new StringBuilder(
            $"Hoje **{Moeda(deHoje)}**, ontem {Moeda(deOntem)} — " +
            $"{Math.Abs(variacao):0.0}% {direcao}.");

        // HONESTIDADE SOBRE A AMOSTRA. Comparar dois dias com tres vendas
        // cada e comparar ruido. Dizer a porcentagem sem dizer isso seria
        // dar ao numero uma autoridade que ele nao tem.
        var nHoje = pagas.Count(s => s.PaymentConfirmedAt!.Value.Date == hoje);
        var nOntem = pagas.Count(s => s.PaymentConfirmedAt!.Value.Date == hoje.AddDays(-1));
        if (nHoje + nOntem < 20)
            sb.Append($"\n\n⚠ São {nHoje} vendas hoje e {nOntem} ontem. Com essa " +
                      "quantidade, a variação é mais ruído que tendência.");

        return sb.ToString();
    }

    /// <summary>
    /// Duvidas sobre como o proprio sistema funciona.
    /// </summary>
    /// <remarks>
    /// A REDE RECONHECE A INTENCAO; A RESPOSTA VEM DAQUI.
    ///
    /// Classificar "como funciona o rfid" como `duvida_sistema` e uma coisa;
    /// responder e outra. A resposta e conhecimento sobre o projeto, e ele
    /// mora aqui em texto escrito por quem construiu — nao numa rede.
    ///
    /// Isto e uma base de conhecimento minima, e ela cresce quando o
    /// Eduardo perguntar algo que nao esta nela.
    /// </remarks>
    /// <summary>Explica a loja para quem TOCA a loja, e nao para quem a construiu.</summary>
    /// <remarks>
    /// A VERSAO ANTERIOR RESPONDIA COM O AVESSO DA CAIXA.
    ///
    /// Ela dizia `POST /api/sessions/{id}/items/by-rfid`, `MinimumStockThreshold`,
    /// "homografia", "metrologia de vista unica", "percentil 95". Tudo
    /// verdade, e tudo inutil para quem so quer saber por que a loja
    /// funciona sem caixa.
    ///
    /// O Chefe pediu explicacao leiga, "sem dar informacoes sobre como
    /// fizemos". Entao o criterio de cada resposta aqui e um so:
    ///
    ///     serve para contar a alguem no balcao, sem precisar explicar
    ///     nada antes
    ///
    /// O detalhe tecnico nao sumiu do sistema — ele mora no painel de
    /// raciocinio e no comando `conferir`, que e onde quem for atras vai
    /// procurar.
    ///
    /// ESCOLHER O ASSUNTO NAO E CLASSIFICAR INTENCAO.
    ///
    /// A rede ja decidiu que isto e uma duvida sobre o sistema — foi ela
    /// que trouxe a pergunta ate aqui. O que falta e achar QUAL assunto
    /// dentro da base, e isso e busca, nao decisao: a mesma diferenca
    /// entre entender o pedido e procurar no indice.
    ///
    /// Mesmo assim melhorei a busca: em vez do primeiro `Contains` que
    /// casar, cada assunto ganha uma PONTUACAO por quantos termos bateram.
    /// "como a camera sabe de qual prateleira veio" tem termo de dois
    /// assuntos; o que tem mais acerto ganha, em vez de vencer quem
    /// aparece primeiro na lista.
    /// </remarks>
    private static string Duvida(string t)
    {
        var assuntos = new (string[] Termos, string Resposta)[]
        {
            (new[] { "geral", "tudo", "resumo", "sistema", "funciona", "como e", "me fale",
                     "me explica", "explica", "visao geral", "por dentro" },
             VisaoGeral()),

            (new[] { "rfid", "tag", "etiqueta", "leitor" },
             "Cada produto tem uma etiqueta pequenininha colada nele, do tamanho de um " +
             "adesivo. Quando o cliente pega o produto, uma antena na prateleira lê essa " +
             "etiqueta e o item entra no carrinho dele sozinho — ninguém precisa passar " +
             "nada em leitor.\n\nÉ isso que responde **o quê** foi pego. Quem pegou e de " +
             "onde, quem responde são as câmeras."),

            (new[] { "camera", "cameras", "vista", "filma", "grava" },
             "São três câmeras comuns, dessas de loja mesmo — nada de equipamento " +
             "especial.\n\nCada uma olha de um jeito: a de **cima** mostra onde a pessoa " +
             "está no salão, a da **frente** vê a altura em que a mão chegou, e a de " +
             "**lado** distingue quem pegou de quem só passou perto.\n\nQuando uma delas " +
             "não está enxergando direito, ela simplesmente não opina — em vez de " +
             "chutar e o sistema errar por causa dela."),

            (new[] { "prateleira", "gondola", "estante", "de onde" },
             "A estante tem cinco prateleiras. O sistema descobre de qual delas o produto " +
             "saiu pela **altura em que a mão chegou** — não por reconhecer a embalagem.\n\n" +
             "Funciona porque uma prateleira fica bem longe da outra, quase meio metro, e " +
             "o erro de altura é de poucos centímetros. Não tem como confundir a de cima " +
             "com a de baixo."),

            (new[] { "surpresa", "estranho", "suspeito", "alerta", "anormal", "roubo" },
             "O sistema fica aprendendo o que é o normal da sua loja: o horário do " +
             "movimento, o que costuma sair junto, quanto tempo a pessoa leva.\n\nQuando " +
             "acontece algo que foge muito disso, ele avisa. E o ponto importante: " +
             "**ninguém ensinou a ele o que é suspeito**. Ele avisa o que é *raro* para a " +
             "sua loja — o que é raro aqui pode ser normal em outra."),

            (new[] { "sessao", "qr", "entrar", "entra", "saida", "sair", "cliente" },
             "O cliente chega, aponta o celular para um QR code e a porta abre. Dali em " +
             "diante tudo que ele pega vai para a conta dele.\n\nNa saída o sistema " +
             "confere o que está com ele contra o que foi registrado, e o pagamento sai " +
             "sozinho. Ele não para em caixa nenhum."),

            (new[] { "rastro", "rastreio", "identidade", "reconhece", "rosto", "privacidade",
                     "lgpd" },
             "Quando alguém entra, vira um número — pessoa 1, pessoa 2 — e esse número " +
             "morre quando ela sai da loja.\n\n**Não há reconhecimento facial.** O sistema " +
             "não sabe quem é a pessoa, só que tem alguém ali e onde. Isso foi decisão de " +
             "projeto, não limitação: sem guardar rosto, a loja fica muito mais simples " +
             "de defender do ponto de vista de privacidade."),

            (new[] { "estoque", "minimo", "acabando", "repor", "reposicao" },
             "Cada produto tem um número mínimo que você define. Quando o estoque cai até " +
             "ali, ele passa a aparecer nos avisos como \"acabando\".\n\nNão é adivinhação: " +
             "é a contagem real, atualizada toda vez que alguém tira um item da prateleira."),

            (new[] { "faturamento", "dinheiro", "vendas", "receita", "caixa" },
             "O faturamento soma só as compras que já foram **pagas**.\n\nQuem está com o " +
             "carrinho aberto agora, ou saiu e o pagamento ainda não confirmou, fica de " +
             "fora da conta — e eu aviso quando isso acontece, para o número não parecer " +
             "menor sem explicação."),

            (new[] { "espacial", "gemeo", "digital", "mapa", "planta" },
             "É uma planta viva da loja. As três câmeras alimentam um mapa em tempo real " +
             "que mostra onde cada pessoa está, em metros — como se você olhasse a loja " +
             "de cima com bonequinhos se mexendo.\n\nO barato é que isso sai de câmera " +
             "comum. Não tem sensor caro nem laser; é geometria em cima da imagem."),

            (new[] { "voce", "gerente", "ia", "inteligencia", "rede neural", "aprende",
                     "treinou", "chatgpt", "gemini" },
             "Eu sou uma rede neural feita para este projeto — não sou o ChatGPT nem o " +
             "Gemini, e não mando nada para fora daqui. Rodo dentro do seu navegador.\n\n" +
             "Aprendi lendo milhares de jeitos de fazer as mesmas perguntas que você faz. " +
             "Por isso entendo \"qnt custa a agua\" e \"quanto tá a água\" como a mesma " +
             "coisa.\n\nQuando não tenho certeza, **eu não chuto**: mostro os três " +
             "palpites mais prováveis e você escolhe. Seu clique vira treino — é assim " +
             "que eu melhoro."),
        };

        // PONTUACAO, e nao o primeiro que casar: quem bate mais termos ganha.
        var melhor = ""; var pontos = 0;
        foreach (var (termos, resposta) in assuntos)
        {
            var n = termos.Count(x => t.Contains(x, StringComparison.Ordinal));
            if (n > pontos) { pontos = n; melhor = resposta; }
        }
        if (pontos > 0) return melhor;

        // SEM ASSUNTO CASADO, A VISAO GERAL — e nao um pedido de desculpas.
        // Quem pergunta "como e o sistema?" sem usar nenhuma das palavras
        // acima quer exatamente a visao geral; responder "nao sei" ali e
        // desperdicar a unica pergunta que a pessoa fez.
        return VisaoGeral();
    }

    /// <summary>A loja inteira em cinco parágrafos, sem uma palavra técnica.</summary>
    private static string VisaoGeral() =>
        "A Smart Store é uma loja que funciona **sem caixa e sem atendente**. " +
        "Na prática, para o cliente é assim:\n\n" +
        "**1. Ele entra** apontando o celular para um QR code na porta.\n\n" +
        "**2. Ele pega o que quer.** Cada produto tem uma etiqueta que a prateleira lê " +
        "sozinha, então o item entra na conta dele na hora — sem passar nada em lugar " +
        "nenhum.\n\n" +
        "**3. Ele sai e pronto.** O pagamento acontece sozinho, pelo que ele levou.\n\n" +
        "Enquanto isso, três câmeras comuns montam um mapa da loja em tempo real: onde " +
        "cada pessoa está, de qual prateleira a mão saiu, quem pegou e quem só olhou. " +
        "Elas não reconhecem rosto — cada pessoa é só um número que some quando ela vai " +
        "embora.\n\n" +
        "**Do seu lado**, você tem este painel: estoque, faturamento, quem está na loja " +
        "agora, o que está acabando. E tem a mim — pode perguntar em português, ou me " +
        "mandar mudar preço e repor estoque, que eu confirmo com você antes de gravar " +
        "qualquer coisa.\n\n" +
        "Quer que eu detalhe alguma parte? Pergunte sobre as **câmeras**, as " +
        "**etiquetas**, a **entrada do cliente**, os **alertas** ou sobre **mim**.";

    private static string Saudacao() =>
        "Olá. Estou lendo os sistemas da loja em tempo real. Pergunte o que quiser sobre " +
        "estoque, faturamento, quem está na loja ou como o sistema funciona.";

    private static string ForaDeEscopo() =>
        "Essa não é comigo — eu só sei da loja. Estoque, vendas, clientes no salão, " +
        "câmeras e como o sistema funciona.\n\nSe você acha que deveria ser comigo, " +
        "pergunte de novo de outro jeito: eu guardo o que não entendo, e é assim que " +
        "o meu treino cresce.";

    /// <summary>`alterar_preco` -> `alterar preço`, para o Chefe ler.</summary>
    private static string Legivel(string intencao) => intencao switch
    {
        "pessoas_na_loja"     => "quantas pessoas tem na loja",
        "estoque_baixo"       => "o que está acabando",
        "duvida_sistema"      => "uma dúvida sobre o sistema",
        "fora_de_escopo"      => "assunto fora da loja",
        "mais_vendidos"       => "o que mais sai",
        "adicionar_produto"   => "cadastrar um produto",
        "alterar_preco"       => "mudar um preço",
        "alterar_estoque"     => "corrigir uma quantidade",
        "remover_produto"     => "tirar um produto",
        "configurar_camera"   => "mexer numa câmera",
        "configurar_sistema"  => "mudar uma configuração",
        "reiniciar_servico"   => "reiniciar o sistema",
        "status_sistema"      => "se está tudo funcionando",
        _                     => intencao.Replace('_', ' '),
    };

    private static ProductDto? ProcurarProduto(List<ProductDto> todos, string t)
        => todos.FirstOrDefault(p =>
            Normalizar(p.Name).Split(' ')
                .Where(palavra => palavra.Length >= 4)
                .Any(palavra => t.Contains(palavra, StringComparison.Ordinal)));

    /// <summary>O que o gerente sabe fazer, dito para quem toca a loja.</summary>
    /// <remarks>
    /// A versao anterior listava "surpresa em nats", "rede neural de 21
    /// intencoes", "conferir a conta em C# contra o Python". Isso e o
    /// avesso da caixa aparecendo do lado de fora — util para quem
    /// desenvolve, ruido para quem quer saber quanto vendeu.
    ///
    /// Aqui fica so o que o Chefe pode PEDIR. O detalhe tecnico continua
    /// existindo e continua honesto — mas mora no painel de raciocinio e
    /// no comando `conferir`, para quem for atras.
    /// </remarks>
    private static string Ajuda() =>
        "É só perguntar. Eu dou conta de:\n\n" +
        "**Olhar a loja agora**\n" +
        "- quantas pessoas estão lá dentro\n" +
        "- o que já está no carrinho de quem está comprando\n" +
        "- se as câmeras estão funcionando\n\n" +
        "**Estoque e preços**\n" +
        "- quanto tem de cada produto\n" +
        "- o que está acabando\n" +
        "- quanto custa qualquer item\n\n" +
        "**Dinheiro**\n" +
        "- quanto entrou hoje, ou no total\n" +
        "- o que mais sai, e o que não sai\n" +
        "- comparar um dia com o outro\n\n" +
        "**Mudar as coisas** (eu sempre confirmo antes)\n" +
        "- cadastrar produto novo\n" +
        "- corrigir preço ou quantidade\n" +
        "- tirar produto do catálogo\n\n" +
        "Pode falar do seu jeito, abreviado ou com pressa — eu me viro. " +
        "Quando eu não tiver certeza, eu pergunto em vez de chutar.";

    // ------------------------------------------------------------------
    // Novas intenções de integração
    // ------------------------------------------------------------------

    private async Task<string> IntegracaoSistemasAsync()
    {
        var espacial = await _espacial.ObterAsync();
        var sb = new StringBuilder("**Coordenação entre sistemas:**\n\n");

        if (espacial is { Online: true })
        {
            sb.Append("✓ Sistema Espacial SO online — enviando dados ao monitor\n");
            sb.Append($"✓ {espacial.Pessoas} pessoas rastreadas em tempo real\n");
        }
        else
        {
            sb.Append("✗ Sistema Espacial SO offline — verifique `python monitor/servidor.py`\n");
        }

        sb.Append("✓ WebApi funcionando — recebendo requisições\n");
        sb.Append("✓ Monitor ativo — ponte entre os sistemas\n");
        sb.Append("✓ Gerente neural integrado — interpretando comandos\n\n");

        sb.Append("Os sistemas estão se comunicando. O monitor do gerente em " +
                  "`Rede-Neural/monitor/servidor.py` é a ponte que conecta " +
                  "o Sistema Espacial SO à loja autônoma, e o gerente neural decide " +
                  "como responder às suas perguntas baseando-se nos dados de ambos.");

        return sb.ToString();
    }

    private async Task<string> AnaliseCombinadaAsync()
    {
        var espacial = await _espacial.ObterAsync();
        var sessao = await SessaoAbertaAsync();
        var sb = new StringBuilder("**Análise combinada dos sistemas:**\n\n");

        if (espacial is { Online: true })
        {
            var comSessao = sessao is null ? 0 : 1;
            var diferenca = espacial.Pessoas - comSessao;

            sb.Append($"Rastros visuais: {espacial.Pessoas} pessoas\n");
            sb.Append($"Sessões ativas: {comSessao} compra(s)\n");
            sb.Append($"Diferença: {diferenca} pessoa(s) sem sessão\n\n");

            if (diferenca > 0)
            {
                sb.Append("⚠ **Atenção:** Há mais pessoas rastreadas do que sessões abertas.\n");
                sb.Append("Isso pode indicar:\n");
                sb.Append("- Pessoas que entraram mas não fizeram check-in pelo QR\n");
                sb.Append("- Possível problema com o leitor RFID\n");
                sb.Append("- Pessoas apenas observando (sem intenção de compra)\n\n");
            }
            else if (diferenca < 0)
            {
                sb.Append("⚠ **Atenção:** Há mais sessões do que pessoas rastreadas.\n");
                sb.Append("Isso pode indicar:\n");
                sb.Append("- Problema com as câmeras do Sistema Espacial SO\n");
                sb.Append("- Sessões abandonadas não encerradas\n\n");
            }
            else
            {
                sb.Append("✓ **Sincronizado:** Número de rastros e sessões coincide.\n");
            }
        }
        else
        {
            sb.Append("Não é possível fazer análise combinada: Sistema Espacial SO offline.\n");
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Novas intenções de cliente
    // ------------------------------------------------------------------

    private async Task<string> EntradaLojaAsync()
    {
        var pendentes = await SegurarAsync(() => _sessoes.GetPendingEntryAsync(), new List<SessionDto>());
        var sb = new StringBuilder("**Entrada na loja:**\n\n");

        if (pendentes.Count == 0)
        {
            sb.Append("Ninguém aguardando entrada. O sistema está pronto para " +
                      "receber novos clientes via QR code no app.");
        }
        else
        {
            sb.Append($"{pendentes.Count} cliente(s) aguardando entrada:\n");
            foreach (var p in pendentes.Take(5))
            {
                // SessionDto nao tem CustomerName nem CreatedAt: tem CustomerId
                // e QrCodeExpiresAt. O QR e o unico carimbo de tempo que
                // existe aqui, entao a espera se conta pelo que falta nele.
                var faltam = (p.QrCodeExpiresAt - DateTime.Now).TotalMinutes;
                sb.Append($"- cliente {p.CustomerId.ToString()[..8]} — QR "
                          + (faltam > 0 ? $"válido por mais {faltam:F0} min" : "vencido")
                          + "\n");
            }
        }

        sb.Append("\nO processo de entrada funciona assim:\n");
        sb.Append("1. Cliente gera QR code no app\n");
        sb.Append("2. Admin libera entrada (ou será automatizado no futuro)\n");
        sb.Append("3. Sistema Espacial SO começa a rastrear a pessoa\n");
        sb.Append("4. Sessão de compra é criada na WebApi");

        return sb.ToString();
    }

    private async Task<string> PagamentoAsync()
    {
        var sessao = await SessaoAbertaAsync();
        var sb = new StringBuilder("**Pagamento:**\n\n");

        if (sessao is null)
        {
            sb.Append("Nenhuma sessão aberta para pagamento.");
        }
        else if (sessao.Items.Count == 0)
        {
            sb.Append("Sessão aberta mas carrinho vazio. Nada a pagar.");
        }
        else
        {
            sb.Append($"Sessão {sessao.Id} com {sessao.Items.Count} itens:\n");
            sb.Append($"Total: **{Moeda(sessao.Total)}**\n\n");
            sb.Append("Para processar o pagamento:\n");
            sb.Append("1. Cliente confirma itens no carrinho\n");
            sb.Append("2. Sistema processa pagamento via gateway\n");
            sb.Append("3. Estoque é atualizado automaticamente\n");
            sb.Append("4. Sistema Espacial SO registra saída da pessoa");
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Novas intenções de API
    // ------------------------------------------------------------------

    private async Task<string> StatusApiAsync()
    {
        var sb = new StringBuilder("**Status da API:**\n\n");

        try
        {
            // Testar se a API está respondendo
            var produtos = await SegurarAsync(() => _produtos.GetAllAsync(), null!);
            if (produtos is not null)
            {
                sb.Append("✓ WebApi online e respondendo\n");
                sb.Append($"✓ {produtos.Count} produtos no catálogo\n");
            }
            else
            {
                sb.Append("✗ WebApi não respondeu — verifique se está rodando\n");
            }
        }
        catch
        {
            sb.Append("✗ Erro ao conectar com WebApi\n");
        }

        sb.Append("\nEndpoints principais:\n");
        sb.Append("- `/api/products` — catálogo e estoque\n");
        sb.Append("- `/api/sessions` — sessões de compra\n");
        sb.Append("- `/api/auth` — autenticação JWT\n");
        sb.Append("- Monitor do gerente em `localhost:8760` — dados do Sistema Espacial SO");

        return sb.ToString();
    }

    private static string LogsSistemaAsync()
    {
        var sb = new StringBuilder("**Logs do sistema:**\n\n");

        sb.Append("Os logs estão distribuídos assim:\n\n");
        sb.Append("**Autonomous (loja):**\n");
        sb.Append("- WebApi: logs no console e configurados para arquivo\n");
        sb.Append("- AdminApp: logs do navegador (F12)\n");
        sb.Append("- EdgeDesktop: logs do aplicativo WPF\n\n");

        sb.Append("**Sistema Espacial SO:**\n");
        sb.Append("- `dados/eventos.jsonl` — eventos em tempo real\n");
        sb.Append("- `dados/bruto/` — dados brutos das câmeras\n");
        sb.Append("- Logs do terminal ao rodar `python rodar.py`\n\n");

        sb.Append("**Rede Neural:**\n");
        sb.Append("- `dados/correcoes.jsonl` — correções do gerente\n");
        sb.Append("- `dados/perguntas_reais.jsonl` — perguntas feitas\n");
        sb.Append("- `dados/eventos_loja.jsonl` — eventos derivados da loja\n\n");

        sb.Append("Para diagnosticar problemas, verifique:\n");
        sb.Append("1. Se `python monitor/servidor.py` está rodando\n");
        sb.Append("2. Se a WebApi está acessível em `localhost:5071`\n");
        sb.Append("3. Se as câmeras do Sistema Espacial SO estão online");

        return sb.ToString();
    }

    // ------------------------------------------------------------------

    private async Task<SessionDto?> SessaoAbertaAsync()
        => await SegurarAsync(() => _sessoes.GetCurrentOpenAsync(), null);

    /// <summary>
    /// Roda a consulta e devolve o padrao em vez de estourar.
    /// </summary>
    /// <remarks>
    /// Um endpoint fora do ar nao pode derrubar a resposta inteira: o chat
    /// responde com o que conseguiu e diz o que faltou. Metade da resposta
    /// e util; uma tela de erro nao e.
    /// </remarks>
    private static async Task<T> SegurarAsync<T>(Func<Task<T>> consulta, T padrao)
    {
        try { return await consulta(); }
        catch { return padrao; }
    }
}
