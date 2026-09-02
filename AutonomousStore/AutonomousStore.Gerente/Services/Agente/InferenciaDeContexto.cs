using System.Text.Json.Serialization;

namespace AutonomousStore.Gerente.Services.Agente;

/// <summary>
/// Sistema de inferência de contexto com dados disponíveis.
/// </summary>
/// <remarks>
/// INFERÊNCIA DE CONTEXTO - O CÉREBRO DO AGENTE
///
/// O agente não apenas interpreta palavras - ele usa o contexto disponível
/// para inferir o que o Chefe realmente quer, mesmo quando a pergunta
/// está mal formulada, cheia de gírias ou com erros de digitação.
///
/// ESTRATÉGIA:
/// 1. Analisa o contexto atual (estoque, produtos, câmeras, etc.)
/// 2. Tenta interpretar mesmo com erros óbvios
/// 3. Usa dados disponíveis para validar inferências
/// 4. Sugere correções quando está incerto
/// 5. Confirma a interpretação antes de agir
/// </remarks>
public class InferenciaDeContexto
{
    private readonly Dictionary<string, object> _contextoDisponivel;
    private readonly List<string> _giriasComuns;
    private readonly Dictionary<string, string> _correcoesComuns;
    
    public InferenciaDeContexto()
    {
        _contextoDisponivel = new Dictionary<string, object>();
        
        // Gírias comuns em português brasileiro
        _giriasComuns = new List<string>
        {
            "td", "tudo", "todo", "cê", "vc", "você", "tava", "estava",
            "pra", "para", "pro", "pro", "tô", "estou", "né", "não",
            "sim", "s", "n", "agora", "agor", "hoje", "hoj", "ontem",
            "ont", "amanhã", "aman", "aqui", "aki", "esse", "is", "esse",
            "essa", "isso", "isso", "pó", "pode", "vai", "ir", "faz",
            "fazer", "mim", "mim", "comigo", "cmg", "gente", "pessoal",
            "maluco", "louco", "crazy", "doido", "legal", "bem", "bom",
            "ruim", "horrível", "horrivel", "caramba", "caraca", "uau",
            "nossa", "nossa", "vixe", "virgem", "santo", "meu deus"
        };
        
        // Correções comuns de erros de digitação
        _correcoesComuns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["adicionar"] = "adicionar",
            ["adicionar"] = "adicionar",
            ["add"] = "adicionar",
            ["criar"] = "adicionar",
            ["novo"] = "adicionar",
            ["tirar"] = "remover",
            ["apagar"] = "remover",
            ["deletar"] = "remover",
            ["excluir"] = "remover",
            ["mudar"] = "alterar",
            ["trocar"] = "alterar",
            ["modificar"] = "alterar",
            ["atualizar"] = "alterar",
            ["preco"] = "preço",
            ["valor"] = "preço",
            ["custo"] = "preço",
            ["qtd"] = "quantidade",
            ["quant"] = "quantidade",
            ["estoque"] = "estoque",
            ["qnt"] = "quantidade",
            ["camera"] = "câmera",
            ["cameras"] = "câmeras",
            ["sistema"] = "sistema",
            ["config"] = "configurar",
            ["configurar"] = "configurar",
            ["reiniciar"] = "reiniciar",
            ["resetar"] = "reiniciar",
            ["restar"] = "reiniciar"
        };
    }
    
    /// <summary>
    /// Atualiza o contexto disponível com dados do sistema.
    /// </summary>
    public void AtualizarContexto(Dictionary<string, object> dados)
    {
        foreach (var (chave, valor) in dados)
        {
            _contextoDisponivel[chave] = valor;
        }
    }
    
    /// <summary>
    /// Interpreta uma pergunta usando contexto e tolerância a erros.
    /// </summary>
    public (string Interpretacao, double Confianca, List<string> Sugestoes) InterpretarPergunta(string perguntaOriginal)
    {
        var perguntaNormalizada = NormalizarParaInterpretacao(perguntaOriginal);
        var interpretacoes = new List<(string Interpretacao, double Confianca)>();
        var sugestoes = new List<string>();
        
        // Tenta interpretação direta
        interpretacoes.Add(InterpretacaoDireta(perguntaNormalizada));
        
        // Tenta interpretação com correção de erros
        interpretacoes.Add(InterpretacaoComCorrecao(perguntaNormalizada));
        
        // Tenta interpretação baseada em contexto
        interpretacoes.Add(InterpretacaoBaseadaEmContexto(perguntaNormalizada));
        
        // Tenta interpretação aproximada
        interpretacoes.Add(InterpretacaoAproximada(perguntaNormalizada));
        
        // Seleciona a melhor interpretação
        var melhor = interpretacoes.OrderByDescending(x => x.Confianca).First();
        
        // Se a confiança for baixa, adiciona sugestões
        if (melhor.Confianca < 0.7)
        {
            sugestoes = GerarSugestoes(perguntaOriginal, perguntaNormalizada);
        }
        
        return (melhor.Interpretacao, melhor.Confianca, sugestoes);
    }
    
    /// <summary>
    /// Normaliza a pergunta para interpretação, removendo gírias e erros comuns.
    /// </summary>
    private string NormalizarParaInterpretacao(string pergunta)
    {
        var normalizada = pergunta.ToLower().Trim();
        
        // Remove pontuação excessiva
        normalizada = System.Text.RegularExpressions.Regex.Replace(normalizada, "[!?.;,.]+", "");
        
        // Substitui gírias por formas padrão
        foreach (var gíria in _giriasComuns)
        {
            normalizada = normalizada.Replace(gíria, "");
        }
        
        // Remove espaços extras
        normalizada = System.Text.RegularExpressions.Regex.Replace(normalizada, @"\s+", " ");
        
        return normalizada.Trim();
    }
    
    /// <summary>
    /// Interpretação direta sem correções.
    /// </summary>
    private (string Interpretacao, double Confianca) InterpretacaoDireta(string pergunta)
    {
        // Palavras-chave para operações
        if (pergunta.Contains("adiciona") || pergunta.Contains("cria") || pergunta.Contains("novo"))
            return ("adicionar_produto", 0.9);
        
        if (pergunta.Contains("altera") || pergunta.Contains("muda") || pergunta.Contains("modifica"))
        {
            if (pergunta.Contains("preco") || pergunta.Contains("valor"))
                return ("alterar_preco", 0.85);
            if (pergunta.Contains("estoque") || pergunta.Contains("quantidade"))
                return ("alterar_estoque", 0.85);
        }
        
        if (pergunta.Contains("remove") || pergunta.Contains("apaga") || pergunta.Contains("exclui"))
            return ("remover_produto", 0.9);
        
        if (pergunta.Contains("camera") || pergunta.Contains("câmera"))
            return ("configurar_camera", 0.8);
        
        if (pergunta.Contains("configura") || pergunta.Contains("sistema"))
            return ("configurar_sistema", 0.75);
        
        if (pergunta.Contains("reinicia") || pergunta.Contains("reset"))
            return ("reiniciar_servico", 0.8);
        
        return ("desconhecido", 0.1);
    }
    
    /// <summary>
    /// Interpretação com correção de erros de digitação.
    /// </summary>
    private (string Interpretacao, double Confianca) InterpretacaoComCorrecao(string pergunta)
    {
        var palavras = pergunta.Split(' ');
        var corrigida = new List<string>();
        
        foreach (var palavra in palavras)
        {
            if (_correcoesComuns.TryGetValue(palavra, out var correcao))
                corrigida.Add(correcao);
            else
                corrigida.Add(palavra);
        }
        
        var perguntaCorrigida = string.Join(" ", corrigida);
        return InterpretacaoDireta(perguntaCorrigida);
    }
    
    /// <summary>
    /// Interpretação baseada no contexto disponível.
    /// </summary>
    private (string Interpretacao, double Confianca) InterpretacaoBaseadaEmContexto(string pergunta)
    {
        // Se o contexto tem informações de produtos, é mais provável que seja sobre produtos
        if (_contextoDisponivel.ContainsKey("produtos") || _contextoDisponivel.ContainsKey("estoque"))
        {
            if (pergunta.Contains("preco") || pergunta.Contains("valor"))
                return ("alterar_preco", 0.7);
            if (pergunta.Contains("quantidade") || pergunta.Contains("qtd"))
                return ("alterar_estoque", 0.7);
        }
        
        // Se o contexto tem informações de câmeras
        if (_contextoDisponivel.ContainsKey("cameras") || _contextoDisponivel.ContainsKey("so_espacial"))
        {
            if (pergunta.Contains("nova") || pergunta.Contains("adiciona"))
                return ("configurar_camera", 0.75);
        }
        
        return ("desconhecido", 0.2);
    }
    
    /// <summary>
    /// Interpretação aproximada para perguntas muito mal formuladas.
    /// </summary>
    private (string Interpretacao, double Confianca) InterpretacaoAproximada(string pergunta)
    {
        // Tenta encontrar padrões mínimos
        if (pergunta.Length < 5)
            return ("desconhecido", 0.1);
        
        // Se tem apenas uma palavra-chave, tenta inferir
        if (pergunta.Contains("add") || pergunta.Contains("novo"))
            return ("adicionar_produto", 0.5);
        
        if (pergunta.Contains("muda") || pergunta.Contains("altera"))
            return ("alterar_preco", 0.4);
        
        if (pergunta.Contains("camera"))
            return ("configurar_camera", 0.6);
        
        return ("desconhecido", 0.2);
    }
    
    /// <summary>
    /// Gera sugestões de reformulação da pergunta.
    /// </summary>
    private List<string> GerarSugestoes(string perguntaOriginal, string perguntaNormalizada)
    {
        var sugestoes = new List<string>();
        
        // Se a pergunta for muito curta
        if (perguntaOriginal.Length < 10)
        {
            sugestoes.Add("Pode ser mais específico? (ex: 'adiciona chocolate ao leite')");
        }
        
        // Se tiver gírias
        if (_giriasComuns.Any(g => perguntaOriginal.ToLower().Contains(g)))
        {
            sugestoes.Add("Evite gírias para melhor entendimento (ex: 'adicionar' em vez de 'add')");
        }
        
        // Se tiver erros de digitação óbvios
        var palavras = perguntaNormalizada.Split(' ');
        var temErros = palavras.Any(p => _correcoesComuns.ContainsKey(p));
        
        if (temErros)
        {
            sugestoes.Add("Notei alguns erros de digitação. Quer que eu corrija?");
        }
        
        // Se estiver muito ambíguo
        if (sugestoes.Count == 0)
        {
            sugestoes.Add("Pode reformular de outra forma? (ex: especificar produto ou ação)");
        }
        
        return sugestoes;
    }
    
    /// <summary>
    /// Verifica se a interpretação faz sentido com o contexto.
    /// </summary>
    public bool VerificarInterpretacao(string interpretacao, Dictionary<string, object> parametros)
    {
        return interpretacao switch
        {
            "adicionar_produto" => parametros.ContainsKey("nome") && parametros.ContainsKey("preco"),
            "alterar_preco" => parametros.ContainsKey("nome") && parametros.ContainsKey("preco"),
            "alterar_estoque" => parametros.ContainsKey("nome") && parametros.ContainsKey("quantidade"),
            "configurar_camera" => parametros.ContainsKey("ip"),
            _ => true
        };
    }
    
    /// <summary>
    /// Sugere parâmetros faltantes baseados no contexto.
    /// </summary>
    public Dictionary<string, object> SugerirParametros(string interpretacao)
    {
        return interpretacao switch
        {
            "adicionar_produto" => new Dictionary<string, object>
            {
                ["nome_sugerido"] = _contextoDisponivel.GetValueOrDefault("ultimo_produto_adicionado", ""),
                ["preco_sugerido"] = _contextoDisponivel.GetValueOrDefault("preco_medio", 10.0),
                ["quantidade_sugerida"] = _contextoDisponivel.GetValueOrDefault("quantidade_padrao", 50)
            },
            "alterar_preco" => new Dictionary<string, object>
            {
                ["produto_sugerido"] = _contextoDisponivel.GetValueOrDefault("produto_mais_recente", "")
            },
            _ => new Dictionary<string, object>()
        };
    }
}