using System.Text.Json.Serialization;

namespace AutonomousStore.AdminApp.Services.Agente;

/// <summary>
/// Camada de personalidade adaptativa do agente autônomo.
/// </summary>
/// <remarks>
/// PERSONALIDADE DO AGENTE
///
/// O agente não é um robô sem alma. Ele tem:
/// - Nome próprio e referência respeitosa ao Chefe
/// - Tom de voz adaptativo ao contexto
/// - Capacidade de humor contextual
/// - Compreensão emocional básica
/// - Empatia nas respostas
///
/// O objetivo é criar uma interação natural e humana, mantendo
/// profissionalismo quando necessário.
/// </remarks>
public enum TomDeVoz
{
    [JsonPropertyName("profissional")]
    Profissional,
    
    [JsonPropertyName("amigavel")]
    Amigavel,
    
    [JsonPropertyName("formal")]
    Formal,
    
    [JsonPropertyName("descontraido")]
    Descontraido,
    
    [JsonPropertyName("serio")]
    Serio,
    
    [JsonPropertyName("empatico")]
    Empatico
}

/// <summary>
/// Emoções detectadas na comunicação do Chefe.
/// </summary>
public enum EmocaoDetetada
{
    [JsonPropertyName("neutro")]
    Neutro,
    
    [JsonPropertyName("feliz")]
    Feliz,
    
    [JsonPropertyName("frustrado")]
    Frustrado,
    
    [JsonPropertyName("irritado")]
    Irritado,
    
    [JsonPropertyName("preocupado")]
    Preocupado,
    
    [JsonPropertyName("urgente")]
    Urgente,
    
    [JsonPropertyName("confuso")]
    Confuso,
    
    [JsonPropertyName("satisfeito")]
    Satisfeito
}

/// <summary>
/// Contexto da conversa atual.
/// </summary>
public record ContextoDeConversa(
    [property: JsonPropertyName("ultima_interacao")] DateTime UltimaInteracao,
    [property: JsonPropertyName("emocao_atual")] EmocaoDetetada EmocaoAtual,
    [property: JsonPropertyName("tom_atual")] TomDeVoz TomAtual,
    [property: JsonPropertyName("historico_recente")] List<string> HistoricoRecente,
    [property: JsonPropertyName("sucessos_recentes")] int SucessosRecentes,
    [property: JsonPropertyName("falhas_recentes")] int FalhasRecentes
)
{
    public ContextoDeConversa() : this(
        DateTime.Now,
        EmocaoDetetada.Neutro,
        TomDeVoz.Profissional,
        new List<string>(),
        0,
        0
    ) { }
    
    /// <summary>
    /// Adiciona uma interação ao histórico.
    /// </summary>
    public ContextoDeConversa AdicionarInteracao(string interacao, bool sucesso = true)
    {
        var novoHistorico = new List<string>(HistoricoRecente);
        novoHistorico.Add(interacao);
        
        // Mantém apenas as últimas 10 interações
        if (novoHistorico.Count > 10)
            novoHistorico = novoHistorico.TakeLast(10).ToList();
        
        return this with
        {
            UltimaInteracao = DateTime.Now,
            HistoricoRecente = novoHistorico,
            SucessosRecentes = sucesso ? SucessosRecentes + 1 : SucessosRecentes,
            FalhasRecentes = sucesso ? FalhasRecentes : FalhasRecentes + 1
        };
    }
    
    /// <summary>
    /// Atualiza a emoção detectada.
    /// </summary>
    public ContextoDeConversa AtualizarEmocao(EmocaoDetetada novaEmocao)
    {
        return this with { EmocaoAtual = novaEmocao };
    }
    
    /// <summary>
    /// Atualiza o tom de voz baseado no contexto.
    /// </summary>
    public ContextoDeConversa AtualizarTom(TomDeVoz novoTom)
    {
        return this with { TomAtual = novoTom };
    }
}

/// <summary>
/// Detector de emoção básico baseado em análise de texto.
/// </summary>
public class DetectorDeEmocao
{
    private readonly Dictionary<string, EmocaoDetetada> _palavrasChave;
    
    public DetectorDeEmocao()
    {
        _palavrasChave = new Dictionary<string, EmocaoDetetada>(StringComparer.OrdinalIgnoreCase)
        {
            // Frustração/irritação
            ["que chato"] = EmocaoDetetada.Frustrado,
            ["irritante"] = EmocaoDetetada.Irritado,
            ["não funciona"] = EmocaoDetetada.Frustrado,
            ["erro de novo"] = EmocaoDetetada.Irritado,
            ["cansado"] = EmocaoDetetada.Frustrado,
            ["pior"] = EmocaoDetetada.Irritado,
            
            // Urgência
            ["urgente"] = EmocaoDetetada.Urgente,
            ["rápido"] = EmocaoDetetada.Urgente,
            ["imediatamente"] = EmocaoDetetada.Urgente,
            ["agora"] = EmocaoDetetada.Urgente,
            ["pressa"] = EmocaoDetetada.Urgente,
            
            // Preocupação
            ["preocupado"] = EmocaoDetetada.Preocupado,
            ["medo"] = EmocaoDetetada.Preocupado,
            ["problema"] = EmocaoDetetada.Preocupado,
            ["errado"] = EmocaoDetetada.Preocupado,
            
            // Confusão
            ["não entendi"] = EmocaoDetetada.Confuso,
            ["confuso"] = EmocaoDetetada.Confuso,
            ["como fazer"] = EmocaoDetetada.Confuso,
            ["não sei"] = EmocaoDetetada.Confuso,
            
            // Satisfação/felicidade
            ["bom"] = EmocaoDetetada.Feliz,
            ["ótimo"] = EmocaoDetetada.Satisfeito,
            ["perfeito"] = EmocaoDetetada.Satisfeito,
            ["excelente"] = EmocaoDetetada.Satisfeito,
            ["funcionou"] = EmocaoDetetada.Feliz,
            ["obrigado"] = EmocaoDetetada.Satisfeito,
            ["valeu"] = EmocaoDetetada.Feliz
        };
    }
    
    /// <summary>
    /// Detecta a emoção baseada no texto.
    /// </summary>
    public EmocaoDetetada Detectar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return EmocaoDetetada.Neutro;
        
        var textoLower = texto.ToLower();
        
        // Verifica palavras-chave
        foreach (var (palavra, emocao) in _palavrasChave)
        {
            if (textoLower.Contains(palavra))
                return emocao;
        }
        
        // Análise de pontuação e intensidade
        if (texto.Contains("!!!") || texto.Contains("?"))
            return EmocaoDetetada.Urgente;
        
        if (texto.EndsWith("?") && texto.Length < 20)
            return EmocaoDetetada.Confuso;
        
        return EmocaoDetetada.Neutro;
    }
    
    /// <summary>
    /// Adiciona uma nova palavra-chave para detecção.
    /// </summary>
    public void AdicionarPalavraChave(string palavra, EmocaoDetetada emocao)
    {
        _palavrasChave[palavra.ToLower()] = emocao;
    }
}

/// <summary>
/// Gerenciador de personalidade do agente.
/// </summary>
public class PersonalidadeDoAgente
{
    private readonly DetectorDeEmocao _detector;
    private readonly Dictionary<TomDeVoz, Dictionary<EmocaoDetetada, TomDeVoz>> _adaptacoes;
    
    public string Nome { get; set; } = "Gerente";
    public string ReferenciaAoChefe { get; set; } = "Chefe";
    public ContextoDeConversa ContextoAtual { get; private set; }
    
    public PersonalidadeDoAgente()
    {
        _detector = new DetectorDeEmocao();
        ContextoAtual = new ContextoDeConversa();
        
        // Configura adaptações de tom baseadas na emoção
        _adaptacoes = new Dictionary<TomDeVoz, Dictionary<EmocaoDetetada, TomDeVoz>>
        {
            [TomDeVoz.Profissional] = new Dictionary<EmocaoDetetada, TomDeVoz>
            {
                [EmocaoDetetada.Frustrado] = TomDeVoz.Empatico,
                [EmocaoDetetada.Irritado] = TomDeVoz.Empatico,
                [EmocaoDetetada.Urgente] = TomDeVoz.Serio,
                [EmocaoDetetada.Preocupado] = TomDeVoz.Empatico,
                [EmocaoDetetada.Confuso] = TomDeVoz.Amigavel,
                [EmocaoDetetada.Feliz] = TomDeVoz.Descontraido,
                [EmocaoDetetada.Satisfeito] = TomDeVoz.Descontraido
            },
            [TomDeVoz.Amigavel] = new Dictionary<EmocaoDetetada, TomDeVoz>
            {
                [EmocaoDetetada.Frustrado] = TomDeVoz.Empatico,
                [EmocaoDetetada.Irritado] = TomDeVoz.Empatico,
                [EmocaoDetetada.Urgente] = TomDeVoz.Serio,
                [EmocaoDetetada.Preocupado] = TomDeVoz.Empatico
            }
        };
    }
    
    /// <summary>
    /// Processa uma entrada do Chefe e atualiza o contexto.
    /// </summary>
    public ContextoDeConversa ProcessarEntrada(string entrada, bool sucesso = true)
    {
        var emocao = _detector.Detectar(entrada);
        var novoContexto = ContextoAtual
            .AtualizarEmocao(emocao)
            .AdicionarInteracao(entrada, sucesso);
        
        // Adapta o tom baseado na emoção
        novoContexto = novoContexto.AtualizarTom(AdaptarTom(novoContexto.TomAtual, emocao));
        
        ContextoAtual = novoContexto;
        return ContextoAtual;
    }
    
    /// <summary>
    /// Adapta o tom de voz baseado na emoção detectada.
    /// </summary>
    private TomDeVoz AdaptarTom(TomDeVoz tomAtual, EmocaoDetetada emocao)
    {
        if (_adaptacoes.TryGetValue(tomAtual, out var adaptacoes))
        {
            if (adaptacoes.TryGetValue(emocao, out var novoTom))
                return novoTom;
        }
        
        return tomAtual;
    }
    
    /// <summary>
    /// Adapta uma resposta baseada na personalidade atual.
    /// </summary>
    public string AdaptarResposta(string respostaBase, bool adicionarHumor = false)
    {
        var resposta = respostaBase;
        
        // NAO trocar "voce" por "Chefe" no meio da frase.
        //
        // O codigo antigo fazia `resposta.Replace("você", "Chefe")` em toda
        // resposta que ainda nao citasse o Chefe. O resultado era portugues
        // quebrado:
        //
        //     "quanto você quer cadastrar"  ->  "quanto Chefe quer cadastrar"
        //     "se você preferir"            ->  "se Chefe preferir"
        //
        // "Voce" e pronome e ocupa o lugar de sujeito ou objeto; "Chefe" e
        // vocativo e so cabe no comeco ou entre virgulas. Trocar um pelo
        // outro por busca e substituicao nao respeita isso.
        //
        // Quem decide o tratamento agora e o `ComoChefe` do GerenteService,
        // que o poe na frente da frase e so de vez em quando.
        
        // Adapta o tom baseado na emoção atual
        resposta = AdaptarAoTom(resposta, ContextoAtual.TomAtual);
        
        // Adiciona humor quando apropriado
        if (adicionarHumor && PodeUsarHumor())
        {
            resposta = AdicionarHumorContextual(resposta);
        }
        
        // Adiciona empatia quando o Chefe está frustrado
        if (ContextoAtual.EmocaoAtual == EmocaoDetetada.Frustrado || 
            ContextoAtual.EmocaoAtual == EmocaoDetetada.Irritado)
        {
            resposta = AdicionarEmpatia(resposta);
        }
        
        return resposta;
    }
    
    /// <summary>
    /// Adapta a resposta ao tom de voz atual.
    /// </summary>
    private string AdaptarAoTom(string resposta, TomDeVoz tom)
    {
        return tom switch
        {
            TomDeVoz.Profissional => resposta,
            TomDeVoz.Amigavel => AdicionarToqueAmigavel(resposta),
            TomDeVoz.Formal => AdicionarToqueFormal(resposta),
            TomDeVoz.Descontraido => AdicionarToqueDescontraido(resposta),
            TomDeVoz.Serio => AdicionarToqueSerio(resposta),
            TomDeVoz.Empatico => AdicionarToqueEmpatico(resposta),
            _ => resposta
        };
    }
    
    /// <summary>
    /// Determina se é apropriado usar humor.
    /// </summary>
    private bool PodeUsarHumor()
    {
        // Não usar humor em situações sérias
        if (ContextoAtual.EmocaoAtual == EmocaoDetetada.Urgente ||
            ContextoAtual.EmocaoAtual == EmocaoDetetada.Preocupado ||
            ContextoAtual.EmocaoAtual == EmocaoDetetada.Irritado)
            return false;
        
        // Usar humor se o Chefe está satisfeito ou feliz
        if (ContextoAtual.EmocaoAtual == EmocaoDetetada.Feliz ||
            ContextoAtual.EmocaoAtual == EmocaoDetetada.Satisfeito)
            return true;
        
        // Usar humor ocasionalmente em situações neutras
        return ContextoAtual.EmocaoAtual == EmocaoDetetada.Neutro && 
               ContextoAtual.SucessosRecentes > 2;
    }
    
    /// <summary>
    /// Adiciona toque amigável à resposta.
    /// </summary>
    private string AdicionarToqueAmigavel(string resposta)
    {
        if (!resposta.EndsWith("!") && !resposta.EndsWith("."))
            resposta += "!";
        
        return resposta;
    }
    
    /// <summary>
    /// Adiciona toque formal à resposta.
    /// </summary>
    private string AdicionarToqueFormal(string resposta)
    {
        return resposta.Replace("!", ".").Replace(" tá", " está");
    }
    
    /// <summary>
    /// Adiciona toque descontraído à resposta.
    /// </summary>
    private string AdicionarToqueDescontraido(string resposta)
    {
        return resposta.Replace("está", "tá").Replace("estou", "tô");
    }
    
    /// <summary>
    /// Adiciona toque sério à resposta.
    /// </summary>
    private string AdicionarToqueSerio(string resposta)
    {
        if (!resposta.Contains("⚠"))
            resposta = "⚠ " + resposta;
        
        return resposta;
    }
    
    /// <summary>
    /// Adiciona toque empático à resposta.
    /// </summary>
    private string AdicionarToqueEmpatico(string resposta)
    {
        // sem vocativo: a empatia ja esta no "Entendo", e repetir o
        // tratamento em toda resposta empatica soa bajulador
        return $"Entendo. {resposta}";
    }
    
    /// <summary>
    /// Adiciona humor contextual à resposta.
    /// </summary>
    private string AdicionarHumorContextual(string resposta)
    {
        var emojis = new[] { "😊", "👍", "✨", "🚀", "💪", "🎯" };
        var random = new Random();
        var emoji = emojis[random.Next(emojis.Length)];
        
        return $"{resposta} {emoji}";
    }
    
    /// <summary>
    /// Adiciona empatia à resposta.
    /// </summary>
    private string AdicionarEmpatia(string resposta)
    {
        return $"Sinto muito. {resposta} Vamos resolver isso juntos.";
    }
    
    /// <summary>
    /// Reinicia o contexto da conversa.
    /// </summary>
    public void ReiniciarContexto()
    {
        ContextoAtual = new ContextoDeConversa();
    }
}