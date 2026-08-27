using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// O CPF é a chave do cadastro. Aceitar um inválido cria um cliente que não pode
/// ser cobrado; recusar um válido impede alguém de entrar na loja.
/// </summary>
public class CpfValidationTests
{
    // CPFs válidos pelo algoritmo oficial, gerados para teste.
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    [InlineData("11144477735")]
    public void CpfValidoEAceitoComOuSemPontuacao(string cpf)
    {
        Assert.True(CpfValidation.IsValid(cpf));
    }

    [Theory]
    [InlineData("529.982.247-26")]   // primeiro dígito verificador errado
    [InlineData("111.444.777-30")]   // segundo dígito verificador errado
    [InlineData("12345678901")]
    public void DigitoVerificadorErradoERecusado(string cpf)
    {
        Assert.False(CpfValidation.IsValid(cpf));
    }

    /// <summary>
    /// Sequências repetidas passam na conta dos dígitos verificadores mas não são
    /// CPFs reais. É o erro clássico de quem implementa só o algoritmo.
    /// </summary>
    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("22222222222")]
    [InlineData("99999999999")]
    [InlineData("000.000.000-00")]
    public void SequenciaRepetidaERecusada(string cpf)
    {
        Assert.False(CpfValidation.IsValid(cpf));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void VazioERecusado(string? cpf)
    {
        Assert.False(CpfValidation.IsValid(cpf));
    }

    [Theory]
    [InlineData("5299822472")]        // 10 dígitos
    [InlineData("529982247251")]      // 12 dígitos
    [InlineData("abc")]
    public void QuantidadeDeDigitosErradaERecusada(string cpf)
    {
        Assert.False(CpfValidation.IsValid(cpf));
    }

    [Fact]
    public void LetrasNoMeioNaoAtrapalhamSeOsDigitosEstiveremCertos()
    {
        // A validação só olha os dígitos. Isto documenta o comportamento atual:
        // se um dia isso virar problema, este teste é o lugar de mudar a decisão.
        Assert.True(CpfValidation.IsValid("529abc982cba247de25"));
    }

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData("529 982 247 25", "52998224725")]
    public void NormalizarDeixaSoOsOnzeDigitos(string entrada, string esperado)
    {
        Assert.Equal(esperado, CpfValidation.Normalize(entrada));
    }
}
