using System.Text.Json;
using AutonomousStore.WebApi.Services;

namespace AutonomousStore.WebApi.Tests;

/// <summary>
/// A máscara aplicada ao JSON de "detalhes técnicos" de uma ocorrência, para o técnico. Só o pedido ao suporte guarda o e-mail
/// de quem pediu ajuda (<c>quemEmail</c>); o resto do JSON tem de sair EXATAMENTE como entrou.
/// </summary>
public class MascaraParaSuporteTests
{
    private static JsonElement Ler(string? json) => JsonDocument.Parse(json!).RootElement;

    [Fact]
    public void MascaraOEmailEMantemONome()
    {
        var saida = MascaraParaSuporte.DadosEnvolvidos(
            """{"app":"ClientApp","quemNome":"Maria Souza","quemEmail":"maria.souza@gmail.com","pagina":"/carrinho"}""");

        var json = Ler(saida);
        Assert.Equal("m***@gmail.com", json.GetProperty("quemEmail").GetString());
        Assert.Equal("Maria Souza", json.GetProperty("quemNome").GetString());     // o nome o tecnico ve
        Assert.Equal("ClientApp", json.GetProperty("app").GetString());
        Assert.Equal("/carrinho", json.GetProperty("pagina").GetString());
    }

    [Fact]
    public void OEmailCompletoNaoSobraEmLugarNenhum()
    {
        var saida = MascaraParaSuporte.DadosEnvolvidos("""{"quemEmail":"maria.souza@gmail.com"}""")!;

        Assert.DoesNotContain("maria.souza", saida);
        Assert.DoesNotContain("souza@", saida);
    }

    [Fact]
    public void DetalheSemEmailSaiIgualAoQueEntrou()
    {
        // Os outros detectores gravam tag de RFID, id de sessao e produto: nao identificam pessoa e nao mudam.
        var entrada = """{"tagRfid":"04A1B2","produto":"Agua 500ml","sessaoId":"7f3c","statusDaSessao":"Aberta"}""";

        var saida = MascaraParaSuporte.DadosEnvolvidos(entrada);

        Assert.Equal(Ler(entrada).ToString(), Ler(saida).ToString());
        Assert.Equal("04A1B2", Ler(saida).GetProperty("tagRfid").GetString());
    }

    [Fact]
    public void AcentosNaoViramSequenciaDeEscape()
    {
        // A tela mostra este texto ao tecnico: "Conveniência" nao pode chegar como "Conveniência".
        var saida = MascaraParaSuporte.DadosEnvolvidos("""{"quemEmail":"a@b.com","mensagem":"Conveniência e pão"}""")!;

        Assert.Contains("Conveniência e pão", saida);
        Assert.DoesNotContain("\\u00", saida);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VazioContinuaVazio(string? entrada)
    {
        Assert.Equal(entrada, MascaraParaSuporte.DadosEnvolvidos(entrada));
    }

    [Theory]
    [InlineData("isto nao e json")]
    [InlineData("{quebrado")]
    [InlineData("[1,2,3]")]                 // JSON valido, mas nao e um objeto: nao ha campo para mascarar
    [InlineData("42")]
    public void JsonQueNaoDaParaLerVoltaComoEstava(string entrada)
    {
        // E gerado pelo proprio sistema. Esconder um detalhe tecnico que ninguem consegue ler nao protegeria ninguem.
        Assert.Equal(entrada, MascaraParaSuporte.DadosEnvolvidos(entrada));
    }

    [Fact]
    public void QuemEmailQueNaoETextoNaoQuebra()
    {
        var entrada = """{"quemEmail":12345,"quemNome":"Maria"}""";

        var saida = MascaraParaSuporte.DadosEnvolvidos(entrada);

        Assert.Equal(12345, Ler(saida).GetProperty("quemEmail").GetInt32());
    }

    [Fact]
    public void QuemEmailNuloContinuaNulo()
    {
        var saida = MascaraParaSuporte.DadosEnvolvidos("""{"quemEmail":null,"quemNome":"Maria"}""");

        Assert.Equal(JsonValueKind.Null, Ler(saida).GetProperty("quemEmail").ValueKind);
    }

    [Fact]
    public void OEmailAvulsoUsaAMesmaMascaraDoDominio()
    {
        Assert.Equal("m***@gmail.com", MascaraParaSuporte.Email("maria.souza@gmail.com"));
        Assert.Null(MascaraParaSuporte.Email(null));
    }
}
