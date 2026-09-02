using System.Text.Json.Serialization;

namespace AutonomousStore.Gerente.Services.Agente;

/// <summary>
/// Sistema de correção de erros proativa.
/// </summary>
/// <remarks>
/// CORREÇÃO DE ERROS PROATIVA
///
/// O agente não apenas reporta erros - ele propõe soluções automaticamente.
/// Quando detecta um problema, analisa a causa, sugere correções e
/// solicita aprovação para executar.
///
/// MECANISMO:
/// 1. Detecta erro ou anomalia
/// 2. Analisa a causa provável
/// 3. Propõe uma ou mais soluções
/// 4. Solicita aprovação do Chefe
/// 5. Executa a correção se aprovada
/// 6. Valida se o problema foi resolvido
/// </remarks>
public enum TipoDeErro
{
    [JsonPropertyName("inconsistencia_dados")]
    InconsistenciaDados,
    
    [JsonPropertyName("operacao_falhou")]
    OperacaoFalhou,
    
    [JsonPropertyName("configuracao_invalida")]
    ConfiguracaoInvalida,
    
    [JsonPropertyName("recurso_indisponivel")]
    RecursoIndisponivel,
    
    [JsonPropertyName("permissao_negada")]
    PermissaoNegada,
    
    [JsonPropertyName("timeout")]
    Timeout,
    
    [JsonPropertyName("desconhecido")]
    Desconhecido
}

/// <summary>
/// Solução proposta para um erro.
/// </summary>
public record SolucaoProposta(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("descricao")] string Descricao,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("risco")] NivelDeRisco Risco,
    [property: JsonPropertyName("automatica")] bool PodeSerAutomatica,
    [property: JsonPropertyName("passos")] List<string> Passos
)
{
    public SolucaoProposta(string descricao, string tipo, NivelDeRisco risco, bool automatica, List<string> passos)
        : this(Guid.NewGuid(), descricao, tipo, risco, automatica, passos) { }
}

/// <summary>
/// Erro detectado pelo sistema.
/// </summary>
public record ErroDetectado(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tipo")] TipoDeErro Tipo,
    [property: JsonPropertyName("mensagem")] string Mensagem,
    [property: JsonPropertyName("contexto")] Dictionary<string, object> Contexto,
    [property: JsonPropertyName("detectado_em")] DateTime DetectadoEm,
    [property: JsonPropertyName("solucoes")] List<SolucaoProposta> Solucoes,
    [property: JsonPropertyName("solucao_escolhida")] SolucaoProposta? SolucaoEscolhida,
    [property: JsonPropertyName("estado")] string Estado
)
{
    public ErroDetectado(TipoDeErro tipo, string mensagem, Dictionary<string, object> contexto)
        : this(Guid.NewGuid(), tipo, mensagem, contexto, DateTime.Now, new List<SolucaoProposta>(), null, "detectado") { }
    
    /// <summary>
    /// Adiciona uma solução proposta.
    /// </summary>
    public ErroDetectado AdicionarSolucao(SolucaoProposta solucao)
    {
        var novasSolucoes = new List<SolucaoProposta>(Solucoes);
        novasSolucoes.Add(solucao);
        
        return this with { Solucoes = novasSolucoes };
    }
    
    /// <summary>
    /// Escolhe uma solução para executar.
    /// </summary>
    public ErroDetectado EscolherSolucao(Guid solucaoId)
    {
        var solucao = Solucoes.FirstOrDefault(s => s.Id == solucaoId);
        if (solucao is null)
            throw new ArgumentException("Solução não encontrada.");
        
        return this with { SolucaoEscolhida = solucao, Estado = "solucao_escolhida" };
    }
    
    /// <summary>
    /// Marca como resolvido.
    /// </summary>
    public ErroDetectado MarcarComoResolvido()
    {
        return this with { Estado = "resolvido" };
    }
    
    /// <summary>
    /// Marca como não resolvido.
    /// </summary>
    public ErroDetectado MarcarComoNaoResolvido()
    {
        return this with { Estado = "nao_resolvido" };
    }
}

/// <summary>
/// Analisador de erros e propositor de soluções.
/// </summary>
public class AnalisadorDeErros
{
    /// <summary>
    /// Analisa um erro e propõe soluções.
    /// </summary>
    public ErroDetectado AnalisarErro(TipoDeErro tipo, string mensagem, Dictionary<string, object> contexto)
    {
        var erro = new ErroDetectado(tipo, mensagem, contexto);
        
        // Propõe soluções baseadas no tipo de erro
        erro = tipo switch
        {
            TipoDeErro.InconsistenciaDados => AnalisarInconsistenciaDados(erro),
            TipoDeErro.OperacaoFalhou => AnalisarOperacaoFalhou(erro),
            TipoDeErro.ConfiguracaoInvalida => AnalisarConfiguracaoInvalida(erro),
            TipoDeErro.RecursoIndisponivel => AnalisarRecursoIndisponivel(erro),
            TipoDeErro.PermissaoNegada => AnalisarPermissaoNegada(erro),
            TipoDeErro.Timeout => AnalisarTimeout(erro),
            _ => AnalisarErroDesconhecido(erro)
        };
        
        return erro;
    }
    
    /// <summary>
    /// Analisa inconsistência de dados.
    /// </summary>
    private ErroDetectado AnalisarInconsistenciaDados(ErroDetectado erro)
    {
        var solucoes = new List<SolucaoProposta>();
        
        // Solução 1: Corrigir automaticamente se for simples
        solucoes.Add(new SolucaoProposta(
            "Corrigir valor automaticamente",
            "correcao_automatica",
            NivelDeRisco.Medio,
            true,
            new List<string> { "Identificar valor correto", "Atualizar no sistema", "Gerar log de auditoria" }
        ));
        
        // Solução 2: Solicitar confirmação manual
        solucoes.Add(new SolucaoProposta(
            "Solicitar confirmação manual do valor correto",
            "confirmacao_manual",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Exibir valores conflitantes", "Solicitar input do Chefe", "Atualizar com valor confirmado" }
        ));
        
        // Solução 3: Investigar causa raiz
        solucoes.Add(new SolucaoProposta(
            "Investigar causa raiz antes de corrigir",
            "investigacao_causa",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Verificar logs recentes", "Analisar padrões", "Identificar origem do problema" }
        ));
        
        foreach (var solucao in solucoes)
        {
            erro = erro.AdicionarSolucao(solucao);
        }
        
        return erro;
    }
    
    /// <summary>
    /// Analisa operação que falhou.
    /// </summary>
    private ErroDetectado AnalisarOperacaoFalhou(ErroDetectado erro)
    {
        var solucoes = new List<SolucaoProposta>();
        
        solucoes.Add(new SolucaoProposta(
            "Tentar executar novamente",
            "retry",
            NivelDeRisco.Baixo,
            true,
            new List<string> { "Aguardar 2 segundos", "Reexecutar operação", "Verificar resultado" }
        ));
        
        solucoes.Add(new SolucaoProposta(
            "Verificar pré-requisitos antes de tentar novamente",
            "verificar_pre_requisitos",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Validar dependências", "Verificar conexão", "Confirmar estado do sistema" }
        ));
        
        foreach (var solucao in solucoes)
        {
            erro = erro.AdicionarSolucao(solucao);
        }
        
        return erro;
    }
    
    /// <summary>
    /// Analisa configuração inválida.
    /// </summary>
    private ErroDetectado AnalisarConfiguracaoInvalida(ErroDetectado erro)
    {
        var solucoes = new List<SolucaoProposta>();
        
        solucoes.Add(new SolucaoProposta(
            "Reverter para configuração padrão",
            "reverter_padrao",
            NivelDeRisco.Medio,
            true,
            new List<string> { "Backup da configuração atual", "Aplicar valores padrão", "Testar sistema" }
        ));
        
        solucoes.Add(new SolucaoProposta(
            "Corrigir parâmetro específico",
            "corrigir_parametro",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Identificar parâmetro inválido", "Solicitar valor correto", "Aplicar correção" }
        ));
        
        foreach (var solucao in solucoes)
        {
            erro = erro.AdicionarSolucao(solucao);
        }
        
        return erro;
    }
    
    /// <summary>
    /// Analisa recurso indisponível.
    /// </summary>
    private ErroDetectado AnalisarRecursoIndisponivel(ErroDetectado erro)
    {
        var solucoes = new List<SolucaoProposta>();
        
        solucoes.Add(new SolucaoProposta(
            "Tentar conectar novamente",
            "retry_conexao",
            NivelDeRisco.Baixo,
            true,
            new List<string> { "Aguardar 5 segundos", "Tentar reconectar", "Verificar disponibilidade" }
        ));
        
        solucoes.Add(new SolucaoProposta(
            "Usar recurso alternativo se disponível",
            "usar_alternativa",
            NivelDeRisco.Medio,
            false,
            new List<string> { "Verificar recursos alternativos", "Mudar configuração para usar alternativa", "Testar funcionamento" }
        ));
        
        solucoes.Add(new SolucaoProposta(
            "Notificar Chefe sobre indisponibilidade",
            "notificar_indisponibilidade",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Registrar erro", "Notificar Chefe", "Aguardar instruções" }
        ));
        
        foreach (var solucao in solucoes)
        {
            erro = erro.AdicionarSolucao(solucao);
        }
        
        return erro;
    }
    
    /// <summary>
    /// Analisa permissão negada.
    /// </summary>
    private ErroDetectado AnalisarPermissaoNegada(ErroDetectado erro)
    {
        var solucoes = new List<SolucaoProposta>();
        
        solucoes.Add(new SolucaoProposta(
            "Solicitar permissão adicional ao Chefe",
            "solicitar_permissao",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Explicar necessidade da permissão", "Solicitar aprovação", "Aguardar autorização" }
        ));
        
        solucoes.Add(new SolucaoProposta(
            "Executar operação com credenciais alternativas",
            "usar_credenciais_alternativas",
            NivelDeRisco.Alto,
            false,
            new List<string> { "Verificar credenciais disponíveis", "Usar credencial temporária", "Registrar uso" }
        ));
        
        foreach (var solucao in solucoes)
        {
            erro = erro.AdicionarSolucao(solucao);
        }
        
        return erro;
    }
    
    /// <summary>
    /// Analisa timeout.
    /// </summary>
    private ErroDetectado AnalisarTimeout(ErroDetectado erro)
    {
        var solucoes = new List<SolucaoProposta>();
        
        solucoes.Add(new SolucaoProposta(
            "Aumentar timeout da operação",
            "aumentar_timeout",
            NivelDeRisco.Baixo,
            true,
            new List<string> { "Dobrar tempo limite", "Reexecutar operação", "Monitorar resultado" }
        ));
        
        solucoes.Add(new SolucaoProposta(
            "Executar operação de forma assíncrona",
            "executar_assincrono",
            NivelDeRisco.Baixo,
            true,
            new List<string> { "Mudar para execução em background", "Notificar quando completar", "Processar resultado" }
        ));
        
        foreach (var solucao in solucoes)
        {
            erro = erro.AdicionarSolucao(solucao);
        }
        
        return erro;
    }
    
    /// <summary>
    /// Analisa erro desconhecido.
    /// </summary>
    private ErroDetectado AnalisarErroDesconhecido(ErroDetectado erro)
    {
        var solucoes = new List<SolucaoProposta>();
        
        solucoes.Add(new SolucaoProposta(
            "Coletar informações detalhadas para diagnóstico",
            "coletar_diagnostico",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Coletar logs", "Capturar estado do sistema", "Gerar relatório" }
        ));
        
        solucoes.Add(new SolucaoProposta(
            "Solicitar intervenção manual do Chefe",
            "intervencao_manual",
            NivelDeRisco.Baixo,
            false,
            new List<string> { "Reportar erro ao Chefe", "Fornecer contexto disponível", "Aguardar instruções" }
        ));
        
        foreach (var solucao in solucoes)
        {
            erro = erro.AdicionarSolucao(solucao);
        }
        
        return erro;
    }
}

/// <summary>
/// Executor de correções de erros.
/// </summary>
public class ExecutorDeCorrecoes
{
    private readonly List<ErroDetectado> _historicoErros;
    
    public ExecutorDeCorrecoes()
    {
        _historicoErros = new List<ErroDetectado>();
    }
    
    /// <summary>
    /// Executa uma solução proposta.
    /// </summary>
    public async Task<(bool Sucesso, string Mensagem)> ExecutarSolucaoAsync(
        ErroDetectado erro,
        SolucaoProposta solucao,
        Func<string, Task<bool>> executor)
    {
        try
        {
            // Executa a solução
            var resultado = await executor(solucao.Tipo);
            
            if (resultado)
            {
                var erroResolvido = erro.MarcarComoResolvido();
                _historicoErros.Add(erroResolvido);
                
                return (true, $"Solução '{solucao.Descricao}' executada com sucesso! O problema foi resolvido.");
            }
            else
            {
                var erroNaoResolvido = erro.MarcarComoNaoResolvido();
                _historicoErros.Add(erroNaoResolvido);
                
                return (false, $"Solução '{solucao.Descricao}' foi executada mas o problema persiste. Outra abordagem pode ser necessária.");
            }
        }
        catch (Exception ex)
        {
            var erroFalha = erro with { Estado = "falha_na_correcao" };
            _historicoErros.Add(erroFalha);
            
            return (false, $"Erro ao executar solução: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Obtém estatísticas de correções.
    /// </summary>
    public Dictionary<string, int> ObterEstatisticas()
    {
        return new Dictionary<string, int>
        {
            ["total_erros"] = _historicoErros.Count,
            ["resolvidos"] = _historicoErros.Count(e => e.Estado == "resolvido"),
            ["nao_resolvidos"] = _historicoErros.Count(e => e.Estado == "nao_resolvido"),
            ["falharam"] = _historicoErros.Count(e => e.Estado == "falha_na_correcao")
        };
    }
    
    /// <summary>
    /// Obtém o histórico de erros.
    /// </summary>
    public IReadOnlyList<ErroDetectado> ObterHistorico(int limite = 20)
    {
        return _historicoErros.TakeLast(limite).ToList().AsReadOnly();
    }
}