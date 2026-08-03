using System.Net.Http.Json;
using AutonomousStore.AdminApp.Models;

namespace AutonomousStore.AdminApp.Services;

public interface IVisionApiService
{
    Task<DetectShelfChangeResponse?> DetectShelfChangeAsync(List<Guid> productIds, string beforeImageBase64, string afterImageBase64);
}

public class VisionApiService : IVisionApiService
{
    private readonly HttpClient _http;

    public VisionApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DetectShelfChangeResponse?> DetectShelfChangeAsync(List<Guid> productIds, string beforeImageBase64, string afterImageBase64)
    {
        var response = await _http.PostAsJsonAsync(
            "api/vision/detect-shelf-change",
            new DetectShelfChangeRequest(productIds, beforeImageBase64, afterImageBase64));

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<DetectShelfChangeResponse>();
    }
}
