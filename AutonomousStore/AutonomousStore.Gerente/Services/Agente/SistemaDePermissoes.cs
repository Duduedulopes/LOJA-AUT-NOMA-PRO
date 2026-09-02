using System.Text.Json.Serialization;

namespace AutonomousStore.Gerente.Services.Agente;

/// <summary>
/// Sistema de permissões e níveis de risco para operações autônomas.
/// </summary>
/// <remarks>
/// FUNDAMENTO DE SEGURANÇA PARA AUTONOMIA
///
/// Sem este sistema, o agente não poderia executar nenhuma operação
/// de escrita no sistema de forma segura. Cada operação é classificada
/// por risco e tipo, exigindo aprovação apropriada.
///
/// NÍVEIS DE RISCO:
/// - BAIXO: Consultas, operações reversíveis
/// - MEDIO: Alterações de dados que podem ser desfeitas
/// - ALTO: Configurações do sistema, requer aprovação manual
/// - CRITICO: Operações estruturais, requer aprovação + confirmação extra
/// </remarks>
public enum NivelDeRisco
{
    [JsonPropertyName("baixo")]
    Baixo,
    
    [JsonPropertyName("medio")]
    Medio,
    
    [JsonPropertyName("alto")]
    Alto,
    
    [JsonPropertyName("critico")]
    Critico
}

/// <summary>
/// Tipo de operação que o agente pode executar.
/// </summary>
public enum TipoDeOperacao
{
    [JsonPropertyName("leitura")]
    Leitura,
    
    [JsonPropertyName("escrita")]
    Escrita,
    
    [JsonPropertyName("configuracao")]
    Configuracao,
    
    [JsonPropertyName("sistema")]
    Sistema
}

/// <summary>
/// Estado de uma requisição de permissão.
/// </summary>
public enum EstadoDePermissao
{
    [JsonPropertyName("pendente")]
    Pendente,
    
    [JsonPropertyName("aprovada")]
    Aprovada,
    
    [JsonPropertyName("negada")]
    Negada,
    
    [JsonPropertyName("executada")]
    Executada,
    
    [JsonPropertyName("falhou")]
    Falhou
}

/// <summary>
/// Requisição de permissão para executar uma operação.
/// </summary>
public record RequisicaoDePermissao(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("operacao")] string Operacao,
    [property: JsonPropertyName("tipo")] TipoDeOperacao Tipo,
    [property: JsonPropertyName("risco")] NivelDeRisco Risco,
    [property: JsonPropertyName("justificativa")] string Justificativa,
    [property: JsonPropertyName("estado")] EstadoDePermissao Estado,
    [property: JsonPropertyName("criada_em")] DateTime CriadaEm,
    [property: JsonPropertyName("dados_adicionais")] Dictionary<string, object>? DadosAdicionais
)
{
    /// <summary>
    /// Determina se esta operação requer aprovação manual do Chefe.
    /// </summary>
    /// <remarks>
    /// CALCULADA, e nao recebida. Era as duas coisas ao mesmo tempo —
    /// parametro do construtor e propriedade daqui — e nesse caso o C#
    /// descarta o parametro sem avisar em voz alta. Quem construisse a
    /// requisicao passando `false` numa operacao de risco alto acharia
    /// que desligou a aprovacao, e nao teria desligado.
    ///
    /// Agora a regra existe num lugar so e nao ha como contradize-la
    /// de fora.
    /// </remarks>
    [JsonPropertyName("requer_aprovacao_manual")]
    public bool RequerAprovacaoManual => Risco >= NivelDeRisco.Alto;
    
    /// <summary>
    /// Determina se esta operação pode ser executada automaticamente.
    /// </summary>
    public bool PodeSerAutomatica => Risco == NivelDeRisco.Baixo && Estado == EstadoDePermissao.Aprovada;
    
    /// <summary>
    /// Marca a requisição como aprovada.
    /// </summary>
    public RequisicaoDePermissao Aprovar(string? motivo = null)
    {
        var dados = DadosAdicionais ?? new Dictionary<string, object>();
        if (motivo != null)
            dados["motivo_aprovacao"] = motivo;
        
        return this with
        {
            Estado = EstadoDePermissao.Aprovada,
            DadosAdicionais = dados
        };
    }
    
    /// <summary>
    /// Marca a requisição como negada.
    /// </summary>
    public RequisicaoDePermissao Negar(string motivo)
    {
        var dados = DadosAdicionais ?? new Dictionary<string, object>();
        dados["motivo_negacao"] = motivo;
        
        return this with
        {
            Estado = EstadoDePermissao.Negada,
            DadosAdicionais = dados
        };
    }
    
    /// <summary>
    /// Marca a requisição como executada com sucesso.
    /// </summary>
    public RequisicaoDePermissao MarcarComoExecutada()
    {
        return this with { Estado = EstadoDePermissao.Executada };
    }
    
    /// <summary>
    /// Marca a requisição como falha na execução.
    /// </summary>
    public RequisicaoDePermissao MarcarComoFalha(string erro)
    {
        var dados = DadosAdicionais ?? new Dictionary<string, object>();
        dados["erro_execucao"] = erro;
        
        return this with
        {
            Estado = EstadoDePermissao.Falhou,
            DadosAdicionais = dados
        };
    }
}

/// <summary>
/// Classificador automático de risco para operações.
/// </summary>
public class ClassificadorDeRisco
{
    private readonly Dictionary<string, (NivelDeRisco Risco, TipoDeOperacao Tipo)> _classificacoes;
    
    public ClassificadorDeRisco()
    {
        _classificacoes = new Dictionary<string, (NivelDeRisco, TipoDeOperacao)>(StringComparer.OrdinalIgnoreCase)
        {
            // Operações de leitura - risco baixo
            ["consultar_estoque"] = (NivelDeRisco.Baixo, TipoDeOperacao.Leitura),
            ["consultar_faturamento"] = (NivelDeRisco.Baixo, TipoDeOperacao.Leitura),
            ["consultar_pessoas"] = (NivelDeRisco.Baixo, TipoDeOperacao.Leitura),
            ["consultar_cameras"] = (NivelDeRisco.Baixo, TipoDeOperacao.Leitura),
            
            // Operações de escrita - risco médio
            ["adicionar_produto"] = (NivelDeRisco.Medio, TipoDeOperacao.Escrita),
            ["alterar_preco"] = (NivelDeRisco.Medio, TipoDeOperacao.Escrita),
            ["alterar_estoque"] = (NivelDeRisco.Medio, TipoDeOperacao.Escrita),
            ["remover_produto"] = (NivelDeRisco.Medio, TipoDeOperacao.Escrita),
            
            // Operações de configuração - risco alto
            ["adicionar_camera"] = (NivelDeRisco.Alto, TipoDeOperacao.Configuracao),
            ["alterar_configuracao"] = (NivelDeRisco.Alto, TipoDeOperacao.Configuracao),
            ["vincular_rfid"] = (NivelDeRisco.Medio, TipoDeOperacao.Configuracao),
            
            // Operações de sistema - risco crítico
            ["reiniciar_servico"] = (NivelDeRisco.Critico, TipoDeOperacao.Sistema),
            ["alterar_estrutura"] = (NivelDeRisco.Critico, TipoDeOperacao.Sistema),
            ["limpar_logs"] = (NivelDeRisco.Alto, TipoDeOperacao.Sistema),
        };
    }
    
    /// <summary>
    /// Classifica uma operação baseada no nome.
    /// </summary>
    public (NivelDeRisco Risco, TipoDeOperacao Tipo) Classificar(string operacao)
    {
        if (_classificacoes.TryGetValue(operacao, out var classificacao))
            return classificacao;
        
        // Classificação padrão para operações desconhecidas
        return (NivelDeRisco.Medio, TipoDeOperacao.Escrita);
    }
    
    /// <summary>
    /// Adiciona uma nova classificação de operação.
    /// </summary>
    public void AdicionarClassificacao(string operacao, NivelDeRisco risco, TipoDeOperacao tipo)
    {
        _classificacoes[operacao] = (risco, tipo);
    }
}

/// <summary>
/// Gerenciador de permissões do agente autônomo.
/// </summary>
public class GerenciadorDePermissoes
{
    private readonly ClassificadorDeRisco _classificador;
    private readonly List<RequisicaoDePermissao> _historico;
    private readonly List<RequisicaoDePermissao> _pendentes;
    
    public GerenciadorDePermissoes()
    {
        _classificador = new ClassificadorDeRisco();
        _historico = new List<RequisicaoDePermissao>();
        _pendentes = new List<RequisicaoDePermissao>();
    }
    
    /// <summary>
    /// Cria uma nova requisição de permissão.
    /// </summary>
    public RequisicaoDePermissao CriarRequisicao(
        string operacao,
        string justificativa,
        Dictionary<string, object>? dadosAdicionais = null)
    {
        var (risco, tipo) = _classificador.Classificar(operacao);
        
        var requisicao = new RequisicaoDePermissao(
            Id: Guid.NewGuid(),
            Operacao: operacao,
            Tipo: tipo,
            Risco: risco,
            Justificativa: justificativa,
            Estado: EstadoDePermissao.Pendente,
            CriadaEm: DateTime.Now,
            DadosAdicionais: dadosAdicionais
        );
        
        _pendentes.Add(requisicao);
        return requisicao;
    }
    
    /// <summary>
    /// Aprova uma requisição pendente.
    /// </summary>
    public bool AprovarRequisicao(Guid id, string? motivo = null)
    {
        var indice = _pendentes.FindIndex(r => r.Id == id);
        if (indice == -1)
            return false;
        
        var aprovada = _pendentes[indice].Aprovar(motivo);
        _pendentes.RemoveAt(indice);
        _historico.Add(aprovada);
        
        return true;
    }
    
    /// <summary>
    /// Nega uma requisição pendente.
    /// </summary>
    public bool NegarRequisicao(Guid id, string motivo)
    {
        var indice = _pendentes.FindIndex(r => r.Id == id);
        if (indice == -1)
            return false;
        
        var negada = _pendentes[indice].Negar(motivo);
        _pendentes.RemoveAt(indice);
        _historico.Add(negada);
        
        return true;
    }
    
    /// <summary>
    /// Marca uma requisição como executada.
    /// </summary>
    public bool MarcarComoExecutada(Guid id)
    {
        var indice = _historico.FindIndex(r => r.Id == id);
        if (indice == -1)
            return false;
        
        _historico[indice] = _historico[indice].MarcarComoExecutada();
        return true;
    }
    
    /// <summary>
    /// Marca uma requisição como falha.
    /// </summary>
    public bool MarcarComoFalha(Guid id, string erro)
    {
        var indice = _historico.FindIndex(r => r.Id == id);
        if (indice == -1)
            return false;
        
        _historico[indice] = _historico[indice].MarcarComoFalha(erro);
        return true;
    }
    
    /// <summary>
    /// Obtém todas as requisições pendentes.
    /// </summary>
    public IReadOnlyList<RequisicaoDePermissao> ObterPendentes()
    {
        return _pendentes.AsReadOnly();
    }
    
    /// <summary>
    /// Obtém o histórico de requisições.
    /// </summary>
    public IReadOnlyList<RequisicaoDePermissao> ObterHistorico(int limite = 50)
    {
        return _historico.TakeLast(limite).ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Obtém estatísticas das requisições.
    /// </summary>
    public Dictionary<string, int> ObterEstatisticas()
    {
        return new Dictionary<string, int>
        {
            ["pendentes"] = _pendentes.Count,
            ["historico_total"] = _historico.Count,
            ["aprovadas"] = _historico.Count(r => r.Estado == EstadoDePermissao.Aprovada || r.Estado == EstadoDePermissao.Executada),
            ["negadas"] = _historico.Count(r => r.Estado == EstadoDePermissao.Negada),
            ["falharam"] = _historico.Count(r => r.Estado == EstadoDePermissao.Falhou),
            ["executadas"] = _historico.Count(r => r.Estado == EstadoDePermissao.Executada)
        };
    }
}