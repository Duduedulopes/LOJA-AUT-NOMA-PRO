using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// O carimbo de empresa em um registro só pode ser posto uma vez. Se desse para
/// trocar, mover um produto ou um comprador de uma empresa para outra seria uma
/// linha de código — que é exatamente o vazamento que o isolamento impede.
/// </summary>
public class TenantEntityTests
{
    private static readonly Guid EmpresaA = Guid.NewGuid();
    private static readonly Guid EmpresaB = Guid.NewGuid();

    private static Product NovoProduto() => new("Água Mineral 500ml", "7891000000011", 3.50m);

    [Fact]
    public void RegistroNovoNaoTemEmpresaAteSerCarimbado()
    {
        Assert.Null(NovoProduto().TenantId);
    }

    [Fact]
    public void CarimbarDefineAEmpresaDona()
    {
        var produto = NovoProduto();

        produto.PertenceAoTenant(EmpresaA);

        Assert.Equal(EmpresaA, produto.TenantId);
    }

    [Fact]
    public void CarimbarDeNovoComAMesmaEmpresaEInofensivo()
    {
        var produto = NovoProduto();
        produto.PertenceAoTenant(EmpresaA);

        produto.PertenceAoTenant(EmpresaA);

        Assert.Equal(EmpresaA, produto.TenantId);
    }

    [Fact]
    public void RegistroDeUmaEmpresaNaoPodeSerPassadoParaOutra()
    {
        var produto = NovoProduto();
        produto.PertenceAoTenant(EmpresaA);

        Assert.Throws<InvalidOperationException>(() => produto.PertenceAoTenant(EmpresaB));
        Assert.Equal(EmpresaA, produto.TenantId);
    }

    [Fact]
    public void EmpresaVaziaERecusada()
    {
        Assert.Throws<ArgumentException>(() => NovoProduto().PertenceAoTenant(Guid.Empty));
    }
}
