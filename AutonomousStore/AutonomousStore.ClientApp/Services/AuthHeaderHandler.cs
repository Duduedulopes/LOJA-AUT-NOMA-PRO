using System.Net.Http.Headers;

namespace AutonomousStore.ClientApp.Services;

/// <summary>Anexa "Authorization: Bearer {token}" em toda chamada, quando o cliente está logado.</summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly AppState _appState;

    public AuthHeaderHandler(AppState appState)
    {
        _appState = appState;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_appState.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appState.Token);

        return base.SendAsync(request, cancellationToken);
    }
}
