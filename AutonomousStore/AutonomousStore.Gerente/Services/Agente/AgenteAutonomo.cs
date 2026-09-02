using System.Text.Json.Serialization;

namespace AutonomousStore.Gerente.Services.Agente;

/// <summary>
/// Agente autônomo inteligente - o coração do sistema evolucionado.
/// </summary>
/// <remarks>
/// AGENTE AUTÔNOMO INTELIGENTE
///
/// Este é o componente central que integra todas as capacidades do agente:
/// - Sistema de permissões e segurança
/// - Personalidade adaptativa com humor
/// - Compreensão emocional básica
/// - Loop de feedback inteligente
/// - Correção de erros proativa
/// - Raciocínio complexo
///
/// O agente pode receber ordens do Chefe, coletar informações automaticamente,
/// solicitar permissões quando necessário, e executar operações com autonomia
/// dentro dos limites de segurança estabelecidos.
/// </remarks>
public class AgenteAutonomo
{
    private readonly GerenciadorDePermissoes _gerenciadorPermissoes;
    private readonly PersonalidadeDoAgente _personalidade;
    private readonly GerenciadorDeFeedback _gerenciadorFeedback;
    private readonly AnalisadorDeErros _analisadorErros;
    private readonly ExecutorDeCorrecoes _executorCorrecoes;
    
    public string Nome => _personalidade.Nome;
    public string ReferenciaAoChefe => _personalidade.ReferenciaAoChefe;
    
    public AgenteAutonomo()
    {
        _gerenciadorPermissoes = new GerenciadorDePermissoes();
        _personalidade = new PersonalidadeDoAgente();
        _gerenciadorFeedback = new GerenciadorDeFeedback();
        _analisadorErros = new AnalisadorDeErros();
        _executorCorrecoes = new ExecutorDeCorrecoes();
    }
    
    /// <summary>
    /// Processa uma ordem do Chefe.
    /// </summary>
    public async Task<string> ProcessarOrdemAsync(
        string ordem,
        Func<string, Dictionary<string, object>, Task<string>> executorOperacao)
    {
        // Atualiza contexto de personalidade
        _personalidade.ProcessarEntrada(ordem);
        
        // Detecta a intenção básica
        var intencao = DetectarIntencao(ordem);
        
        // Se for uma pergunta simples, delega para o sistema existente
        if (EhPerguntaSimples(intencao))
        {
            return await ResponderPerguntaSimples(ordem, executorOperacao);
        }
        
        // Se for uma ordem de execução, processa como agente autônomo
        if (EhOrdemDeExecucao(intencao))
        {
            return await ProcessarOrdemDeExecucao(ordem, intencao, executorOperacao);
        }
        
        // Se não reconheceu, pede clarificação
        return _personalidade.AdaptarResposta(
            $"Não tenho certeza do que você quer. Pode reformular de outra forma?",
            false
        );
    }
    
    /// <summary>
    /// Detecta a intenção básica da ordem.
    /// </summary>
    private string DetectarIntencao(string ordem)
    {
        var ordemLower = ordem.ToLower();
        
        // Operações de escrita
        if (ordemLower.Contains("adiciona") || ordemLower.Contains("cria") || ordemLower.Contains("novo"))
            return "adicionar";
        
        if (ordemLower.Contains("altera") || ordemLower.Contains("muda") || ordemLower.Contains("modifica"))
            return "alterar";
        
        if (ordemLower.Contains("remove") || ordemLower.Contains("apaga") || ordemLower.Contains("exclui"))
            return "remover";
        
        // Operações de configuração
        if (ordemLower.Contains("câmera") || ordemLower.Contains("camera"))
            return "configurar_camera";
        
        if (ordemLower.Contains("configura") || ordemLower.Contains("configur"))
            return "configurar_sistema";
        
        // Perguntas simples
        if (ordemLower.Contains("quantos") || ordemLower.Contains("quanto") || ordemLower.Contains("qual"))
            return "pergunta_simples";
        
        if (ordemLower.Contains("como") || ordemLower.Contains("status"))
            return "pergunta_simples";
        
        return "desconhecido";
    }
    
    /// <summary>
    /// Determina se é uma pergunta simples.
    /// </summary>
    private bool EhPerguntaSimples(string intencao)
    {
        return intencao == "pergunta_simples";
    }
    
    /// <summary>
    /// Determina se é uma ordem de execução.
    /// </summary>
    private bool EhOrdemDeExecucao(string intencao)
    {
        return intencao is "adicionar" or "alterar" or "remover" or 
               "configurar_camera" or "configurar_sistema";
    }
    
    /// <summary>
    /// Responde a uma pergunta simples.
    /// </summary>
    private async Task<string> ResponderPerguntaSimples(
        string pergunta,
        Func<string, Dictionary<string, object>, Task<string>> executor)
    {
        try
        {
            var resposta = await executor(pergunta, new Dictionary<string, object>());
            return _personalidade.AdaptarResposta(resposta, true);
        }
        catch (Exception ex)
        {
            var erro = _analisadorErros.AnalisarErro(
                TipoDeErro.OperacaoFalhou,
                ex.Message,
                new Dictionary<string, object> { ["pergunta"] = pergunta }
            );
            
            return _personalidade.AdaptarResposta(
                $"Encontrei um problema ao responder: {ex.Message}. {GerarMensagemDeErro(erro)}",
                false
            );
        }
    }
    
    /// <summary>
    /// Processa uma ordem de execução com autonomia.
    /// </summary>
    private async Task<string> ProcessarOrdemDeExecucao(
        string ordem,
        string intencao,
        Func<string, Dictionary<string, object>, Task<string>> executor)
    {
        // Cria requisição de permissão
        var requisicao = _gerenciadorPermissoes.CriarRequisicao(
            intencao,
            $"Ordem do {ReferenciaAoChefe}: {ordem}"
        );
        
        // Se requer aprovação manual, solicita
        if (requisicao.RequerAprovacaoManual)
        {
            return _personalidade.AdaptarResposta(
                $"Entendi! Para {intencao}, preciso da sua aprovação. " +
                $"Esta é uma operação de {requisicao.Risco} risco. Posso prosseguir?",
                false
            );
        }
        
        // Se não requer aprovação, coleta informações necessárias
        var informacoesNecessarias = DeterminarInformacoesNecessarias(intencao);
        
        if (informacoesNecessarias.Any())
        {
            var contexto = _gerenciadorFeedback.IniciarDialogo(intencao, informacoesNecessarias);
            return IniciarColetaDeInformacoes(contexto);
        }
        
        // Se não precisa de informações adicionais, executa diretamente
        return await ExecutarOperacao(ordem, intencao, new Dictionary<string, object>(), executor);
    }
    
    /// <summary>
    /// Determina quais informações são necessárias para uma operação.
    /// </summary>
    private List<PerguntaPendente> DeterminarInformacoesNecessarias(string intencao)
    {
        return intencao switch
        {
            "adicionar" => new List<PerguntaPendente>
            {
                new("O que você quer adicionar?", "tipo", "texto"),
                new("Qual o nome/descrição?", "nome", "texto"),
                new("Qual o valor/preço?", "preco", "preco"),
                new("Qual a quantidade inicial?", "quantidade", "quantidade")
            },
            "configurar_camera" => new List<PerguntaPendente>
            {
                new("Qual o IP da câmera?", "ip", "ip"),
                new("Qual o tipo de câmera? (USB/HTTP)", "tipo", "texto"),
                new("Qual o papel dela? (alto/frontal/lateral)", "papel", "texto")
            },
            _ => new List<PerguntaPendente>()
        };
    }
    
    /// <summary>
    /// Inicia a coleta de informações.
    /// </summary>
    private string IniciarColetaDeInformacoes(ContextoDoDialogo contexto)
    {
        var proximaPergunta = contexto.ProximaPergunta();
        if (proximaPergunta is not null)
        {
            return _personalidade.AdaptarResposta(
                $"Claro, {ReferenciaAoChefe}! {proximaPergunta.Pergunta}",
                false
            );
        }
        
        return _personalidade.AdaptarResposta(
            "Entendi! Vou processar isso agora.",
            true
        );
    }
    
    /// <summary>
    /// Processa a resposta do Chefe durante a coleta de informações.
    /// </summary>
    public string ProcessarRespostaDoChefe(string resposta)
    {
        var contexto = _gerenciadorFeedback.ObterContextoAtual();
        if (contexto is null)
        {
            return _personalidade.AdaptarResposta(
                "Não tenho nenhuma pergunta pendente no momento.",
                false
            );
        }
        
        var (novoContexto, mensagem) = _gerenciadorFeedback.ProcessarResposta(resposta);
        
        if (novoContexto.Estado == EstadoDoDialogo.Confirmacao)
        {
            var resumo = _gerenciadorFeedback.ResumirInformacoes();
            return _personalidade.AdaptarResposta(
                $"{resumo}\n\n{mensagem}",
                false
            );
        }
        
        if (novoContexto.Estado == EstadoDoDialogo.Completo)
        {
            var informacoes = _gerenciadorFeedback.FinalizarDialogo();
            return _personalidade.AdaptarResposta(
                $"Perfeito! Todas as informações foram coletadas. Posso executar a operação?",
                true
            );
        }
        
        return _personalidade.AdaptarResposta(mensagem, false);
    }
    
    /// <summary>
    /// Executa uma operação com as informações coletadas.
    /// </summary>
    private async Task<string> ExecutarOperacao(
        string ordem,
        string intencao,
        Dictionary<string, object> informacoes,
        Func<string, Dictionary<string, object>, Task<string>> executor)
    {
        try
        {
            var resultado = await executor(intencao, informacoes);
            
            // Marca como executado com sucesso
            var contexto = _gerenciadorFeedback.ObterContextoAtual();
            if (contexto is not null)
            {
                _personalidade.ProcessarEntrada(resultado, true);
            }
            
            return _personalidade.AdaptarResposta(
                $"Operação concluída com sucesso! {resultado}",
                true
            );
        }
        catch (Exception ex)
        {
            // Analisa o erro e propõe soluções
            var erro = _analisadorErros.AnalisarErro(
                TipoDeErro.OperacaoFalhou,
                ex.Message,
                new Dictionary<string, object>
                {
                    ["ordem"] = ordem,
                    ["intencao"] = intencao,
                    ["informacoes"] = informacoes
                }
            );
            
            _personalidade.ProcessarEntrada(ex.Message, false);
            
            return _personalidade.AdaptarResposta(
                $"Encontrei um problema ao executar: {ex.Message}\n\n{GerarMensagemDeErro(erro)}",
                false
            );
        }
    }
    
    /// <summary>
    /// Gera uma mensagem de erro baseada nas soluções propostas.
    /// </summary>
    private string GerarMensagemDeErro(ErroDetectado erro)
    {
        if (!erro.Solucoes.Any())
            return "Não tenho uma solução automática para este problema.";
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Posso tentar as seguintes soluções:");
        
        for (int i = 0; i < erro.Solucoes.Count; i++)
        {
            var solucao = erro.Solucoes[i];
            sb.AppendLine($"{i + 1}. {solucao.Descricao} ({solucao.Risco} risco)");
        }
        
        sb.AppendLine();
        sb.AppendLine($"Qual solução você prefere? Ou quer que eu tente a primeira automaticamente?");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Aprova uma requisição pendente.
    /// </summary>
    public bool AprovarRequisicao(Guid id, string? motivo = null)
    {
        return _gerenciadorPermissoes.AprovarRequisicao(id, motivo);
    }
    
    /// <summary>
    /// Nega uma requisição pendente.
    /// </summary>
    public bool NegarRequisicao(Guid id, string motivo)
    {
        return _gerenciadorPermissoes.NegarRequisicao(id, motivo);
    }
    
    /// <summary>
    /// Obtém requisições pendentes.
    /// </summary>
    public IReadOnlyList<RequisicaoDePermissao> ObterRequisicoesPendentes()
    {
        return _gerenciadorPermissoes.ObterPendentes();
    }
    
    /// <summary>
    /// Obtém estatísticas do agente.
    /// </summary>
    public Dictionary<string, object> ObterEstatisticas()
    {
        return new Dictionary<string, object>
        {
            ["permissoes"] = _gerenciadorPermissoes.ObterEstatisticas(),
            // `new { ["x"] = y }` nao existe: chave entre colchetes e
            // inicializador de INDICE, que so um dicionario tem. Tipo anonimo
            // usa `new { X = y }`. Como o resto do metodo ja monta
            // dicionarios, estes viram dicionarios tambem.
            ["personalidade"] = new Dictionary<string, object>
            {
                ["nome"] = Nome,
                ["referencia_chefe"] = ReferenciaAoChefe,
                ["contexto"] = _personalidade.ContextoAtual
            },
            ["feedback"] = new Dictionary<string, object>
            {
                ["dialogo_ativo"] = _gerenciadorFeedback.ObterContextoAtual() is not null
            },
            ["correcoes"] = _executorCorrecoes.ObterEstatisticas()
        };
    }
    
    /// <summary>
    /// Reinicia o contexto do agente.
    /// </summary>
    public void Reiniciar()
    {
        _personalidade.ReiniciarContexto();
    }
}