using System.Net.Http.Headers;

namespace AutonomousStore.AdminApp;

/// <summary>Anexa o token de admin (se logado) em toda chamada HTTP feita pelo app.</summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly AppState _appState;

    public AuthHeaderHandler(AppState appState)
    {
        _appState = appState;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_appState.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appState.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
