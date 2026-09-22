using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Infrastructure.Repositories;

namespace AutonomousStore.Infrastructure.Tests;

/// <summary>
/// As consultas que alimentam o painel do Criador: a equipe, os compradores por empresa, e o suporte (a fila e o que foi
/// resolvido). Quem enxerga todas as empresas conta todas; quem é de uma empresa só conta a dela.
/// </summary>
public class ConsultasDoPainelTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();

    public void Dispose() => _banco.Dispose();

    private static SuporteUser Tecnico(string nome, string email, string cpf)
        => new(nome, email, "11999999999", cpf, "hash-qualquer");

    private void ComprarEm(Guid empresaId, string email, string cpf)
    {
        using var contexto = _banco.ComoEmpresa(empresaId);
        contexto.Customers.Add(Customer.RegisterWithPassword("Fulano", email, "11999999999", cpf, "hash-qualquer"));
        contexto.SaveChanges();
    }

    private static Ocorrencia NoSuporte(Severidade severidade, string chave, int horasAtras = 2)
    {
        var o = new Ocorrencia(
            DateTime.UtcNow.AddHours(-horasAtras), "WebApi", "Modulo", "Operacao", TipoDeOcorrencia.ErroExecucao, severidade,
            "algo deu errado", AcaoRecomendada.ApenasRegistrar, chave: chave);
        o.EnviarAoSuporte("preciso de ajuda");
        return o;
    }

    private static Ocorrencia Nova(string chave)
        => new(DateTime.UtcNow, "WebApi", "Modulo", "Operacao", TipoDeOcorrencia.ErroExecucao, Severidade.Media,
               "algo deu errado", AcaoRecomendada.ApenasRegistrar, chave: chave);

    private void Gravar(Guid? empresaId, params Ocorrencia[] ocorrencias)
    {
        using var contexto = empresaId is { } id ? _banco.ComoEmpresa(id) : _banco.ComoPlataforma();
        contexto.Ocorrencias.AddRange(ocorrencias);
        contexto.SaveChanges();
    }

    // ═══════════════════════════════════════════════════════════ a equipe
    [Fact]
    public async Task AEquipeVemEmOrdemDeNome_InclusiveOsDesativados()
    {
        using (var contexto = _banco.ComoPlataforma())
        {
            var zeca = Tecnico("Zeca", "zeca@suporte.invalid", "52998224725");
            var ana = Tecnico("Ana", "ana@suporte.invalid", "11144477735");
            zeca.Deactivate();
            contexto.Add(zeca);
            contexto.Add(ana);
            contexto.SaveChanges();
        }

        using var leitura = _banco.ComoPlataforma();
        var equipe = await new SuporteUserRepository(leitura).ListarAsync();

        Assert.Equal(new[] { "Ana", "Zeca" }, equipe.Select(t => t.Name).ToArray());
        Assert.Equal(new[] { true, false }, equipe.Select(t => t.IsActive).ToArray());     // quem saiu continua na lista, marcado
    }

    [Fact]
    public async Task SemTecnicosAListaVemVazia()
    {
        using var contexto = _banco.ComoPlataforma();

        Assert.Empty(await new SuporteUserRepository(contexto).ListarAsync());
    }

    // ═══════════════════════════════════════════ compradores por empresa
    [Fact]
    public async Task ContaOsCompradoresDeCadaEmpresa()
    {
        ComprarEm(_banco.EmpresaA, "um@a.invalid", "52998224725");
        ComprarEm(_banco.EmpresaA, "dois@a.invalid", "11144477735");
        ComprarEm(_banco.EmpresaB, "um@b.invalid", "52998224725");      // o mesmo CPF em OUTRA empresa e outro cadastro

        using var criador = _banco.ComoPlataforma();
        var contagem = await new CustomerRepository(criador).ContarPorEmpresaAsync();

        Assert.Equal(2, contagem[_banco.EmpresaA]);
        Assert.Equal(1, contagem[_banco.EmpresaB]);
    }

    [Fact]
    public async Task EmpresaSemCompradoresNaoApareceNaContagem()
    {
        ComprarEm(_banco.EmpresaA, "um@a.invalid", "52998224725");

        using var criador = _banco.ComoPlataforma();
        var contagem = await new CustomerRepository(criador).ContarPorEmpresaAsync();

        Assert.False(contagem.ContainsKey(_banco.EmpresaB));
        Assert.Single(contagem);
    }

    [Fact]
    public async Task OAdminSoContaOsCompradoresDaPropriaEmpresa()
    {
        ComprarEm(_banco.EmpresaA, "um@a.invalid", "52998224725");
        ComprarEm(_banco.EmpresaB, "um@b.invalid", "11144477735");

        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);
        var contagem = await new CustomerRepository(admin).ContarPorEmpresaAsync();

        // O filtro do banco vale aqui também: a B nem aparece para o Admin da A.
        Assert.Equal(new[] { _banco.EmpresaA }, contagem.Keys.ToArray());
    }

    // ═════════════════════════════════════════════════ a fila do suporte
    [Fact]
    public async Task AFilaTrazSoOQueFoiParaOSuporte_DeTodasAsEmpresas_ComAGravidadeEAIdade()
    {
        Gravar(_banco.EmpresaA, NoSuporte(Severidade.Alta, "a1", horasAtras: 5), NoSuporte(Severidade.Baixa, "a2"), Nova("a3"));
        Gravar(_banco.EmpresaB, NoSuporte(Severidade.Critica, "b1"));
        Gravar(null, NoSuporte(Severidade.Media, "p1"));                 // fato da plataforma: sem empresa

        using var criador = _banco.ComoPlataforma();
        var fila = await new OcorrenciaRepository(criador).NaFilaDoSuporteAsync();

        Assert.Equal(4, fila.Count);                                      // a "Nova" (que ninguem mandou ao suporte) nao entra
        Assert.Equal(2, fila.Count(f => f.TenantId == _banco.EmpresaA));
        Assert.Single(fila, f => f.TenantId == _banco.EmpresaB && f.Severidade == Severidade.Critica);
        Assert.Single(fila, f => f.TenantId is null && f.Severidade == Severidade.Media);
        Assert.True(fila.Single(f => f.Severidade == Severidade.Alta).QuandoUtc < DateTime.UtcNow.AddHours(-4));
    }

    [Fact]
    public async Task ONaoCriadorSoVeAFilaDaPropriaEmpresa()
    {
        Gravar(_banco.EmpresaA, NoSuporte(Severidade.Alta, "a1"));
        Gravar(_banco.EmpresaB, NoSuporte(Severidade.Alta, "b1"), NoSuporte(Severidade.Baixa, "b2"));

        using var adminDaA = _banco.ComoEmpresa(_banco.EmpresaA);
        var fila = await new OcorrenciaRepository(adminDaA).NaFilaDoSuporteAsync();

        Assert.Single(fila);
        Assert.Equal(_banco.EmpresaA, fila[0].TenantId);
    }

    // ══════════════════════════════════════════════ o que foi resolvido
    [Fact]
    public async Task ResolvidasTrazSoAsResolvidas_ComQuemResolveu_EIgnoraAsIgnoradasEAsNovas()
    {
        var resolvida = NoSuporte(Severidade.Media, "r1");
        resolvida.Resolver("Bia", "feito");
        var ignorada = NoSuporte(Severidade.Baixa, "r2");
        ignorada.Ignorar("Carlos", "nao e problema");
        var daB = NoSuporte(Severidade.Alta, "r3");
        daB.Resolver("Bia", null);

        Gravar(_banco.EmpresaA, resolvida, ignorada, Nova("r4"));
        Gravar(_banco.EmpresaB, daB);

        using var criador = _banco.ComoPlataforma();
        var lista = await new OcorrenciaRepository(criador).ResolvidasDesdeAsync(DateTime.UtcNow.AddDays(-1));

        Assert.Equal(2, lista.Count);
        Assert.All(lista, r => Assert.Equal("Bia", r.ResolvidaPor));       // o "Carlos" ignorou, nao resolveu
        Assert.All(lista, r => Assert.True(r.ResolvidaEm >= r.QuandoUtc));
    }

    [Fact]
    public async Task ResolvidasRespeitaADataDeCorte()
    {
        var resolvida = NoSuporte(Severidade.Media, "r1");
        resolvida.Resolver("Bia", null);
        Gravar(_banco.EmpresaA, resolvida);

        using var criador = _banco.ComoPlataforma();
        var repositorio = new OcorrenciaRepository(criador);

        Assert.Single(await repositorio.ResolvidasDesdeAsync(DateTime.UtcNow.AddDays(-1)));
        Assert.Empty(await repositorio.ResolvidasDesdeAsync(DateTime.UtcNow.AddMinutes(5)));   // corte no futuro: nada
    }
}
