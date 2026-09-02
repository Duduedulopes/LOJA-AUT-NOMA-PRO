using System.Text.Json;
using System.Text.RegularExpressions;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Uma fabrica por detector. Aqui mora a POLITICA de classificacao.
/// </summary>
/// <remarks>
/// POR QUE ISTO E UM LUGAR SO.
///
/// Cada detector precisa decidir tres coisas: o tipo, a severidade e a acao
/// recomendada. Espalhar essa decisao pelos controllers faria a politica
/// existir em oito arquivos e em nenhum — e no dia em que "roubo passa a ser
/// Alta em vez de Critica" alguem mudaria sete dos oito.
///
/// Cada metodo aqui e uma frase completa: "isto que eu vi, deste jeito, e
/// deste tipo, com esta gravidade, e o que se faz e isto". Da para ler a
/// politica inteira de uma vez.
///
/// TODA FABRICA PREENCHE `chave`. E o que impede a mesma ocorrencia de virar
/// cem linhas quando o varredor rodar cem vezes.
///
/// NENHUMA USA `CorrigirAutomaticamente` — nem poderia: o construtor de
/// `Ocorrencia` recusa.
/// </remarks>
public static class Deteccoes
{
    private const string Loja = "AutonomousStore";

    private static string Json(object o) => JsonSerializer.Serialize(o);

    /// <summary>Os apps que podem reportar erro de navegador.</summary>
    /// <remarks>
    /// Lista fechada porque a rota que recebe isto e ANONIMA: sem ela,
    /// qualquer um escreveria "Sistema: Banco Central" no seu historico.
    /// </remarks>
    public static readonly string[] AppsQueReportam = { "ClientApp", "AdminApp", "SuporteApp" };

    /// <summary>A mensagem sem os numeros que mudam a cada ocorrencia.</summary>
    /// <remarks>
    /// POR QUE OS NUMEROS SAEM DA CHAVE.
    ///
    /// "Produto 42 não encontrado" e "Produto 91 não encontrado" sao o MESMO
    /// defeito visto duas vezes. Se o id entrasse na chave, cada compra
    /// azarada viraria uma linha nova e a contagem nunca passaria de 1 —
    /// exatamente o problema que a contagem existe para resolver.
    ///
    /// O numero nao se perde: ele continua inteiro na `Descricao` e no
    /// `DadosEnvolvidosJson`. Sai so da chave, que e identidade e nao dado.
    /// </remarks>
    private static string Assinatura(string texto, int limite = 120)
    {
        var s = Regex.Replace(texto ?? "", @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", "#");
        s = Regex.Replace(s, @"\d+", "#");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length <= limite ? s : s[..limite];
    }

    /// <summary>A coluna `Chave` tem 200 caracteres. Cortar aqui e melhor que estourar no banco.</summary>
    private static string Cabe(string chave) => chave.Length <= 200 ? chave : chave[..200];

    /// <summary>
    /// Sessao cancelada que ainda tinha produto no carrinho.
    /// </summary>
    /// <remarks>
    /// O FURO QUE JA EXISTE E DA PARA CONTAR HOJE. O estoque baixa no
    /// `AddItem` — o produto saiu fisicamente da prateleira. O `RemoveItem`
    /// devolve. O `Cancel` NAO devolvia: entao toda sessao cancelada com
    /// item deixou o sistema contando a menos do que existe na prateleira.
    ///
    /// Isto e defeito de software, nao roubo, e a descricao diz isso com
    /// todas as letras — acusar cliente por bug nosso seria o pior erro que
    /// este sistema poderia cometer.
    /// </remarks>
    public static Ocorrencia SessaoCanceladaComItem(
        Guid sessaoId,
        DateTime quandoUtc,
        IReadOnlyList<(string Produto, int Quantidade, decimal Valor)> itens)
    {
        var unidades = itens.Sum(i => i.Quantidade);
        var reais = itens.Sum(i => i.Valor);
        var lista = string.Join(", ", itens.Select(i => $"{i.Quantidade}x {i.Produto}"));

        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: Loja,
            modulo: "SessionsController",
            operacao: "Cancel",
            tipo: TipoDeOcorrencia.FuroDeSistema,
            severidade: reais >= 50m ? Severidade.Alta : Severidade.Media,
            descricao:
                $"Sessão cancelada com {unidades} unidade(s) ainda no carrinho ({lista}). " +
                "O estoque foi baixado quando o produto saiu da prateleira e o cancelamento " +
                "não devolveu — o sistema está contando a menos do que existe.",
            recomendacao: AcaoRecomendada.SugerirCorrecao,
            correlationId: sessaoId,
            dadosEnvolvidosJson: Json(new
            {
                sessaoId,
                itens = itens.Select(i => new { i.Produto, i.Quantidade, i.Valor }),
                unidades,
                reais,
            }),
            causaProvavel:
                "O método Cancel do SessionsController não chama IncreaseStockAsync, " +
                "ao contrário do RemoveItem. (Inferência a partir da leitura do código.)",
            impacto: $"{unidades} unidade(s), {reais:0.00} em valor de catálogo",
            chave: $"sessao-cancelada-com-item:{sessaoId}");
    }

    /// <summary>
    /// RFID da porta leu uma tag conhecida saindo, e nao ha pagamento.
    /// </summary>
    /// <remarks>
    /// A UNICA DETECCAO QUE ACUSA UMA PESSOA. Por isso o registro e frio: o
    /// que a leitora leu, quando, e qual sessao estava aberta. Nada sobre
    /// quem, nada sobre intencao. O sistema nao viu ninguem — viu uma tag.
    /// </remarks>
    public static Ocorrencia SaidaSemPagamento(
        string tagRfid,
        string produto,
        Guid? sessaoId,
        string statusDaSessao,
        DateTime quandoUtc)
    {
        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: Loja,
            modulo: "SessionsController",
            operacao: "VerifyExit",
            tipo: TipoDeOcorrencia.Roubo,
            severidade: Severidade.Critica,
            descricao:
                $"A leitora da porta identificou **{produto}** saindo e o pagamento não está " +
                $"confirmado (sessão {statusDaSessao}). O alarme foi disparado na leitora.",
            recomendacao: AcaoRecomendada.BloquearOperacao,
            correlationId: sessaoId ?? Guid.NewGuid(),
            dadosEnvolvidosJson: Json(new { tagRfid, produto, sessaoId, statusDaSessao, quandoUtc }),
            impacto: "1 unidade saindo sem pagamento confirmado",
            // Tag + instante: a mesma tag passando de novo daqui a uma hora e
            // outro fato, nao o mesmo repetido.
            chave: $"saida-sem-pagamento:{tagRfid}:{quandoUtc.Ticks}");
    }

    /// <summary>
    /// Tag passou pela porta e nao corresponde a produto nenhum.
    /// </summary>
    /// <remarks>
    /// DUAS LEITURAS POSSIVEIS, e nenhuma delas e certeza — por isso as duas
    /// vao em `CausaProvavel`, marcadas como inferencia, e o tipo e
    /// `ErroDados` e nao `Roubo`. Chamar de roubo o que provavelmente e
    /// cadastro faltando ensinaria o Chefe a ignorar a palavra roubo.
    /// </remarks>
    public static Ocorrencia TagDesconhecidaNaPorta(string tagRfid, DateTime quandoUtc)
    {
        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: Loja,
            modulo: "SessionsController",
            operacao: "VerifyExit",
            tipo: TipoDeOcorrencia.ErroDados,
            severidade: Severidade.Alta,
            descricao:
                $"A leitora da porta leu a tag \"{tagRfid}\", que não corresponde a nenhum " +
                "produto cadastrado. A leitora tratou como suspeito e não liberou.",
            recomendacao: AcaoRecomendada.SolicitarAprovacao,
            dadosEnvolvidosJson: Json(new { tagRfid, quandoUtc }),
            causaProvavel:
                "Ou é um produto sem TagRfid vinculada no catálogo, ou é um item que não é " +
                "da loja. As duas são possíveis e o sistema não tem como separar. (Inferência.)",
            chave: $"tag-desconhecida:{tagRfid}:{quandoUtc.Ticks}");
    }

    /// <summary>
    /// Camera de prateleira olhou e nao viu mudanca, com sessao aberta.
    /// </summary>
    /// <remarks>
    /// NAO E CRIME, E CEGUEIRA. Uma sessao aberta significa que ha alguem na
    /// loja; a camera comparou antes e depois e nao achou diferenca. Pode ser
    /// que nada tenha saido mesmo — e ai isto e ruido. Por isso a severidade
    /// e `Informativa` e a acao e so registrar: o valor esta na SOMA, em
    /// descobrir qual prateleira acumula cegueira.
    /// </remarks>
    public static Ocorrencia CameraNaoViuMudanca(Guid sessaoId, DateTime quandoUtc)
    {
        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: Loja,
            modulo: "VisionController",
            operacao: "DetectShelfChange",
            tipo: TipoDeOcorrencia.FuroDeCobertura,
            severidade: Severidade.Informativa,
            descricao:
                "A câmera comparou antes e depois com uma sessão aberta e não identificou " +
                "mudança de produto. Pode não ter saído nada, ou pode ter saído algo fora " +
                "do alcance dela.",
            recomendacao: AcaoRecomendada.ApenasRegistrar,
            correlationId: sessaoId,
            dadosEnvolvidosJson: Json(new { sessaoId, quandoUtc }),
            chave: $"camera-sem-mudanca:{sessaoId}:{quandoUtc.Ticks}");
    }

    /// <summary>
    /// A visao identificou um produto que nao esta entre os monitorados.
    /// </summary>
    public static Ocorrencia ProdutoForaDoCatalogo(
        Guid sessaoId, string oQueFoiVisto, DateTime quandoUtc)
    {
        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: Loja,
            modulo: "VisionController",
            operacao: "DetectShelfChange",
            tipo: TipoDeOcorrencia.FuroDeCobertura,
            severidade: Severidade.Media,
            descricao:
                $"A câmera identificou \"{oQueFoiVisto}\", que não está entre os produtos " +
                "monitorados dessa prateleira. O item saiu e não entrou em carrinho nenhum.",
            recomendacao: AcaoRecomendada.SugerirCorrecao,
            correlationId: sessaoId,
            dadosEnvolvidosJson: Json(new { sessaoId, oQueFoiVisto, quandoUtc }),
            causaProvavel:
                "Ou o produto não está cadastrado, ou está fora da lista de ProductIds " +
                "configurada para essa prateleira. (Inferência.)",
            impacto: "1 unidade saiu da prateleira sem entrar em carrinho",
            chave: $"produto-fora-do-catalogo:{sessaoId}:{oQueFoiVisto}");
    }

    /// <summary>Excecao que subiu ate o topo da WebApi.</summary>
    /// <remarks>
    /// SEM CHAVE, DE PROPOSITO. Duas excecoes iguais em momentos diferentes
    /// sao dois fatos — a segunda pode ser justamente o sinal de que o
    /// problema virou rotina. Deduplicar aqui esconderia frequencia, que e
    /// metade da informacao.
    /// </remarks>
    public static Ocorrencia ErroDeExecucao(
        string caminho, string metodoHttp, Exception erro, Guid correlationId, DateTime quandoUtc)
    {
        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: Loja,
            modulo: erro.TargetSite?.DeclaringType?.Name ?? "WebApi",
            operacao: $"{metodoHttp} {caminho}",
            tipo: TipoDeOcorrencia.ErroExecucao,
            severidade: Severidade.Alta,
            descricao: $"{erro.GetType().Name}: {erro.Message}",
            recomendacao: AcaoRecomendada.ApenasRegistrar,
            correlationId: correlationId,

            // ISTO MUDOU, E MUDOU UMA DECISAO ANTERIOR.
            //
            // Antes a excecao nascia SEM chave, com o argumento de que duas
            // excecoes iguais em momentos diferentes sao dois fatos e que
            // agrupar esconderia frequencia.
            //
            // O argumento estava certo; a conclusao, nao. A frequencia agora
            // esta guardada em `VezesVistas`, entao agrupar nao esconde mais
            // nada — e a alternativa era um defeito em loop empurrar o
            // historico inteiro para fora da tela em minutos.
            chave: Cabe($"erro-execucao:{erro.GetType().Name}:{metodoHttp} {caminho}:{Assinatura(erro.Message)}"),
            dadosEnvolvidosJson: Json(new
            {
                caminho,
                metodoHttp,
                tipo = erro.GetType().FullName,
                mensagem = erro.Message,
                // A pilha vai TRUNCADA: o valor esta nos primeiros quadros, e
                // uma pilha inteira de recursao enche a coluna sozinha.
                pilha = erro.StackTrace is { Length: > 4000 }
                    ? erro.StackTrace[..4000] + "\n... (truncada)"
                    : erro.StackTrace,
                interna = erro.InnerException?.Message,
            }));
    }

    /// <summary>
    /// Um erro que estourou no NAVEGADOR — no app do cliente, do admin ou do
    /// suporte.
    /// </summary>
    /// <remarks>
    /// POR QUE ESTE DETECTOR E DIFERENTE DE TODOS OS OUTROS.
    ///
    /// Os demais nascem de algo que o servidor VIU. Este nasce de algo que o
    /// navegador CONTOU — e navegador e a maquina de outra pessoa. Nada aqui
    /// pode ser tratado como verdade sobre o sistema: `sistema` sai de uma
    /// lista fechada, e o resto entra como texto, do tamanho que a coluna
    /// aguenta e nada alem.
    ///
    /// A severidade e Alta e nao Critica: Critica acende o sino em vermelho e
    /// esta reservada para roubo. Uma tela que quebrou precisa de alguem hoje,
    /// nao agora — e se estiver acontecendo com todo mundo, quem grita e o
    /// contador de repeticoes, nao a cor.
    /// </remarks>
    public static Ocorrencia ErroNoNavegador(
        string app,
        string pagina,
        string mensagem,
        string? pilha,
        string? navegador,
        Guid correlationId,
        DateTime quandoUtc)
    {
        if (!AppsQueReportam.Contains(app))
            throw new ArgumentException($"App desconhecido: \"{app}\".", nameof(app));

        var texto = string.IsNullOrWhiteSpace(mensagem) ? "(erro sem mensagem)" : mensagem.Trim();
        var rota = string.IsNullOrWhiteSpace(pagina) ? "/" : pagina.Trim();

        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: app,
            modulo: "Navegador",
            operacao: Curto(rota, 120),
            tipo: TipoDeOcorrencia.ErroExecucao,
            severidade: Severidade.Alta,
            descricao: Curto(texto, 2000),
            recomendacao: AcaoRecomendada.ApenasRegistrar,
            correlationId: correlationId,
            dadosEnvolvidosJson: Json(new
            {
                app,
                pagina = rota,
                mensagem = Curto(texto, 2000),
                // Mesma regra do lado do servidor: o valor esta nos primeiros
                // quadros, e uma pilha de recursao enche a coluna sozinha.
                pilha = pilha is { Length: > 4000 } ? pilha[..4000] + "\n... (truncada)" : pilha,
                navegador = Curto(navegador ?? "", 300),
            }),
            chave: Cabe($"erro-navegador:{app}:{Assinatura(texto)}:{Assinatura(rota, 60)}"));
    }

    private static string Curto(string s, int limite) => s.Length <= limite ? s : s[..limite];

    /// <summary>Alguém escreveu para o suporte: uma dúvida ou um pedido de mudança.</summary>
    /// <remarks>
    /// O UNICO "DETECTOR" QUE NAO DETECTA NADA.
    ///
    /// Todos os outros aqui nascem de algo que o sistema viu sozinho. Este
    /// nasce de alguem digitando. Mora nesta classe assim mesmo porque o
    /// destino e o mesmo — a fila do tecnico — e porque a politica de
    /// classificacao continua sendo uma so: se o tipo e a gravidade de um
    /// pedido fossem decididos no controlador, seriam a unica decisao desse
    /// tipo fora deste arquivo.
    ///
    /// SEM CHAVE, DE PROPOSITO, E ESSA E A LINHA MAIS IMPORTANTE AQUI.
    ///
    /// Todo o resto desta classe agrupa fatos repetidos. Pedido NAO agrupa:
    /// duas pessoas perguntando a mesma coisa sao duas pessoas esperando
    /// resposta. Com chave, a segunda cairia dentro da conversa da primeira —
    /// que leria a resposta de um estranho, ou nao leria nada.
    ///
    /// A descricao guarda so o ASSUNTO. O texto vira a primeira mensagem da
    /// conversa: e ali que ele pertence, e e de la que a resposta sai.
    /// </remarks>
    public static Ocorrencia PedidoAoSuporte(
        string app,
        bool ehMudanca,
        string assunto,
        string texto,
        string quemNome,
        string quemEmail,
        string? paginaOndeEstava,
        DateTime quandoUtc)
    {
        if (!AppsQueReportam.Contains(app))
            throw new ArgumentException($"App desconhecido: \"{app}\".", nameof(app));
        if (string.IsNullOrWhiteSpace(quemEmail))
            throw new ArgumentException("Sem e-mail ninguém consegue ler a resposta.", nameof(quemEmail));

        var titulo = string.IsNullOrWhiteSpace(assunto)
            ? Curto(texto.Trim(), 120)
            : Curto(assunto.Trim(), 200);

        var pedido = new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: app,
            modulo: "Pedido ao suporte",
            // A página em que a pessoa estava é metade do contexto: "não
            // consigo mudar o preço" dito de /produtos e dito de /vendas são
            // dúvidas diferentes.
            operacao: Curto(string.IsNullOrWhiteSpace(paginaOndeEstava) ? "/" : paginaOndeEstava.Trim(), 120),
            tipo: ehMudanca ? TipoDeOcorrencia.PedidoDeMudanca : TipoDeOcorrencia.Duvida,
            // Baixa, e não Informativa: informativa é o que ninguém precisa
            // olhar hoje, e tem gente esperando resposta do outro lado.
            severidade: Severidade.Baixa,
            descricao: titulo,
            recomendacao: AcaoRecomendada.ApenasRegistrar,
            dadosEnvolvidosJson: Json(new { app, quemNome, quemEmail, pagina = paginaOndeEstava }),
            chave: null);

        pedido.AbertoPorAlguem(quemEmail);

        // Direto para a fila: ninguém detectou isto, alguém PEDIU. Esperar
        // que um humano despache o que já foi endereçado a mão seria só
        // atraso.
        pedido.EnviarAoSuporte(null);

        pedido.AdicionarMensagem(
            autor: app switch
            {
                "ClientApp" => AutorDaMensagem.Cliente,
                "SuporteApp" => AutorDaMensagem.Suporte,
                _ => AutorDaMensagem.Admin,
            },
            quemNome: quemNome,
            quemEmail: quemEmail,
            texto: texto,
            quandoUtc: quandoUtc);

        return pedido;
    }

    /// <summary>Um serviço de fora respondeu errado — o Gemini, o monitor, o que for.</summary>
    /// <remarks>
    /// O QUE ESTE DETECTOR RESOLVE.
    ///
    /// O assistente do cliente devolvia "não consegui falar com o agente
    /// (erro 404)" e ERA SÓ ISSO: o texto exato que o Google mandou de volta
    /// — que é onde estava a resposta — ia para o lixo. Nem log, nem
    /// ocorrência. Para descobrir o motivo era preciso ir mexer no código.
    ///
    /// Agora a falha vira linha no histórico, com o corpo da resposta dentro.
    /// Quem abrir a ocorrência lê o que o serviço de fora disse, com todas as
    /// letras.
    ///
    /// SEVERIDADE MÉDIA: o serviço de fora cair não quebra a loja — o cliente
    /// continua comprando sem o assistente. Alta ficaria ao lado de coisa que
    /// impede venda, e igualar as duas é como se aprende a ignorar as duas.
    ///
    /// A chave agrupa por serviço + operação + status: mil 404 seguidos do
    /// mesmo endpoint são UM problema com contador, e não mil linhas.
    /// </remarks>
    public static Ocorrencia FalhaDeIntegracao(
        string servico,
        string operacao,
        int status,
        string? corpoDaResposta,
        Guid correlationId,
        DateTime quandoUtc)
    {
        return new Ocorrencia(
            quandoUtc: quandoUtc,
            sistema: Loja,
            modulo: Curto(servico, 120),
            operacao: Curto(operacao, 120),
            tipo: TipoDeOcorrencia.FalhaIntegracao,
            severidade: Severidade.Media,
            descricao: $"{servico} respondeu {status} em {operacao}.",
            recomendacao: AcaoRecomendada.ApenasRegistrar,
            correlationId: correlationId,
            causaProvavel: status switch
            {
                404 => "Inferência: normalmente é o NOME DO MODELO ou o caminho da rota — o serviço "
                     + "existe, o recurso pedido não. Vale conferir contra a lista de modelos que a "
                     + "chave enxerga.",
                401 or 403 => "Inferência: credencial — chave errada, expirada, de outro tipo ou sem "
                            + "permissão para esta API.",
                429 => "Inferência: cota. Bateu no limite de chamadas do plano.",
                >= 500 => "Inferência: problema do lado deles. Costuma passar sozinho.",
                _ => null,
            },
            dadosEnvolvidosJson: Json(new
            {
                servico,
                operacao,
                status,
                // O CORPO É A METADE QUE FALTAVA. O status diz que deu errado;
                // o corpo diz o quê.
                resposta = corpoDaResposta is { Length: > 4000 }
                    ? corpoDaResposta[..4000] + "\n... (truncado)"
                    : corpoDaResposta,
            }),
            chave: Cabe($"falha-integracao:{servico}:{operacao}:{status}"));
    }
}
