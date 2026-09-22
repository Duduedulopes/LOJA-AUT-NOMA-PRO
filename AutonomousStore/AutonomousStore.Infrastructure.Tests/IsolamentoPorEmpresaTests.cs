using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Tests;

/// <summary>
/// O que o multiempresa promete ao cliente que paga: os dados dele são dele.
/// Cada teste abaixo é uma forma de essa promessa quebrar — e o nome diz qual.
/// </summary>
public class IsolamentoPorEmpresaTests : IDisposable
{
    // CPFs válidos (dígitos verificadores corretos) — o domínio recusa os inválidos.
    private const string CpfDaMaria = "52998224725";
    private const string CpfDoJoao = "11144477735";

    private readonly BancoDeTeste _banco = new();

    public void Dispose() => _banco.Dispose();

    private static Product NovoProduto(string barcode = "7891000000011", string? rfid = null)
    {
        var produto = new Product("Água Mineral 500ml", barcode, 3.50m);
        if (rfid is not null) produto.AssignRfidTag(rfid);
        return produto;
    }

    private static Customer NovoComprador(string email = "maria@exemplo.com", string cpf = CpfDaMaria)
        => Customer.RegisterWithPassword("Maria", email, "11999999999", cpf, "hash-qualquer");

    private static Ocorrencia NovaOcorrencia(string? chave = null)
        => new(DateTime.UtcNow, "WebApi", "Teste", "Operacao",
               TipoDeOcorrencia.ErroExecucao, Severidade.Media, "algo deu errado",
               AcaoRecomendada.ApenasRegistrar, chave: chave);

    // ═══════════════════════════════════════════════════════════ leitura
    [Fact]
    public void EmpresaSoEnxergaOsProdutosDela()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Products.Add(NovoProduto("111"));
            a.SaveChanges();
        }
        using (var b = _banco.ComoEmpresa(_banco.EmpresaB))
        {
            b.Products.Add(NovoProduto("222"));
            b.SaveChanges();
        }

        using var lendoComoA = _banco.ComoEmpresa(_banco.EmpresaA);
        using var lendoComoB = _banco.ComoEmpresa(_banco.EmpresaB);

        Assert.Equal(new[] { "111" }, lendoComoA.Products.Select(p => p.Barcode).ToArray());
        Assert.Equal(new[] { "222" }, lendoComoB.Products.Select(p => p.Barcode).ToArray());
    }

    [Fact]
    public void ConhecerOIdDeUmRegistroDeOutraEmpresaNaoAbreAPorta()
    {
        Guid idDoProdutoDeB;
        using (var b = _banco.ComoEmpresa(_banco.EmpresaB))
        {
            var produto = NovoProduto("222");
            b.Products.Add(produto);
            b.SaveChanges();
            idDoProdutoDeB = produto.Id;
        }

        using var a = _banco.ComoEmpresa(_banco.EmpresaA);

        // O cenario do ataque: A adivinhou ou copiou o id de um produto de B.
        Assert.Null(a.Products.FirstOrDefault(p => p.Id == idDoProdutoDeB));
        Assert.Empty(a.Products.Where(p => p.Id == idDoProdutoDeB).ToList());
    }

    [Fact]
    public void FindPelaChaveTambemNaoVazaEntreEmpresas()
    {
        Guid idDoProdutoDeB;
        using (var b = _banco.ComoEmpresa(_banco.EmpresaB))
        {
            var produto = NovoProduto("222");
            b.Products.Add(produto);
            b.SaveChanges();
            idDoProdutoDeB = produto.Id;
        }

        using var a = _banco.ComoEmpresa(_banco.EmpresaA);

        // `Find` e o atalho que o EF trata a parte (olha o cache antes do banco):
        // vale conferir que ele tambem passa pelo filtro.
        Assert.Null(a.Products.Find(idDoProdutoDeB));
    }

    [Fact]
    public async Task RepositorioTambemRespeitaOIsolamento()
    {
        using (var b = _banco.ComoEmpresa(_banco.EmpresaB))
        {
            b.Customers.Add(NovoComprador("maria@exemplo.com"));
            b.SaveChanges();
        }

        using var a = _banco.ComoEmpresa(_banco.EmpresaA);
        var repositorio = new CustomerRepository(a);

        // A procura pelo mesmo e-mail que existe em B: para A, ele nao existe.
        Assert.Null(await repositorio.GetByEmailAsync("maria@exemplo.com"));
        Assert.Null(await repositorio.GetByCpfAsync(CpfDaMaria));
    }

    [Fact]
    public void QuemNaoFoiIdentificadoNaoEnxergaNada()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Products.Add(NovoProduto());
            a.Customers.Add(NovoComprador());
            a.SaveChanges();
        }

        // Falha FECHADA: esquecer de definir a empresa da requisicao da tela
        // vazia, e nunca dado de outra empresa.
        using var anonimo = _banco.ComoAnonimo();

        Assert.Empty(anonimo.Products.ToList());
        Assert.Empty(anonimo.Customers.ToList());
    }

    [Fact]
    public void OcorrenciaDaPlataformaSemEmpresaNaoApareceParaQuemNaoFoiIdentificado()
    {
        using (var plataforma = _banco.ComoPlataforma())
        {
            plataforma.Ocorrencias.Add(NovaOcorrencia());   // sem empresa
            plataforma.SaveChanges();
        }

        // O caso que enganaria um filtro ingenuo: linha sem empresa (nulo) e
        // requisicao sem empresa (nulo) — "nulo igual a nulo" nao pode valer.
        using var anonimo = _banco.ComoAnonimo();
        using var empresa = _banco.ComoEmpresa(_banco.EmpresaA);

        Assert.Empty(anonimo.Ocorrencias.ToList());
        Assert.Empty(empresa.Ocorrencias.ToList());
    }

    [Fact]
    public void PlataformaEnxergaTodasAsEmpresas()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Products.Add(NovoProduto("111"));
            a.SaveChanges();
        }
        using (var b = _banco.ComoEmpresa(_banco.EmpresaB))
        {
            b.Products.Add(NovoProduto("222"));
            b.SaveChanges();
        }
        using (var plataforma = _banco.ComoPlataforma())
        {
            plataforma.Ocorrencias.Add(NovaOcorrencia());
            plataforma.SaveChanges();
        }

        using var criador = _banco.ComoPlataforma();

        Assert.Equal(new[] { "111", "222" },
            criador.Products.Select(p => p.Barcode).OrderBy(b => b).ToArray());
        Assert.Single(criador.Ocorrencias.ToList());
    }

    [Fact]
    public void OcorrenciaDeUmaEmpresaNaoApareceParaAOutra()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Ocorrencias.Add(NovaOcorrencia());
            a.SaveChanges();
        }

        using var b = _banco.ComoEmpresa(_banco.EmpresaB);
        using var plataforma = _banco.ComoPlataforma();

        Assert.Empty(b.Ocorrencias.ToList());
        Assert.Single(plataforma.Ocorrencias.ToList());     // o suporte enxerga
    }

    [Fact]
    public void AdminSeEncontraPeloEmailSemEmpresaNaRequisicao()
    {
        using (var plataforma = _banco.ComoPlataforma())
        {
            var admin = new AdminUser("Ana", "ana@empresa-a.com", "hash");
            admin.PertenceAoTenant(_banco.EmpresaA);
            plataforma.AdminUsers.Add(admin);
            plataforma.SaveChanges();
        }

        // O login do Admin acontece ANTES de existir empresa na requisicao: e
        // o e-mail que revela a empresa. Por isso essa tabela nao tem filtro.
        using var anonimo = _banco.ComoAnonimo();
        var achado = anonimo.AdminUsers.Single(a => a.Email == "ana@empresa-a.com");

        Assert.Equal(_banco.EmpresaA, achado.TenantId);
    }

    // ═══════════════════════════════════════════════════════════ carimbo
    [Fact]
    public void RegistroNovoNasceComAEmpresaDaRequisicao()
    {
        Guid id;
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            var produto = NovoProduto();
            a.Products.Add(produto);
            a.SaveChanges();
            id = produto.Id;
        }

        using var criador = _banco.ComoPlataforma();

        Assert.Equal(_banco.EmpresaA, criador.Products.Single(p => p.Id == id).TenantId);
    }

    [Fact]
    public void GravarSemEmpresaDefinidaFalhaAltoEmVezDeCriarRegistroInvisivel()
    {
        using var anonimo = _banco.ComoAnonimo();
        anonimo.Products.Add(NovoProduto());

        var erro = Assert.Throws<InvalidOperationException>(() => anonimo.SaveChanges());
        Assert.Contains("empresa", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlataformaSemDizerEmNomeDeQuemGravaTambemFalha()
    {
        using var criador = _banco.ComoPlataforma();
        criador.Products.Add(NovoProduto());

        Assert.Throws<InvalidOperationException>(() => criador.SaveChanges());
    }

    [Fact]
    public void PlataformaPodeGravarEmNomeDeUmaEmpresa()
    {
        Guid id;
        using (var criador = _banco.ComoPlataforma())
        {
            var comprador = NovoComprador();
            comprador.PertenceAoTenant(_banco.EmpresaA);
            criador.Customers.Add(comprador);
            criador.SaveChanges();
            id = comprador.Id;
        }

        using var a = _banco.ComoEmpresa(_banco.EmpresaA);
        using var b = _banco.ComoEmpresa(_banco.EmpresaB);

        Assert.NotNull(a.Customers.FirstOrDefault(c => c.Id == id));
        Assert.Null(b.Customers.FirstOrDefault(c => c.Id == id));
    }

    [Fact]
    public void EmpresaComumNaoPodeGravarNoNomeDeOutra()
    {
        using var a = _banco.ComoEmpresa(_banco.EmpresaA);
        var produto = NovoProduto();
        produto.PertenceAoTenant(_banco.EmpresaB);      // A tentando escrever em B
        a.Products.Add(produto);

        Assert.Throws<InvalidOperationException>(() => a.SaveChanges());
    }

    [Fact]
    public void OcorrenciaDaPlataformaPodeNascerSemEmpresa()
    {
        using var criador = _banco.ComoPlataforma();
        var ocorrencia = NovaOcorrencia();
        criador.Ocorrencias.Add(ocorrencia);

        criador.SaveChanges();

        Assert.Null(ocorrencia.TenantId);
    }

    [Fact]
    public void EmpresaDeUmRegistroNaoPodeSerTrocadaDepois()
    {
        Guid id;
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            var produto = NovoProduto();
            a.Products.Add(produto);
            a.SaveChanges();
            id = produto.Id;
        }

        using var criador = _banco.ComoPlataforma();
        var carregado = criador.Products.Single(p => p.Id == id);
        criador.Entry(carregado).Property(nameof(TenantEntity.TenantId)).CurrentValue = _banco.EmpresaB;

        Assert.Throws<InvalidOperationException>(() => criador.SaveChanges());
    }

    [Fact]
    public void EmpresaInexistenteERecusadaPeloBanco()
    {
        using var criador = _banco.ComoPlataforma();
        var produto = NovoProduto();
        produto.PertenceAoTenant(Guid.NewGuid());       // empresa que nao existe
        criador.Products.Add(produto);

        Assert.Throws<DbUpdateException>(() => criador.SaveChanges());
    }

    // ═══════════════════════════════════════════════ unicidade por empresa
    [Fact]
    public void DuasEmpresasPodemTerOMesmoCodigoDeBarras()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Products.Add(NovoProduto("789"));
            a.SaveChanges();
        }

        using var b = _banco.ComoEmpresa(_banco.EmpresaB);
        b.Products.Add(NovoProduto("789"));

        b.SaveChanges();    // nao pode lancar: o codigo so precisa ser unico DENTRO da empresa
    }

    [Fact]
    public void MesmaEmpresaNaoPodeRepetirOCodigoDeBarras()
    {
        using var a = _banco.ComoEmpresa(_banco.EmpresaA);
        a.Products.Add(NovoProduto("789"));
        a.SaveChanges();

        a.Products.Add(NovoProduto("789"));

        Assert.Throws<DbUpdateException>(() => a.SaveChanges());
    }

    [Fact]
    public void DuasEmpresasPodemTerAMesmaTagRfid()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Products.Add(NovoProduto("1", rfid: "TAG-1"));
            a.SaveChanges();
        }

        using var b = _banco.ComoEmpresa(_banco.EmpresaB);
        b.Products.Add(NovoProduto("2", rfid: "TAG-1"));

        b.SaveChanges();
    }

    [Fact]
    public void MesmaPessoaPodeSeCadastrarEmDuasEmpresas()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Customers.Add(NovoComprador());
            a.SaveChanges();
        }

        using var b = _banco.ComoEmpresa(_banco.EmpresaB);
        b.Customers.Add(NovoComprador());       // mesmo e-mail e mesmo CPF

        b.SaveChanges();
    }

    [Fact]
    public void MesmaPessoaNaoPodeSeCadastrarDuasVezesNaMesmaEmpresa()
    {
        using var a = _banco.ComoEmpresa(_banco.EmpresaA);
        a.Customers.Add(NovoComprador("maria@exemplo.com", CpfDaMaria));
        a.SaveChanges();

        a.Customers.Add(NovoComprador("maria@exemplo.com", CpfDoJoao));     // mesmo e-mail

        Assert.Throws<DbUpdateException>(() => a.SaveChanges());
    }

    [Fact]
    public void OMesmoErroEmDuasEmpresasSaoDoisRegistros()
    {
        using (var a = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            a.Ocorrencias.Add(NovaOcorrencia(chave: "erro-x"));
            a.SaveChanges();
        }

        using var b = _banco.ComoEmpresa(_banco.EmpresaB);
        b.Ocorrencias.Add(NovaOcorrencia(chave: "erro-x"));

        b.SaveChanges();
    }

    // ═════════════════════════════════════════════ a rede de seguranca
    /// <summary>
    /// O teste que protege o FUTURO: amanhã alguém cria uma entidade nova de
    /// empresa e esquece de ligar o filtro. Sem este teste, o vazamento só seria
    /// descoberto por um cliente. Com ele, o build quebra.
    /// </summary>
    [Fact]
    public void TodaEntidadeDeEmpresaTemFiltroExcetoAsDeclaradasSemFiltro()
    {
        // Sem filtro de proposito, e o motivo de cada uma:
        var semFiltro = new HashSet<Type>
        {
            typeof(AdminUser),      // e procurado pelo e-mail no login, antes de haver empresa
        };

        using var contexto = _banco.ComoPlataforma();

        var semProtecao = contexto.Model.GetEntityTypes()
            .Where(t => typeof(TenantEntity).IsAssignableFrom(t.ClrType))
            .Where(t => !semFiltro.Contains(t.ClrType))
            .Where(t => t.GetQueryFilter() is null)
            .Select(t => t.ClrType.Name)
            .ToList();

        Assert.True(semProtecao.Count == 0,
            "Entidades de empresa SEM filtro global (vazariam dados entre empresas): "
            + string.Join(", ", semProtecao));
    }
}
