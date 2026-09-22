using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Infrastructure.Persistence;
using AutonomousStore.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Tests;

/// <summary>
/// Um banco SQLite em memória, compartilhado por vários DbContext — cada um
/// fazendo o papel de uma requisição diferente, com a sua própria empresa.
/// É assim que o isolamento é testado de verdade: quem grava e quem lê são
/// contextos distintos, como seriam duas pessoas de duas empresas.
/// </summary>
public sealed class BancoDeTeste : IDisposable
{
    private readonly SqliteConnection _conexao;

    public Guid EmpresaA { get; } = Guid.NewGuid();
    public Guid EmpresaB { get; } = Guid.NewGuid();

    /// <param name="comEmpresas">Falso para um banco recém-criado, sem nenhuma empresa (o primeiro cadastro).</param>
    public BancoDeTeste(bool comEmpresas = true)
    {
        // O banco em memória morre quando a conexão fecha: por isso ela fica
        // aberta e é compartilhada por todos os contextos do teste.
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        using var setup = Contexto(new TenantContext());
        setup.Database.EnsureCreated();

        if (!comEmpresas) return;

        // As duas empresas existem desde o início: os registros de negócio
        // têm chave estrangeira para elas.
        using var plataforma = ComoPlataforma();
        plataforma.Tenants.Add(NovaEmpresa(EmpresaA, "Empresa A", "empresa-a", codigo: 1));
        plataforma.Tenants.Add(NovaEmpresa(EmpresaB, "Empresa B", "empresa-b", codigo: 2));
        plataforma.SaveChanges();
    }

    /// <summary>Uma "requisição" de quem ainda não foi identificado: nenhuma empresa.</summary>
    public AutonomousDbContext ComoAnonimo() => Contexto(new TenantContext());

    /// <summary>Uma "requisição" de alguém da empresa informada (Admin ou Comprador).</summary>
    public AutonomousDbContext ComoEmpresa(Guid empresaId)
    {
        var tenant = new TenantContext();
        tenant.DefinirEmpresa(empresaId);
        return Contexto(tenant);
    }

    /// <summary>Uma "requisição" do Criador ou do Técnico: enxerga todas as empresas.</summary>
    public AutonomousDbContext ComoPlataforma()
    {
        var tenant = new TenantContext();
        tenant.DefinirTodasAsEmpresas();
        return Contexto(tenant);
    }

    private AutonomousDbContext Contexto(TenantContext tenant)
    {
        var opcoes = new DbContextOptionsBuilder<AutonomousDbContext>()
            .UseSqlite(_conexao)
            .Options;

        return new AutonomousDbContext(opcoes, tenant);
    }

    private static Tenant NovaEmpresa(Guid id, string nome, string slug, int codigo)
    {
        var empresa = new Tenant(nome, slug, limiteDeLojas: 2);
        empresa.AtribuirCodigo(codigo);

        // O Id nasce aleatório na entidade; o teste precisa de um conhecido.
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(empresa, id);

        return empresa;
    }

    public void Dispose() => _conexao.Dispose();
}
