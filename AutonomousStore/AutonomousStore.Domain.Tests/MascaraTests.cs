using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// O que o técnico vê no lugar do dado pessoal. A máscara precisa cumprir dois
/// papéis ao mesmo tempo: deixar o suficiente para o técnico reconhecer a
/// pessoa, e esconder o suficiente para o dado não ser usável.
/// </summary>
public class MascaraTests
{
    // ---------------------------------------------------------------- e-mail
    [Theory]
    [InlineData("maria.silva@gmail.com", "m***@gmail.com")]
    [InlineData("  Maria@Exemplo.com.br ", "M***@Exemplo.com.br")]
    [InlineData("a@x.com", "a***@x.com")]
    public void EmailMostraSoAPrimeiraLetraEODominio(string email, string esperado)
    {
        Assert.Equal(esperado, Mascara.Email(email));
    }

    [Fact]
    public void EmailNaoDeixaVazarOnomeDoUsuario()
    {
        var mascarado = Mascara.Email("maria.silva@gmail.com")!;

        Assert.DoesNotContain("maria", mascarado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("silva", mascarado, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("semarroba")]
    [InlineData("@semusuario.com")]
    public void EmailForaDoFormatoEscondeTudo(string email)
    {
        Assert.Equal("***", Mascara.Email(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmailVazioContinuaVazio(string? email)
    {
        Assert.Equal(email, Mascara.Email(email));
    }

    // ------------------------------------------------------------------- cpf
    [Theory]
    [InlineData("52998224725", "***.***.***-25")]
    [InlineData("529.982.247-25", "***.***.***-25")]
    public void CpfMostraSoOsDoisUltimosDigitos(string cpf, string esperado)
    {
        Assert.Equal(esperado, Mascara.Cpf(cpf));
    }

    [Fact]
    public void CpfNaoDeixaVazarOsDigitosDoMeio()
    {
        var mascarado = Mascara.Cpf("52998224725")!;

        Assert.DoesNotContain("529", mascarado);
        Assert.DoesNotContain("982", mascarado);
        Assert.DoesNotContain("247", mascarado);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abc")]
    public void CpfComTamanhoErradoEscondeTudo(string cpf)
    {
        Assert.Equal("***", Mascara.Cpf(cpf));
    }

    // -------------------------------------------------------------- telefone
    [Theory]
    [InlineData("11999991234", "(**) *****-1234")]
    [InlineData("(11) 99999-1234", "(**) *****-1234")]
    [InlineData("1133334444", "(**) *****-4444")]
    public void TelefoneMostraSoOsQuatroUltimosDigitos(string telefone, string esperado)
    {
        Assert.Equal(esperado, Mascara.Telefone(telefone));
    }

    [Fact]
    public void TelefoneCurtoDemaisEscondeTudo()
    {
        Assert.Equal("***", Mascara.Telefone("123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TelefoneVazioContinuaVazio(string? telefone)
    {
        Assert.Equal(telefone, Mascara.Telefone(telefone));
    }
}
