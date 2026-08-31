using System.Globalization;
using System.Text;
using AutonomousStore.AdminApp.Models;

namespace AutonomousStore.AdminApp.Services.Agente;

/// <summary>
/// Uma ordem em andamento: o que o Chefe pediu, o que ja se sabe, e o que
/// ainda falta perguntar antes de gravar.
/// </summary>
/// <remarks>
/// O que o Eduardo pediu, e onde cada parte virou codigo:
///
///     "usar os dados que ele possui"     `LeitorDeValores`, que tira
///                                        produto e numero da propria frase
///     "verificar se e isso mesmo"        `Resumo`, com o dado real do
///                                        catalogo: de quanto, para quanto
///     "pede a confirmacao para alterar"  `Passo.Confirmando` — nada grava
///                                        antes disso
///     "ou volta para outra pergunta"     cancelar nao encerra: devolve a
///                                        conversa
///
/// A REGRA QUE NAO SE NEGOCIA: acao sempre confirma, mesmo com 99% de
/// confianca. Consulta errada mostra um numero errado e a pessoa olha de
/// novo; ordem errada escreve no banco.
///
/// `Operacao` guarda o nome que a REDE deu — `alterar_preco`, e nao
/// "alterar". Ver a nota em `GerenteService.ProcessarComoAgenteAsync`.
/// </remarks>
public class Conversa
{
    public enum Passo
    {
        /// <summary>Ainda faltam dados. O gerente esta perguntando.</summary>
        Coletando,

        /// <summary>Tudo na mao. Falta o Chefe dizer sim.</summary>
        Confirmando,
    }

    /// <summary>Um dado que a operacao precisa ter antes de gravar.</summary>
    public record Campo(string Nome, string Pergunta, TipoDeValor Tipo);

    public enum TipoDeValor { Produto, Dinheiro, Inteiro, Texto }

    /// <summary>O nome que a REDE deu para a ordem. Nunca um nome derivado aqui.</summary>
    public string Operacao { get; }

    /// <summary>A frase original, guardada porque ela costuma ter o que falta.</summary>
    public string Pedido { get; }

    public Dictionary<string, string> Dados { get; } = new();
    public Passo Onde { get; private set; } = Passo.Coletando;

    /// <summary>O produto do catalogo, quando ja resolvido. E o dado real.</summary>
    public ProductDto? Produto { get; set; }

    private readonly List<Campo> _campos;
    private int _insistencias;

    /// <summary>Quantas vezes o gerente reperguntou a MESMA coisa sem entender.</summary>
    /// <remarks>
    /// Sem um teto, uma resposta que o gerente nao consegue ler vira um
    /// loop: ele pergunta, nao entende, pergunta igual. Tres e o limite
    /// que o `LoopDeFeedback` ja usava; mantido para nao ter dois numeros
    /// diferentes para a mesma ideia.
    /// </remarks>
    public bool Desistiu => _insistencias >= 3;

    public Conversa(string operacao, string pedido, List<Campo> campos)
    {
        Operacao = operacao;
        Pedido = pedido;
        _campos = campos;
    }

    public Campo? Falta => _campos.FirstOrDefault(c => !Dados.ContainsKey(c.Nome));
    public bool Completa => Falta is null;

    public void Guardar(string campo, string valor)
    {
        Dados[campo] = valor;
        _insistencias = 0;
    }

    public void NaoEntendi() => _insistencias++;
    public void VaiConfirmar() { Onde = Passo.Confirmando; _insistencias = 0; }

    /// <summary>Volta a perguntar um campo — o Chefe disse que esta errado.</summary>
    public void Refazer(string campo)
    {
        Dados.Remove(campo);
        if (campo == "produto") Produto = null;
        Onde = Passo.Coletando;
        _insistencias = 0;
    }

    // ══════════════════════════════════════════════════════════════════
    //  OS CAMPOS DE CADA OPERACAO
    // ══════════════════════════════════════════════════════════════════

    /// <summary>O que cada ordem precisa saber. Vazio = operacao sem dialogo.</summary>
    /// <remarks>
    /// As chaves sao os nomes DA REDE. Os nomes dos campos batem com os que
    /// os executores em `GerenteService` ja leem do dicionario ("nome",
    /// "preco", "quantidade") — a traducao acontece em `ParaExecutor`, num
    /// lugar so, em vez de espalhada.
    /// </remarks>
    public static List<Campo>? CamposDe(string operacao) => operacao switch
    {
        "alterar_preco" => new()
        {
            new("produto", "Qual produto?", TipoDeValor.Produto),
            new("preco", "Qual o novo preço?", TipoDeValor.Dinheiro),
        },
        "alterar_estoque" => new()
        {
            new("produto", "Qual produto?", TipoDeValor.Produto),
            new("quantidade", "Quantas unidades no total?", TipoDeValor.Inteiro),
        },
        "adicionar_produto" => new()
        {
            new("nome", "Qual o nome do produto?", TipoDeValor.Texto),
            new("preco", "Por quanto vai vender?", TipoDeValor.Dinheiro),
            new("quantidade", "Quantas unidades entram no estoque?", TipoDeValor.Inteiro),
        },
        // `remover_produto` NAO entra aqui de proposito.
        //
        // A WebApi nao tem endpoint de remocao. Pedir "posso remover?",
        // ouvir "sim" e responder "na verdade eu nao consigo" e pior do que
        // nao ter perguntado: gasta a confianca do Chefe numa promessa que
        // o sistema nao pode cumprir. Enquanto nao existir o endpoint, ela
        // segue pelo caminho antigo, que explica as alternativas.
        _ => null,
    };

    /// <summary>O dicionario no formato que os executores ja esperam.</summary>
    public Dictionary<string, object> ParaExecutor()
    {
        var d = new Dictionary<string, object>();
        foreach (var (k, v) in Dados)
            d[k == "produto" ? "nome" : k] = v;
        // O executor procura por nome; se o produto ja foi resolvido contra
        // o catalogo, manda o nome exato dele e nao o que o Chefe digitou.
        if (Produto is not null) d["nome"] = Produto.Name;
        return d;
    }
}
