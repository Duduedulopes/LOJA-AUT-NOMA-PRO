using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AutonomousStore.Gerente.Models;

namespace AutonomousStore.Gerente.Services.Agente;

/// <summary>
/// Tira da frase o que ela ja carrega: qual produto, qual numero.
/// </summary>
/// <remarks>
/// ISTO NAO E CLASSIFICAR INTENCAO, E LER UM VALOR. A rede ja disse "ele
/// quer alterar um preco"; aqui so falta saber QUAL produto e QUANTO — a
/// mesma diferenca entre entender o pedido e anotar o numero.
///
/// A ARMADILHA QUE ESTE ARQUIVO EVITA: produto tem numero no nome. "Coca
/// 2L", "Agua 500ml". Procurar numero na frase inteira faz "muda o preco da
/// coca 2l" virar preco = 2, e o Chefe descobre no caixa. Por isso o nome
/// do produto SAI da frase antes da procura de numero.
/// </remarks>
public static class LeitorDeValores
{
    /// <summary>Sem acento, minusculo, so letra e numero.</summary>
    public static string Normalizar(string s)
    {
        var d = (s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c == ',' || c == '.' ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Acha na frase um produto DO CATALOGO. So vale produto que existe.
    /// </summary>
    /// <remarks>
    /// Casar por palavra de 4+ letras, e nao por `Contains` do texto todo:
    /// `Contains("")` casa com o primeiro produto da lista, e um nome vazio
    /// chegando aqui alteraria o produto errado sem ninguem notar. Foi o
    /// que `AlterarPrecoAgenteAsync` fazia quando recebia o dicionario
    /// vazio que o agente mandava.
    ///
    /// Devolve `Varios` quando mais de um produto casa. Ai nao se escolhe:
    /// pergunta. Escolher o primeiro seria adivinhar em cima de uma ordem
    /// de escrita.
    /// </remarks>
    public static (ProductDto? Achado, List<ProductDto> Varios) Produto(
        IEnumerable<ProductDto> catalogo, string frase)
    {
        var t = Normalizar(frase);
        var casaram = catalogo.Where(p =>
            Normalizar(p.Name).Split(' ')
                .Where(w => w.Length >= 4)
                .Any(w => t.Split(' ').Contains(w)))
            .ToList();

        if (casaram.Count == 1) return (casaram[0], casaram);
        return (null, casaram);
    }

    /// <summary>A frase sem as palavras do nome do produto.</summary>
    /// <remarks>Para que "coca 2l" nao doe o seu 2 para o campo de preco.</remarks>
    public static string SemOProduto(string frase, ProductDto? produto)
    {
        if (produto is null) return Normalizar(frase);
        var doNome = Normalizar(produto.Name).Split(' ').ToHashSet();
        return string.Join(' ', Normalizar(frase).Split(' ').Where(w => !doNome.Contains(w)));
    }

    // ══════════════════════════════════════════════════════════════════
    //  NUMEROS
    // ══════════════════════════════════════════════════════════════════

    private static readonly Regex NUMERO = new(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    /// <summary>Tudo que parece numero na frase, ja convertido.</summary>
    private static List<(string Cru, decimal Valor, bool TemCentavos)> Numeros(string t)
    {
        var saida = new List<(string, decimal, bool)>();
        foreach (Match m in NUMERO.Matches(t))
        {
            var cru = m.Value;
            var centavos = cru.Contains(',') || cru.Contains('.');
            // pt-BR: a virgula e decimal. O ponto tambem, aqui — ninguem
            // digita separador de milhar num chat, e "5.50" e o teclado
            // numerico do Chefe, nao cinco mil e cinquenta.
            var limpo = cru.Replace(',', '.');
            if (decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                saida.Add((cru, v, centavos));
        }
        return saida;
    }

    /// <summary>Um valor em dinheiro, se a frase tiver um so que sirva.</summary>
    /// <remarks>
    /// Devolve nulo quando ha DUVIDA, e duvida aqui inclui "achei dois
    /// numeros". "adiciona 10 caixas de leite a 8,90" tem os dois; chutar
    /// qual e o preco grava o numero errado. Pergunta-se.
    ///
    /// A desempate so acontece com sinal explicito: quem tem virgula ou
    /// vem depois de R$ e preco.
    /// </remarks>
    public static decimal? Dinheiro(string frase)
    {
        var t = Normalizar(frase);
        var ns = Numeros(t);
        if (ns.Count == 0) return null;
        if (ns.Count == 1) return ns[0].Valor;

        var comCentavos = ns.Where(n => n.TemCentavos).ToList();
        if (comCentavos.Count == 1) return comCentavos[0].Valor;

        return null;   // dois candidatos igualmente plausiveis: nao se chuta
    }

    /// <summary>Uma quantidade inteira, se a frase tiver uma so que sirva.</summary>
    public static int? Inteiro(string frase)
    {
        var t = Normalizar(frase);
        var ns = Numeros(t);
        if (ns.Count == 0) return null;

        // Quantidade e inteira. Um numero com centavos quase nunca e
        // quantidade — serve de desempate quando ha os dois.
        var inteiros = ns.Where(n => !n.TemCentavos && n.Valor == decimal.Truncate(n.Valor)).ToList();
        if (inteiros.Count == 1) return (int)inteiros[0].Valor;
        if (inteiros.Count == 0 && ns.Count == 1 && ns[0].Valor == decimal.Truncate(ns[0].Valor))
            return (int)ns[0].Valor;

        return null;
    }
}
