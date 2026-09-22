using AutonomousStore.Domain.Entities;
using AutonomousStore.Infrastructure.Repositories;

namespace AutonomousStore.Infrastructure.Tests;

/// <summary>
/// As lojas por empresa: quantas estão ativas (o número que o limite da assinatura confere), se um nome já existe, e o que cada
/// contexto enxerga. O limite e o nome são POR EMPRESA — a loja "Centro" de uma empresa não barra a "Centro" de outra.
/// </summary>
public class StoreRepositoryTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();

    public void Dispose() => _banco.Dispose();

    private Guid Criar(Guid empresaId, string nome, bool ativa = true)
    {
        using var contexto = _banco.ComoEmpresa(empresaId);
        var loja = new Store(nome);
        if (!ativa) loja.Deactivate();
        contexto.Stores.Add(loja);
        contexto.SaveChanges();
        return loja.Id;
    }

    // ═══════════════════════════════════════════ o número que o limite confere
    [Fact]
    public async Task ContaSoAsLojasAtivas()
    {
        Criar(_banco.EmpresaA, "Centro");
        Criar(_banco.EmpresaA, "Norte");
        Criar(_banco.EmpresaA, "Sul", ativa: false);         // desativada: a vaga esta livre

        using var contexto = _banco.ComoEmpresa(_banco.EmpresaA);

        Assert.Equal(2, await new StoreRepository(contexto).ContarAtivasAsync(_banco.EmpresaA));
    }

    [Fact]
    public async Task OContadorDeUmaEmpresaNaoContaAsDeOutra()
    {
        Criar(_banco.EmpresaA, "Centro");
        Criar(_banco.EmpresaB, "Centro");
        Criar(_banco.EmpresaB, "Norte");

        using var criador = _banco.ComoPlataforma();
        var repositorio = new StoreRepository(criador);

        // O Criador ve todas as empresas, e por isso a pergunta NOMEIA a empresa.
        Assert.Equal(1, await repositorio.ContarAtivasAsync(_banco.EmpresaA));
        Assert.Equal(2, await repositorio.ContarAtivasAsync(_banco.EmpresaB));
    }

    [Fact]
    public async Task EmpresaSemLojasTemZero()
    {
        using var contexto = _banco.ComoPlataforma();

        Assert.Equal(0, await new StoreRepository(contexto).ContarAtivasAsync(_banco.EmpresaA));
    }

    // ═════════════════════════════════════════════════════ o nome
    [Fact]
    public async Task ONomeJaExisteNaMesmaEmpresaSemDiferenciarMaiuscula()
    {
        Criar(_banco.EmpresaA, "Loja Centro");

        using var criador = _banco.ComoPlataforma();
        var repositorio = new StoreRepository(criador);

        Assert.True(await repositorio.ExisteComNomeAsync(_banco.EmpresaA, "LOJA CENTRO"));
        Assert.True(await repositorio.ExisteComNomeAsync(_banco.EmpresaA, "  loja centro  "));
        Assert.False(await repositorio.ExisteComNomeAsync(_banco.EmpresaA, "Loja Norte"));
    }

    [Fact]
    public async Task OMesmoNomeEmOutraEmpresaNaoConta()
    {
        Criar(_banco.EmpresaA, "Centro");

        using var criador = _banco.ComoPlataforma();

        // Para quem ve todas as empresas, "existe uma loja Centro?" acharia a da A e recusaria a da B, que e perfeitamente valida.
        Assert.False(await new StoreRepository(criador).ExisteComNomeAsync(_banco.EmpresaB, "Centro"));
    }

    [Fact]
    public async Task AoRenomearALojaNaoConflitaComElaMesma()
    {
        var centro = Criar(_banco.EmpresaA, "Centro");
        var norte = Criar(_banco.EmpresaA, "Norte");

        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);
        var repositorio = new StoreRepository(admin);

        Assert.False(await repositorio.ExisteComNomeAsync(_banco.EmpresaA, "Centro", exceto: centro));   // e ela mesma
        Assert.True(await repositorio.ExisteComNomeAsync(_banco.EmpresaA, "Centro", exceto: norte));     // e a OUTRA que tem esse nome
    }

    // ═════════════════════════════════════ o que cada contexto enxerga
    [Fact]
    public async Task OAdminListaSoAsLojasDaEmpresaDele()
    {
        Criar(_banco.EmpresaA, "Norte");
        Criar(_banco.EmpresaA, "Centro");
        Criar(_banco.EmpresaB, "Da B");

        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);

        var lojas = await new StoreRepository(admin).ListarAsync();

        Assert.Equal(new[] { "Centro", "Norte" }, lojas.Select(l => l.Nome).ToArray());     // so as dela, em ordem de nome
    }

    [Fact]
    public async Task OSuporteListaAsLojasDeTodasAsEmpresas()
    {
        Criar(_banco.EmpresaA, "Da A");
        Criar(_banco.EmpresaB, "Da B");

        using var suporte = _banco.ComoPlataforma();

        Assert.Equal(2, (await new StoreRepository(suporte).ListarAsync()).Count);
    }

    [Fact]
    public async Task OCriadorListaAsLojasDeUmaEmpresaSo()
    {
        Criar(_banco.EmpresaA, "Da A");
        Criar(_banco.EmpresaB, "Da B");

        using var criador = _banco.ComoPlataforma();

        var daB = await new StoreRepository(criador).ListarDaEmpresaAsync(_banco.EmpresaB);

        Assert.Equal(new[] { "Da B" }, daB.Select(l => l.Nome).ToArray());
    }

    [Fact]
    public async Task ALojaDeOutraEmpresaNaoSeAbrePorId()
    {
        var idDaLojaDaB = Criar(_banco.EmpresaB, "Da B");

        using var adminDaA = _banco.ComoEmpresa(_banco.EmpresaA);

        // O cenario do ataque: o Admin da A copiou o id da loja da B. Para ele, ela nao existe.
        Assert.Null(await new StoreRepository(adminDaA).GetByIdAsync(idDaLojaDaB));
    }

    [Fact]
    public async Task ALojaCriadaPeloAdminNasceNaEmpresaDele()
    {
        var id = Criar(_banco.EmpresaA, "Centro");

        using var criador = _banco.ComoPlataforma();
        var loja = await new StoreRepository(criador).GetByIdAsync(id);

        Assert.Equal(_banco.EmpresaA, loja!.TenantId);
    }
}
