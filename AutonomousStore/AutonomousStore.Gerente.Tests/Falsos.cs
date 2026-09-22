using System.Text.Json;
using AutonomousStore.Gerente.Models;
using AutonomousStore.Gerente.Services;
using AutonomousStore.Gerente.Services.Aprendizado;

namespace AutonomousStore.Gerente.Tests;

/// <summary>
/// As APIs da loja, falsas, e com uma memória: cada chamada que o gerente faz fica anotada em
/// <see cref="Chamadas"/>. É o que permite afirmar não só "ele recusou", mas "ele recusou SEM
/// nem ir buscar o dado" — que é o que importa quando o dado é o faturamento de uma empresa.
/// </summary>
public sealed class ApiFalsa : IProductApiService, ISessionApiService, IGerenteEspacialService
{
    public List<string> Chamadas { get; } = new();

    private T Anota<T>(string nome, T retorno)
    {
        Chamadas.Add(nome);
        return retorno;
    }

    // ── produtos ──────────────────────────────────────────────────────
    public Task<List<ProductDto>> GetAllAsync() => Task.FromResult(Anota(nameof(GetAllAsync), new List<ProductDto>()));
    public Task<List<ProductDto>> GetLowStockAsync() => Task.FromResult(Anota(nameof(GetLowStockAsync), new List<ProductDto>()));

    public Task<(bool Success, ProductDto? Product, string? Error)> CreateAsync(CreateProductRequest request)
        => Task.FromResult(Anota<(bool, ProductDto?, string?)>(nameof(CreateAsync), (true, null, null)));

    public Task<(bool Success, string? Error)> UpdatePriceAsync(Guid id, decimal price)
        => Task.FromResult(Anota<(bool, string?)>(nameof(UpdatePriceAsync), (true, null)));

    public Task<(bool Success, string? Error)> UpdateDetailsAsync(Guid id, string name, string? description, string? imageUrl)
        => Task.FromResult(Anota<(bool, string?)>(nameof(UpdateDetailsAsync), (true, null)));

    public Task<(bool Success, string? Error)> RestockAsync(Guid id, int quantity)
        => Task.FromResult(Anota<(bool, string?)>(nameof(RestockAsync), (true, null)));

    public Task<(bool Success, string? Error)> SetStockThresholdAsync(Guid id, int? threshold)
        => Task.FromResult(Anota<(bool, string?)>(nameof(SetStockThresholdAsync), (true, null)));

    public Task<(bool Success, string? Error)> AssignRfidTagAsync(Guid id, string? rfidTag)
        => Task.FromResult(Anota<(bool, string?)>(nameof(AssignRfidTagAsync), (true, null)));

    // ── sessões ───────────────────────────────────────────────────────
    public Task<SessionDto?> GetCurrentOpenAsync() => Task.FromResult(Anota<SessionDto?>(nameof(GetCurrentOpenAsync), null));
    public Task<SessionDto?> GetActiveByCustomerAsync(Guid clienteId) => Task.FromResult(Anota<SessionDto?>(nameof(GetActiveByCustomerAsync), null));
    public Task<List<SessionDto>> GetPendingEntryAsync() => Task.FromResult(Anota(nameof(GetPendingEntryAsync), new List<SessionDto>()));

    public Task<(bool Success, string? Error)> ConfirmEntryAsync(string qrCodeToken)
        => Task.FromResult(Anota<(bool, string?)>(nameof(ConfirmEntryAsync), (true, null)));

    public Task<List<SessionDto>> GetHistoryAsync() => Task.FromResult(Anota(nameof(GetHistoryAsync), new List<SessionDto>()));

    // ── sistema espacial ──────────────────────────────────────────────
    public Task<EspacialResumoDto?> ObterAsync() => Task.FromResult(Anota<EspacialResumoDto?>(nameof(ObterAsync), null));
}

/// <summary>
/// O classificador que devolve a intenção que o teste mandar, com confiança total. Serve para
/// exercitar o caminho de quem DIGITA a frase sem depender da rede acertar — a permissão vale
/// para o que a rede disse, seja o que for.
/// </summary>
public sealed class ClassificadorFalso : IClassificadorDeIntencao
{
    public string IntencaoQueVaiDevolver { get; set; } = "saudacao";

    public bool Pronto => true;
    public ModeloIntencao? Modelo => null;

    public Task<bool> CarregarAsync() => Task.FromResult(true);
    public Intencao Classificar(string pergunta) => new(IntencaoQueVaiDevolver, 0.99, Confiavel: true);

    public Pensamento Pensar(string pergunta) => throw new NotSupportedException();
    public Task<string> ConferirContraOPythonAsync() => throw new NotSupportedException();
    public IReadOnlyList<int> PecasDe(string texto) => Array.Empty<int>();
    public void UsarRede(RedeTreinavel rede) { }
}

/// <summary>Um HttpClient que falha: o gerente grava as perguntas no monitor, e o teste não tem monitor.</summary>
public sealed class FabricaDeHttpFalsa : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new Falha());

    private sealed class Falha : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("sem monitor neste teste");
    }
}

/// <summary>As intenções que o Rede-Neural treinou — a fonte da verdade, lida do mesmo arquivo que os apps baixam.</summary>
public static class ModeloTreinado
{
    public static IReadOnlyList<string> Intencoes { get; } = Ler();

    private static IReadOnlyList<string> Ler()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "modelos", "intencao.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(caminho));

        return doc.RootElement.GetProperty("intencoes").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();
    }
}
