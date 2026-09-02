using System.Text.Json.Serialization;
using System.Net.Http.Json;

namespace AutonomousStore.Gerente.Services.Aprendizado;

public interface IServicoDeAprendizado
{
    bool Pronto { get; }
    int Aceitas { get; }
    int Recusadas { get; }
    double AcertoNaGuarda { get; }

    Task<bool> PrepararAsync();
    Task<Aprendiz.Veredito> EnsinarAsync(string pergunta, string intencao, double confiancaDoPalpite,
                                         string palpite, PerfilDeQuemFala quemEnsinou);
    Task<bool> DevolverAoPythonAsync();
    void Reiniciar();
}

/// <summary>
/// O interruptor: é esta classe que liga o aprendizado ao chat e ao Python.
/// </summary>
/// <remarks>
/// AS PEÇAS JÁ EXISTIAM SOLTAS. O gradiente estava provado, o passo de treino
/// escrito, a trava medida, e a ponte HTTP testada — e nada disso acontecia
/// quando o Chefe clicava num botão, porque ninguém chamava ninguém.
///
/// APRENDE SEMPRE, SINCRONIZA QUANDO DÁ.
///
/// O monitor em Python só está no ar quando alguém o liga. Se o aprendizado
/// dependesse dele, uma tarde com o monitor desligado seria uma tarde de
/// correções perdidas — e o Chefe não teria como saber. Então o passo de
/// gradiente acontece SEMPRE, no navegador, e o envio ao Python é uma
/// tentativa que pode falhar em silêncio.
///
/// O QUE AINDA NÃO ESTÁ AQUI: gravar no banco. Enquanto isso não existir, o
/// que a loja aprendeu vive na memória da aba e morre no F5. O envio ao
/// Python é o que salva — mas só quando o monitor está ligado. Está escrito
/// para ninguém descobrir isso por acidente.
/// </remarks>
public class ServicoDeAprendizado : IServicoDeAprendizado
{
    private readonly IClassificadorDeIntencao _classificador;
    private readonly IHttpClientFactory _fabrica;

    private Aprendiz? _aprendiz;
    private Dictionary<string, int>? _porNome;

    public ServicoDeAprendizado(IClassificadorDeIntencao classificador, IHttpClientFactory fabrica)
    {
        _classificador = classificador;
        _fabrica = fabrica;
    }

    public bool Pronto => _aprendiz is not null;
    public int Aceitas => _aprendiz?.Aceitas ?? 0;
    public int Recusadas => _aprendiz?.Recusadas ?? 0;
    public double AcertoNaGuarda => _aprendiz?.AcertoNaGuarda() ?? 0;

    /// <summary>Monta a rede treinável e o conjunto de guarda. Uma vez só.</summary>
    public async Task<bool> PrepararAsync()
    {
        if (_aprendiz is not null) return true;
        if (!await _classificador.CarregarAsync()) return false;

        var m = _classificador.Modelo;
        if (m is null || m.Camadas.Count < 2) return false;

        _porNome = m.Intencoes.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);

        var baseDoPython = new RedeTreinavel(
            m.Tabela.Select(l => l.ToArray()).ToArray(),
            m.Camadas[0].Pesos.Select(l => l.ToArray()).ToArray(),
            m.Camadas[0].Vies.ToArray(),
            m.Camadas[1].Pesos.Select(l => l.ToArray()).ToArray(),
            m.Camadas[1].Vies.ToArray());

        // ── a guarda que o Python gerou ───────────────────────────────
        //
        // SEM GUARDA NÃO HÁ APRENDIZADO. Ela é o que separa "aprendeu" de
        // "estragou": medimos que ensinar uma frase sem trava quebra outras
        // quatorze. Preferir aprender sem rede de proteção seria escolher o
        // pior dos dois mundos — a loja mudaria de opinião e ninguém saberia
        // se para melhor.
        List<RedeTreinavel.Exemplo> guarda;
        try
        {
            var http = _fabrica.CreateClient(ClassificadorDeIntencao.ClienteEstatico);
            var cru = await http.GetFromJsonAsync<List<FraseDeGuarda>>(
                "_content/AutonomousStore.Gerente/modelos/guarda.json");

            guarda = (cru ?? [])
                .Where(f => _porNome.ContainsKey(f.Intencao))
                .Select(f => new RedeTreinavel.Exemplo(
                    _classificador.PecasDe(f.Pergunta), _porNome[f.Intencao]))
                .Where(e => e.Indices.Count > 0)
                .ToList();
        }
        catch { return false; }

        if (guarda.Count == 0) return false;

        _aprendiz = new Aprendiz(baseDoPython, guarda);
        return true;
    }

    /// <summary>Alguém corrigiu. É aqui que a rede aprende.</summary>
    /// <remarks>
    /// QUEM ENSINOU VAI JUNTO, E ISSO NÃO É BUROCRACIA.
    ///
    /// O Eduardo decidiu que o cliente também ensina — a correção dele vale
    /// tanto quanto a do Chefe, porque é ele quem usa as palavras que a loja
    /// vai ouvir de verdade. Mas "vale igual" e "some no meio" são coisas
    /// diferentes: se um dia a rede começar a errar bonito, a primeira
    /// pergunta vai ser "isso veio de onde?", e sem a origem no arquivo não
    /// existe resposta — nem como desfazer só um lado.
    /// </remarks>
    public async Task<Aprendiz.Veredito> EnsinarAsync(
        string pergunta, string intencao, double confiancaDoPalpite, string palpite,
        PerfilDeQuemFala quemEnsinou)
    {
        if (!await PrepararAsync() || _aprendiz is null || _porNome is null)
            return new Aprendiz.Veredito(Aprendiz.Resultado.Recusado, 0, 0, 0, 0, 0);

        if (!_porNome.TryGetValue(intencao, out var certa))
            return new Aprendiz.Veredito(Aprendiz.Resultado.Recusado, 0, 0, 0, 0, 0);

        // NÃO SE ENSINA O QUE NÃO SE PODE PERGUNTAR.
        //
        // Os botões já saem filtrados pelo perfil, então hoje ninguém chega
        // aqui com uma intenção proibida. Mas ensinar é escrever no cérebro
        // da loja: se um caminho novo aparecer amanhã, o cliente estaria
        // empurrando `faturamento` para dentro da rede — e a barreira do
        // GerenteService, que só filtra a RESPOSTA, não veria nada.
        if (!quemEnsinou.Pode(intencao))
            return new Aprendiz.Veredito(Aprendiz.Resultado.Recusado, 0, 0, 0, 0, 0);

        var indices = _classificador.PecasDe(pergunta);
        var v = _aprendiz.Ensinar(indices, certa);

        // A rede que responde passa a ser a que aprendeu. Sem isto o Chefe
        // ensinaria e o chat continuaria errando a mesma frase — que é a
        // forma mais rápida de ele parar de clicar.
        if (v.Mudou) _classificador.UsarRede(_aprendiz.Atual);

        await AvisarOPythonAsync(pergunta, palpite, confiancaDoPalpite, intencao, v, quemEnsinou);
        return v;
    }

    /// <summary>Manda a correção para o `correcoes.jsonl` do Rede-Neural.</summary>
    /// <remarks>
    /// A rota `/api/correcao` existe no monitor desde sempre e nunca tinha
    /// sido chamada — o arquivo passou de 30 de agosto até hoje com zero
    /// bytes. Cada clique que se perdeu ali era o dado mais caro do projeto:
    /// eu posso escrever mil frases de treino, mas só o Chefe sabe o que ELE
    /// quis dizer.
    ///
    /// Engole a falha de propósito: o monitor desligado não pode impedir a
    /// loja de aprender.
    /// </remarks>
    private async Task AvisarOPythonAsync(
        string pergunta, string palpite, double confianca, string escolhida, Aprendiz.Veredito v,
        PerfilDeQuemFala quemEnsinou)
    {
        try
        {
            var http = _fabrica.CreateClient("MonitorGerente");
            await http.PostAsJsonAsync("api/correcao", new
            {
                pergunta,
                palpite,
                confianca,
                escolhida,
                aceita = v.Mudou,
                resultado = v.Resultado.ToString(),
                taxa = v.TaxaUsada,
                acerto_guarda_antes = v.AcertoAntes,
                acerto_guarda_depois = v.AcertoDepois,
                // "AutonomousStore" estava escrito na mão aqui, e era a mesma
                // palavra para o dono e para o freguês. Agora o arquivo separa
                // AdminApp de ClientApp: dá para filtrar, contar, e jogar fora
                // um lado sem perder o outro.
                origem = quemEnsinou.Origem,
                pode_escrever = quemEnsinou.PodeEscrever,
            });
        }
        catch { /* monitor fora do ar: a loja aprendeu do mesmo jeito */ }
    }

    /// <summary>Devolve ao Python o modelo que a loja aprendeu.</summary>
    /// <remarks>
    /// A outra metade da ponte. O Python recebe os pesos e o boletim do que
    /// mudou, e pode recalibrar o limiar com o corpus inteiro — coisa que o
    /// navegador não tem como fazer.
    ///
    /// O `medido` do Python vai junto, INTACTO. Ele descreve a validação
    /// cruzada de lá e continua verdadeiro sobre ela; o que a loja aprendeu
    /// depois entra num bloco separado. Número sem data é armadilha; número
    /// com data é informação.
    /// </remarks>
    public async Task<bool> DevolverAoPythonAsync()
    {
        if (_aprendiz is null || _classificador.Modelo is null) return false;

        var m = _classificador.Modelo;
        var r = _aprendiz.Atual;

        var pacote = new Dictionary<string, object?>
        {
            ["intencoes"] = m.Intencoes,
            ["pecas"] = m.Pecas,
            ["dimensao"] = m.Dimensao,
            ["tabela"] = r.Tabela,
            ["camadas"] = new object[]
            {
                new { ativacao = m.Camadas[0].Ativacao, pesos = r.TroncoPesos, vies = r.TroncoVies },
                new { ativacao = m.Camadas[1].Ativacao, pesos = r.IntencaoPesos, vies = r.IntencaoVies },
            },
            // O tom vai como veio: a loja não o treina, então devolvê-lo
            // alterado seria mentir sobre o que aconteceu aqui.
            ["tons"] = m.Tons,
            ["camada_tom"] = m.CamadaTom,
            ["limiar"] = m.Limiar,
            ["limiar_confirmacao"] = m.LimiarConfirmacao,
            ["botoes"] = m.Botoes,
            ["medido"] = m.Medido,
            ["aprendido_na_loja"] = new
            {
                correcoes_aceitas = _aprendiz.Aceitas,
                correcoes_recusadas = _aprendiz.Recusadas,
                acerto_na_guarda = _aprendiz.AcertoNaGuarda(),
                distancia_da_base = _aprendiz.DistanciaDaBase(),
                em = DateTime.UtcNow.ToString("O"),
            },
        };

        try
        {
            var http = _fabrica.CreateClient("MonitorGerente");
            var resposta = await http.PostAsJsonAsync("api/modelo", pacote);
            return resposta.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Reiniciar()
    {
        _aprendiz?.Reiniciar();
        if (_aprendiz is not null) _classificador.UsarRede(_aprendiz.Atual);
    }

    private sealed record FraseDeGuarda(
        [property: JsonPropertyName("pergunta")] string Pergunta,
        [property: JsonPropertyName("intencao")] string Intencao,
        [property: JsonPropertyName("base")] string? Base);
}
