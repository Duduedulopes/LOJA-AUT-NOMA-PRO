using System.Text.Json.Serialization;

namespace AutonomousStore.AdminApp.Services.Agente;

/// <summary>
/// Sistema de loop de feedback inteligente para clarificação de dúvidas.
/// </summary>
/// <remarks>
/// LOOP DE FEEDBACK INTELIGENTE
///
/// O agente não adivinha. Quando não entende ou precisa de mais informações,
/// ele entra em um ciclo de diálogo até esclarecer a dúvida completamente.
///
/// MECANISMO:
/// 1. Detecta informação faltante
/// 2. Formula pergunta específica
/// 3. Aguarda resposta do Chefe
/// 4. Analisa se a resposta esclareceu
/// 5. Se não, reformula de outra forma
/// 6. Se sim, continua com a operação
/// </remarks>
public enum EstadoDoDialogo
{
    [JsonPropertyName("aguardando_informacao")]
    AguardandoInformacao,
    
    [JsonPropertyName("processando")]
    Processando,
    
    [JsonPropertyName("confirmacao")]
    Confirmacao,
    
    [JsonPropertyName("completo")]
    Completo,
    
    [JsonPropertyName("cancelado")]
    Cancelado
}

/// <summary>
/// Pergunta pendente no diálogo.
/// </summary>
public record PerguntaPendente(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("pergunta")] string Pergunta,
    [property: JsonPropertyName("campo_alvo")] string CampoAlvo,
    [property: JsonPropertyName("tipo_esperado")] string TipoEsperado,
    [property: JsonPropertyName("opcional")] bool Opcional,
    [property: JsonPropertyName("tentativas")] int Tentativas,
    [property: JsonPropertyName("criada_em")] DateTime CriadaEm
)
{
    public PerguntaPendente(string pergunta, string campoAlvo, string tipoEsperado, bool opcional = false)
        : this(Guid.NewGuid(), pergunta, campoAlvo, tipoEsperado, opcional, 0, DateTime.Now) { }
    
    /// <summary>
    /// Incrementa o contador de tentativas.
    /// </summary>
    public PerguntaPendente NovaTentativa()
    {
        return this with { Tentativas = Tentativas + 1 };
    }
    
    /// <summary>
    /// Determina se deve desistir após muitas tentativas.
    /// </summary>
    public bool DeveDesistir()
    {
        return Tentativas >= 3;
    }
}

/// <summary>
/// Contexto do diálogo atual.
/// </summary>
public record ContextoDoDialogo(
    [property: JsonPropertyName("estado")] EstadoDoDialogo Estado,
    [property: JsonPropertyName("perguntas_pendentes")] List<PerguntaPendente> PerguntasPendentes,
    [property: JsonPropertyName("informacoes_coletadas")] Dictionary<string, object> InformacoesColetadas,
    [property: JsonPropertyName("operacao_alvo")] string OperacaoAlvo,
    [property: JsonPropertyName("iniciado_em")] DateTime IniciadoEm
)
{
    public ContextoDoDialogo(string operacaoAlvo)
        : this(EstadoDoDialogo.AguardandoInformacao, new List<PerguntaPendente>(), 
              new Dictionary<string, object>(), operacaoAlvo, DateTime.Now) { }
    
    /// <summary>
    /// Adiciona uma pergunta pendente.
    /// </summary>
    public ContextoDoDialogo AdicionarPergunta(PerguntaPendente pergunta)
    {
        var novasPerguntas = new List<PerguntaPendente>(PerguntasPendentes);
        novasPerguntas.Add(pergunta);
        
        return this with { PerguntasPendentes = novasPerguntas };
    }
    
    /// <summary>
    /// Remove uma pergunta pendente.
    /// </summary>
    public ContextoDoDialogo RemoverPergunta(Guid perguntaId)
    {
        var novasPerguntas = PerguntasPendentes.Where(p => p.Id != perguntaId).ToList();
        return this with { PerguntasPendentes = novasPerguntas };
    }
    
    /// <summary>
    /// Adiciona uma informação coletada.
    /// </summary>
    public ContextoDoDialogo AdicionarInformacao(string campo, object valor)
    {
        var novasInfos = new Dictionary<string, object>(InformacoesColetadas);
        novasInfos[campo] = valor;
        
        return this with { InformacoesColetadas = novasInfos };
    }
    
    /// <summary>
    /// Atualiza o estado do diálogo.
    /// </summary>
    public ContextoDoDialogo AtualizarEstado(EstadoDoDialogo novoEstado)
    {
        return this with { Estado = novoEstado };
    }
    
    /// <summary>
    /// Determina se todas as informações necessárias foram coletadas.
    /// </summary>
    public bool InformacoesCompletas()
    {
        return PerguntasPendentes.All(p => p.Opcional || InformacoesColetadas.ContainsKey(p.CampoAlvo));
    }
    
    /// <summary>
    /// Obtém a próxima pergunta a ser feita.
    /// </summary>
    public PerguntaPendente? ProximaPergunta()
    {
        return PerguntasPendentes.FirstOrDefault(p => !InformacoesColetadas.ContainsKey(p.CampoAlvo));
    }
}

/// <summary>
/// Gerenciador de loop de feedback inteligente.
/// </summary>
public class GerenciadorDeFeedback
{
    private ContextoDoDialogo? _contextoAtual;
    private readonly List<ContextoDoDialogo> _historico;
    
    public GerenciadorDeFeedback()
    {
        _historico = new List<ContextoDoDialogo>();
    }
    
    /// <summary>
    /// Inicia um novo diálogo para coletar informações.
    /// </summary>
    public ContextoDoDialogo IniciarDialogo(string operacao, List<PerguntaPendente> perguntasNecessarias)
    {
        _contextoAtual = new ContextoDoDialogo(operacao);
        
        foreach (var pergunta in perguntasNecessarias)
        {
            _contextoAtual = _contextoAtual.AdicionarPergunta(pergunta);
        }
        
        return _contextoAtual;
    }
    
    /// <summary>
    /// Processa a resposta do Chefe a uma pergunta.
    /// </summary>
    public (ContextoDoDialogo Contexto, string Mensagem) ProcessarResposta(string resposta)
    {
        if (_contextoAtual is null)
            throw new InvalidOperationException("Nenhum diálogo ativo.");
        
        var proximaPergunta = _contextoAtual.ProximaPergunta();
        if (proximaPergunta is null)
        {
            _contextoAtual = _contextoAtual.AtualizarEstado(EstadoDoDialogo.Completo);
            return (_contextoAtual, "Todas as informações foram coletadas. Posso prosseguir?");
        }
        
        // Tenta processar a resposta
        var (sucesso, valor, mensagem) = ProcessarValor(resposta, proximaPergunta.TipoEsperado);
        
        if (sucesso && valor is not null)
        {
            _contextoAtual = _contextoAtual
                .AdicionarInformacao(proximaPergunta.CampoAlvo, valor)
                .RemoverPergunta(proximaPergunta.Id);
            
            if (_contextoAtual.InformacoesCompletas())
            {
                _contextoAtual = _contextoAtual.AtualizarEstado(EstadoDoDialogo.Confirmacao);
                return (_contextoAtual, "Perfeito! Vou resumir o que entendi. Isso está correto?");
            }
            
            var novaPergunta = _contextoAtual.ProximaPergunta();
            if (novaPergunta is not null)
            {
                return (_contextoAtual, novaPergunta.Pergunta);
            }
        }
        else
        {
            // Reformula a pergunta de outra forma
            var reformulada = proximaPergunta.NovaTentativa();
            
            if (reformulada.DeveDesistir())
            {
                _contextoAtual = _contextoAtual.AtualizarEstado(EstadoDoDialogo.Cancelado);
                return (_contextoAtual, $"Não consegui entender, {ReferenciaAoChefe()}. Vamos tentar de outra forma ou prefere cancelar?");
            }
            
            _contextoAtual = _contextoAtual
                .RemoverPergunta(proximaPergunta.Id)
                .AdicionarPergunta(reformulada);
            
            return (_contextoAtual, ReformularPergunta(reformulada));
        }
        
        return (_contextoAtual, mensagem);
    }
    
    /// <summary>
    /// Processa e valida um valor baseado no tipo esperado.
    /// </summary>
    private (bool Sucesso, object? Valor, string Mensagem) ProcessarValor(string resposta, string tipoEsperado)
    {
        return tipoEsperado.ToLower() switch
        {
            "texto" or "string" => (true, resposta.Trim(), ""),
            "numero" or "decimal" or "preco" => ProcessarDecimal(resposta),
            "inteiro" or "quantidade" => ProcessarInteiro(resposta),
            "booleano" or "sim_nao" => ProcessarBooleano(resposta),
            "ip" => ProcessarIP(resposta),
            _ => (true, resposta, "")
        };
    }
    
    /// <summary>
    /// Processa um valor decimal.
    /// </summary>
    private (bool Sucesso, object? Valor, string Mensagem) ProcessarDecimal(string resposta)
    {
        if (decimal.TryParse(resposta.Replace("R$", "").Trim(), out var valor))
            return (true, valor, "");
        
        return (false, null, "Por favor, forneça um valor numérico válido (ex: 5.50).");
    }
    
    /// <summary>
    /// Processa um valor inteiro.
    /// </summary>
    private (bool Sucesso, object? Valor, string Mensagem) ProcessarInteiro(string resposta)
    {
        if (int.TryParse(resposta, out var valor))
            return (true, valor, "");
        
        return (false, null, "Por favor, forneça um número inteiro válido (ex: 50).");
    }
    
    /// <summary>
    /// Processa um valor booleano.
    /// </summary>
    private (bool Sucesso, object? Valor, string Mensagem) ProcessarBooleano(string resposta)
    {
        var lower = resposta.ToLower().Trim();
        if (lower is "sim" or "s" or "yes" or "y" or "true")
            return (true, true, "");
        
        if (lower is "não" or "nao" or "n" or "no" or "false")
            return (true, false, "");
        
        return (false, null, "Por favor, responda com sim ou não.");
    }
    
    /// <summary>
    /// Processa um endereço IP.
    /// </summary>
    private (bool Sucesso, object? Valor, string Mensagem) ProcessarIP(string resposta)
    {
        if (System.Net.IPAddress.TryParse(resposta.Trim(), out _))
            return (true, resposta.Trim(), "");
        
        return (false, null, "Por favor, forneça um endereço IP válido (ex: 192.168.1.50).");
    }
    
    /// <summary>
    /// Reformula uma pergunta de outra forma.
    /// </summary>
    private string ReformularPergunta(PerguntaPendente pergunta)
    {
        var reformulacoes = new Dictionary<string, string[]>
        {
            ["preco"] = new[]
            {
                "Qual é o valor em reais?",
                "Quanto custa? Digite apenas o número",
                "Me dige o preço, por favor"
            },
            ["quantidade"] = new[]
            {
                "Quantas unidades vão ser adicionadas?",
                "Me informe o número de itens",
                "Qual a quantidade?"
            },
            ["ip"] = new[]
            {
                "Qual o endereço IP da câmera?",
                "Me diga o IP (ex: 192.168.1.50)",
                "Qual é o IP para conexão?"
            }
        };
        
        if (reformulacoes.TryGetValue(pergunta.CampoAlvo, out var opcoes))
        {
            var indice = Math.Min(pergunta.Tentativas, opcoes.Length - 1);
            return opcoes[indice];
        }
        
        return $"Poderia reformular: {pergunta.Pergunta}";
    }
    
    /// <summary>
    /// Confirma as informações coletadas antes de prosseguir.
    /// </summary>
    public string ResumirInformacoes()
    {
        if (_contextoAtual is null)
            return "Nenhuma informação coletada.";
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Resumo do que entendi:");
        
        foreach (var (campo, valor) in _contextoAtual.InformacoesColetadas)
        {
            sb.AppendLine($"- {campo}: {valor}");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Cancela o diálogo atual.
    /// </summary>
    public void CancelarDialogo()
    {
        if (_contextoAtual is not null)
        {
            _contextoAtual = _contextoAtual.AtualizarEstado(EstadoDoDialogo.Cancelado);
            _historico.Add(_contextoAtual);
            _contextoAtual = null;
        }
    }
    
    /// <summary>
    /// Finaliza o diálogo com sucesso.
    /// </summary>
    public Dictionary<string, object> FinalizarDialogo()
    {
        if (_contextoAtual is null)
            throw new InvalidOperationException("Nenhum diálogo ativo.");
        
        var informacoes = new Dictionary<string, object>(_contextoAtual.InformacoesColetadas);
        
        _contextoAtual = _contextoAtual.AtualizarEstado(EstadoDoDialogo.Completo);
        _historico.Add(_contextoAtual);
        _contextoAtual = null;
        
        return informacoes;
    }
    
    /// <summary>
    /// Obtém o contexto atual do diálogo.
    /// </summary>
    public ContextoDoDialogo? ObterContextoAtual()
    {
        return _contextoAtual;
    }
    
    /// <summary>
    /// Referência ao Chefe (para compatibilidade).
    /// </summary>
    private string ReferenciaAoChefe() => "Chefe";
}