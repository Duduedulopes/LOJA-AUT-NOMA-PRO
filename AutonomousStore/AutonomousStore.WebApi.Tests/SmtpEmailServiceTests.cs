using AutonomousStore.WebApi.Services;
using Microsoft.Extensions.Logging;

namespace AutonomousStore.WebApi.Tests;

/// <summary>
/// O interruptor <c>Email:Habilitado</c>. Existe por causa de um erro real: a API rodando em Development com a senha SMTP de
/// verdade mandou "Bem-vindo(a)" para endereços de teste — inclusive de terceiros. Aqui se prova que em Development o padrão
/// é DESLIGADO, que fora dele continua LIGADO como sempre foi, e que quem liga, liga de propósito.
///
/// Nenhum destes testes envia e-mail de verdade: onde o serviço "tenta enviar", o servidor SMTP é uma porta local fechada.
/// </summary>
public class SmtpEmailServiceTests
{
    private static (SmtpEmailService Servico, LogFalso<SmtpEmailService> Log) Montar(string ambiente, string? habilitado)
    {
        // Credenciais PREENCHIDAS, como no appsettings.Development.json do dono — é o cenario perigoso: com a senha
        // ali, so o interruptor separa "testar" de "mandar e-mail de verdade". O servidor e uma porta local fechada.
        var itens = new List<(string, string?)>
        {
            ("Email:SenderEmail", "remetente@teste.invalid"),
            ("Email:SenderPassword", "senha-de-teste"),
            ("Email:SmtpHost", "127.0.0.1"),
            ("Email:SmtpPort", Configuracao.PortaLocalFechada().ToString()),
        };
        if (habilitado is not null) itens.Add(("Email:Habilitado", habilitado));

        var log = new LogFalso<SmtpEmailService>();
        return (new SmtpEmailService(Configuracao.De(itens.ToArray()), new AmbienteFalso(ambiente), log), log);
    }

    // ═══════════════════════════════════════════ o padrão de cada ambiente
    [Fact]
    public void EmDevelopmentSemConfigurarOPadraoEDesligado()
    {
        Assert.False(SmtpEmailService.EstaHabilitado(Configuracao.De(), new AmbienteFalso("Development")));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Homologacao")]
    public void ForaDeDevelopmentSemConfigurarOPadraoELigado(string ambiente)
    {
        // O comportamento de sempre em producao: quem nao mexe em nada continua recebendo e-mail.
        Assert.True(SmtpEmailService.EstaHabilitado(Configuracao.De(), new AmbienteFalso(ambiente)));
    }

    // ═══════════════════════════════════════ quem escolhe, manda
    [Fact]
    public void QuemLigaEmDevelopmentLigaDePropositoEFunciona()
    {
        var config = Configuracao.De(("Email:Habilitado", "true"));

        Assert.True(SmtpEmailService.EstaHabilitado(config, new AmbienteFalso("Development")));
    }

    [Fact]
    public void QuemDesligaEmProducaoDesliga()
    {
        var config = Configuracao.De(("Email:Habilitado", "false"));

        Assert.False(SmtpEmailService.EstaHabilitado(config, new AmbienteFalso("Production")));
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void OBooleanoNaoDependeDeMaiusculaOuMinuscula(string valor)
    {
        Assert.True(SmtpEmailService.EstaHabilitado(Configuracao.De(("Email:Habilitado", valor)), new AmbienteFalso("Development")));
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public void ValorIlegivelCaiNoPadraoDoAmbiente(string ambiente, bool esperado)
    {
        // "talvez", vazio, "sim": nao e booleano. Na duvida, o que e SEGURO naquele ambiente — e em Development, seguro e desligado.
        foreach (var lixo in new[] { "talvez", "", "sim", "1 " })
            Assert.Equal(esperado, SmtpEmailService.EstaHabilitado(Configuracao.De(("Email:Habilitado", lixo)), new AmbienteFalso(ambiente)));
    }

    // ═══════════════════════════════════ o que o serviço FAZ, ligado ou não
    [Fact]
    public async Task DesligadoNaoTentaEnviarMesmoComASenhaPreenchida()
    {
        var (servico, log) = Montar("Development", habilitado: null);

        await servico.SendWelcomeEmailAsync("alguem@teste.invalid", "Maria");

        Assert.True(log.Disse(LogLevel.Warning, "NÃO enviado para alguem@teste.invalid"));
        Assert.True(log.Disse(LogLevel.Warning, "desligado"));

        // O ponto: nenhuma tentativa de conexao. "Falha ao enviar" e o que o servico loga quando TENTOU e nao conseguiu.
        Assert.DoesNotContain(log.Linhas, l => l.Nivel == LogLevel.Error);
    }

    [Fact]
    public async Task LigadoDePropositoTentaEnviarEPorIssoFalhaContraAPortaFechada()
    {
        var (servico, log) = Montar("Development", habilitado: "true");

        await servico.SendWelcomeEmailAsync("alguem@teste.invalid", "Maria");

        // Tentou (e a porta local recusou): prova de que "ligado" de fato liga. Sem este teste, um interruptor que
        // desligasse TUDO para sempre passaria em todos os outros.
        Assert.True(log.Disse(LogLevel.Error, "Falha ao enviar e-mail de boas-vindas"));
        Assert.DoesNotContain(log.Linhas, l => l.Texto.Contains("NÃO enviado"));
    }

    [Fact]
    public async Task ProducaoSemConfigurarTentaEnviarComoSempre()
    {
        var (servico, log) = Montar("Production", habilitado: null);

        await servico.SendWelcomeEmailAsync("alguem@teste.invalid", "Maria");

        Assert.True(log.Disse(LogLevel.Error, "Falha ao enviar e-mail de boas-vindas"));
    }

    [Fact]
    public async Task ProducaoDesligadaNaoTentaEnviar()
    {
        var (servico, log) = Montar("Production", habilitado: "false");

        await servico.SendWelcomeEmailAsync("alguem@teste.invalid", "Maria");

        Assert.True(log.Disse(LogLevel.Warning, "NÃO enviado"));
        Assert.DoesNotContain(log.Linhas, l => l.Nivel == LogLevel.Error);
    }

    // ═══════════════════════════════ o link de redefinição, sem e-mail
    [Fact]
    public async Task EmDevelopmentComEmailDesligadoOLinkDeRedefinicaoVaiParaOLog()
    {
        var (servico, log) = Montar("Development", habilitado: null);

        await servico.SendPasswordResetEmailAsync("alguem@teste.invalid", "Maria", "https://loja.teste/redefinir-senha?token=ABC");

        // Sem isto "esqueci minha senha" seria impossivel de testar: o link so existia dentro do e-mail.
        Assert.True(log.Disse(LogLevel.Information, "https://loja.teste/redefinir-senha?token=ABC"));
        Assert.DoesNotContain(log.Linhas, l => l.Nivel == LogLevel.Error);       // e nada foi enviado
    }

    [Fact]
    public async Task EmProducaoDesligadaOLinkNuncaVaiParaOLog()
    {
        // O link e uma credencial de meia hora. Log de producao nao pode te-la.
        var (servico, log) = Montar("Production", habilitado: "false");

        await servico.SendPasswordResetEmailAsync("alguem@teste.invalid", "Maria", "https://loja.teste/redefinir-senha?token=ABC");

        Assert.DoesNotContain(log.Linhas, l => l.Texto.Contains("token=ABC"));
    }

    [Fact]
    public async Task ComOEmailLigadoOLinkVaiSoNoEmailENuncaNoLog()
    {
        var (servico, log) = Montar("Development", habilitado: "true");

        await servico.SendPasswordResetEmailAsync("alguem@teste.invalid", "Maria", "https://loja.teste/redefinir-senha?token=ABC");

        Assert.DoesNotContain(log.Linhas, l => l.Texto.Contains("token=ABC"));
    }
}
