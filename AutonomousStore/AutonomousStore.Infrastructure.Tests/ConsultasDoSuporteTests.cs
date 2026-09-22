using AutonomousStore.Domain.Entities;
using AutonomousStore.Infrastructure.Repositories;

namespace AutonomousStore.Infrastructure.Tests;

/// <summary>
/// As consultas que o técnico de suporte usa. Ele "vê todas as empresas", e é
/// exatamente aí que uma pergunta ingênua ("existe alguém com este e-mail?")
/// responde errado: acha a mesma pessoa em OUTRA empresa.
/// </summary>
public class ConsultasDoSuporteTests : IDisposable
{
    private const string CpfDaMaria = "52998224725";
    private const string CpfDoJoao = "11144477735";

    private readonly BancoDeTeste _banco = new();

    public void Dispose() => _banco.Dispose();

    private static Customer Comprador(string nome, string email, string cpf)
        => Customer.RegisterWithPassword(nome, email, "11999999999", cpf, "hash-qualquer");

    private void Cadastrar(Guid empresaId, Customer comprador)
    {
        using var contexto = _banco.ComoEmpresa(empresaId);
        contexto.Customers.Add(comprador);
        contexto.SaveChanges();
    }

    // ═══════════════════════════════════════════ o número da empresa
    [Fact]
    public async Task EmpresaNovaRecebeOProximoNumeroLivre()
    {
        using var plataforma = _banco.ComoPlataforma();
        var repositorio = new TenantRepository(plataforma);

        // O banco de teste ja tem as empresas 1 e 2.
        var nova = new Tenant("Terceira", "terceira");
        await repositorio.AddAsync(nova);
        await repositorio.SaveChangesAsync();

        Assert.Equal(3, nova.Codigo);
        Assert.Equal("0003 · Terceira", nova.Rotulo);
    }

    [Fact]
    public async Task OPrimeiroNumeroDeUmBancoVazioE1()
    {
        // Um banco sem nenhuma empresa: o MAX de uma tabela vazia e nulo, e nao pode virar erro.
        using var banco = new BancoDeTeste(comEmpresas: false);
        using var plataforma = banco.ComoPlataforma();
        var repositorio = new TenantRepository(plataforma);

        var primeira = new Tenant("Primeira", "primeira");
        await repositorio.AddAsync(primeira);
        await repositorio.SaveChangesAsync();

        Assert.Equal(1, primeira.Codigo);
    }

    [Fact]
    public async Task BuscaVariasEmpresasDeUmaVezSoParaRotular()
    {
        using var plataforma = _banco.ComoPlataforma();
        var repositorio = new TenantRepository(plataforma);

        var achadas = await repositorio.GetByIdsAsync([_banco.EmpresaA, _banco.EmpresaB, _banco.EmpresaA, Guid.NewGuid()]);

        Assert.Equal(new[] { "0001 · Empresa A", "0002 · Empresa B" }, achadas.Select(t => t.Rotulo).OrderBy(r => r).ToArray());
    }

    [Fact]
    public async Task BuscarZeroEmpresasNaoVaiAoBanco()
    {
        using var plataforma = _banco.ComoPlataforma();

        Assert.Empty(await new TenantRepository(plataforma).GetByIdsAsync([]));
    }

    // ═══════════════════════════════════════ o cadastro pelo técnico
    [Fact]
    public async Task AMesmaPessoaEmOutraEmpresaNaoContaComoJaCadastrada()
    {
        Cadastrar(_banco.EmpresaA, Comprador("Maria", "maria@exemplo.com", CpfDaMaria));

        // O tecnico enxerga todas as empresas. Se a pergunta fosse "existe alguem com este
        // e-mail?", acharia a Maria da empresa A e recusaria cadastrar a Maria da empresa B.
        using var tecnico = _banco.ComoPlataforma();
        var repositorio = new CustomerRepository(tecnico);

        Assert.False(await repositorio.ExisteNaEmpresaAsync(_banco.EmpresaB, "maria@exemplo.com", CpfDaMaria));
        Assert.True(await repositorio.ExisteNaEmpresaAsync(_banco.EmpresaA, "maria@exemplo.com", CpfDaMaria));
    }

    [Fact]
    public async Task OCpfSozinhoJaBastaParaConsiderarCadastradoNaMesmaEmpresa()
    {
        Cadastrar(_banco.EmpresaA, Comprador("Maria", "maria@exemplo.com", CpfDaMaria));

        using var tecnico = _banco.ComoPlataforma();

        // E-mail diferente, mesmo CPF: e a mesma pessoa, e o indice unico do banco barraria.
        Assert.True(await new CustomerRepository(tecnico)
            .ExisteNaEmpresaAsync(_banco.EmpresaA, "outro@exemplo.com", CpfDaMaria));
    }

    [Fact]
    public async Task OEmailNaoDiferenciaMaiusculaDeMinuscula()
    {
        Cadastrar(_banco.EmpresaA, Comprador("Maria", "maria@exemplo.com", CpfDaMaria));

        using var tecnico = _banco.ComoPlataforma();

        Assert.True(await new CustomerRepository(tecnico)
            .ExisteNaEmpresaAsync(_banco.EmpresaA, "  MARIA@Exemplo.COM ", CpfDoJoao));
    }

    // ═════════════════════════════════════════ a lista do técnico
    [Fact]
    public async Task ListaCompradoresDeTodasAsEmpresasQuandoNaoFiltra()
    {
        Cadastrar(_banco.EmpresaA, Comprador("Ana", "ana@a.com", CpfDaMaria));
        Cadastrar(_banco.EmpresaB, Comprador("Bruno", "bruno@b.com", CpfDoJoao));

        using var tecnico = _banco.ComoPlataforma();

        var todos = await new CustomerRepository(tecnico).ListarAsync(null, null, 50);

        Assert.Equal(new[] { "Ana", "Bruno" }, todos.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task ListaSoOsDeUmaEmpresaQuandoFiltra()
    {
        Cadastrar(_banco.EmpresaA, Comprador("Ana", "ana@a.com", CpfDaMaria));
        Cadastrar(_banco.EmpresaB, Comprador("Bruno", "bruno@b.com", CpfDoJoao));

        using var tecnico = _banco.ComoPlataforma();

        var daB = await new CustomerRepository(tecnico).ListarAsync(_banco.EmpresaB, null, 50);

        Assert.Equal(new[] { "Bruno" }, daB.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task BuscaPorNomeOuEmail()
    {
        Cadastrar(_banco.EmpresaA, Comprador("Ana Souza", "ana@a.com", CpfDaMaria));
        Cadastrar(_banco.EmpresaA, Comprador("Bruno Lima", "souza.bruno@b.com", CpfDoJoao));

        using var tecnico = _banco.ComoPlataforma();
        var repositorio = new CustomerRepository(tecnico);

        Assert.Equal(2, (await repositorio.ListarAsync(null, "souza", 50)).Count);   // no nome de uma, no e-mail da outra
        Assert.Single(await repositorio.ListarAsync(null, "Lima", 50));
        Assert.Empty(await repositorio.ListarAsync(null, "ninguem", 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100000)]
    public async Task OLimiteDaListaFicaEntreUmEDuzentos(int limite)
    {
        for (var i = 0; i < 3; i++)
            Cadastrar(_banco.EmpresaA, Comprador($"Comprador {i}", $"c{i}@a.com", i switch { 0 => CpfDaMaria, 1 => CpfDoJoao, _ => "39053344705" }));

        using var tecnico = _banco.ComoPlataforma();

        var lista = await new CustomerRepository(tecnico).ListarAsync(null, null, limite);

        Assert.InRange(lista.Count, 1, 3);      // nunca zero (limite baixo) e nunca alem do que existe
    }

    [Fact]
    public async Task AdminDeUmaEmpresaNaoListaCompradoresDeOutraMesmoPedindo()
    {
        Cadastrar(_banco.EmpresaB, Comprador("Bruno", "bruno@b.com", CpfDoJoao));

        // Um contexto de Admin da empresa A pedindo explicitamente a empresa B: o filtro
        // global vale por cima de qualquer parametro da consulta.
        using var adminDaA = _banco.ComoEmpresa(_banco.EmpresaA);

        Assert.Empty(await new CustomerRepository(adminDaA).ListarAsync(_banco.EmpresaB, null, 50));
    }
}
