using System.Text.RegularExpressions;
using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Uma empresa assinante do sistema — a fronteira de isolamento: o que uma
/// empresa cadastra, outra não enxerga.
///
/// Não confundir com <see cref="Company"/>: aquela é a marca dona de um
/// produto no catálogo (a "Empresas" do banco); esta é quem contratou o
/// sistema. Uma empresa assinante pode ter várias marcas no catálogo dela.
/// </summary>
public class Tenant : Entity
{
    /// <summary>A empresa que recebeu tudo o que já existia antes do multiempresa.</summary>
    public static readonly Guid IdDaEmpresaPadrao = new("00000000-0000-0000-0000-000000000001");

    // Endereços que o sistema já usa: uma empresa com esses nomes ocuparia o
    // lugar de uma rota ou de um app.
    private static readonly HashSet<string> SlugsReservados = new(StringComparer.Ordinal)
    {
        "admin", "api", "app", "criador", "suporte", "www", "hubs", "login", "static", "assets",
    };

    private static readonly Regex FormatoDoSlug =
        new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Nome { get; private set; } = "";

    /// <summary>
    /// Número sequencial da empresa (1, 2, 3…), dado ao cadastrar. É como o suporte a
    /// enxerga: "0042 · Rede Sabor". O técnico não precisa de CNPJ nem de contato para
    /// saber de qual empresa é um chamado, e o código não muda nunca.
    /// </summary>
    public int Codigo { get; private set; }

    /// <summary>Como a empresa aparece para o técnico: código + nome fantasia, e mais nada.</summary>
    public string Rotulo => $"{Codigo:D4} · {Nome}";

    /// <summary>
    /// Identificador público da empresa, o que vai no link e no QR code da
    /// loja ("redesabor"). Serve para mostrar nome e logo antes do login e
    /// NUNCA como prova de quem a pessoa é — quem autoriza é o token.
    /// </summary>
    public string Slug { get; private set; } = "";

    public StatusDoTenant Status { get; private set; }

    /// <summary>Quantas lojas a assinatura permite. É o que o preço acompanha.</summary>
    public int LimiteDeLojas { get; private set; }

    public bool EstaAtiva => Status == StatusDoTenant.Ativa;

    protected Tenant() { }

    public Tenant(string nome, string slug, int limiteDeLojas = 1)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("O nome da empresa não pode ser vazio.", nameof(nome));

        if (limiteDeLojas < 1)
            throw new ArgumentException("A empresa precisa poder ter ao menos uma loja.", nameof(limiteDeLojas));

        Nome = nome.Trim();
        Slug = NormalizarSlug(slug);
        LimiteDeLojas = limiteDeLojas;
        Status = StatusDoTenant.Ativa;
    }

    /// <summary>Minúsculo, sem espaço nas pontas, e só letras, números e hífen.</summary>
    public static string NormalizarSlug(string? slug)
    {
        var normalizado = (slug ?? "").Trim().ToLowerInvariant();

        if (normalizado.Length is < 3 or > 40 || !FormatoDoSlug.IsMatch(normalizado))
            throw new ArgumentException(
                "O identificador precisa ter de 3 a 40 caracteres: letras minúsculas, números e hífen.",
                nameof(slug));

        if (SlugsReservados.Contains(normalizado))
            throw new ArgumentException("Esse identificador é reservado pelo sistema.", nameof(slug));

        return normalizado;
    }

    /// <summary>Só a infraestrutura chama, ao cadastrar. O código vale uma vez: renumerar empresa confundiria o suporte.</summary>
    public void AtribuirCodigo(int codigo)
    {
        if (codigo < 1)
            throw new ArgumentException("O código da empresa começa em 1.", nameof(codigo));

        if (Codigo != 0)
            throw new InvalidOperationException("Esta empresa já tem código.");

        Codigo = codigo;
    }

    public void Renomear(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("O nome da empresa não pode ser vazio.", nameof(nome));

        Nome = nome.Trim();
    }

    public void DefinirLimiteDeLojas(int limite)
    {
        if (limite < 1)
            throw new ArgumentException("A empresa precisa poder ter ao menos uma loja.", nameof(limite));

        LimiteDeLojas = limite;
    }

    /// <summary>
    /// A regra do preço: mais lojas que o plano permite, não. Quem cria a loja
    /// passa quantas ela já tem — a contagem mora no banco, a regra mora aqui.
    /// </summary>
    public bool PodeTerMaisUmaLoja(int lojasAtuais) => lojasAtuais < LimiteDeLojas;

    public void Suspender() => Status = StatusDoTenant.Suspensa;

    public void Reativar() => Status = StatusDoTenant.Ativa;
}
