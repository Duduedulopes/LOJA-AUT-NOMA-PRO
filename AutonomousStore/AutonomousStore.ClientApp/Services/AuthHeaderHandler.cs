using System.Net.Http.Headers;
using AutonomousStore.Domain.Common;

namespace AutonomousStore.ClientApp.Services;

/// <summary>Anexa "Authorization: Bearer {token}" e "X-Empresa: {empresa}" em toda chamada. O primeiro, quando o cliente está logado.</summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly AppState _appState;
    private readonly EmpresaDaLoja _empresa;

    public AuthHeaderHandler(AppState appState, EmpresaDaLoja empresa)
    {
        _appState = appState;
        _empresa = empresa;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_appState.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appState.Token);

        // Diz de qual empresa e a loja. So conta ANTES do login (cadastro, login, catalogo): com token, o
        // servidor le a empresa do token e ignora este cabecalho.
        if (_empresa.Slug is not null)
            request.Headers.TryAddWithoutValidation(Papeis.CabecalhoEmpresa, _empresa.Slug);

        return base.SendAsync(request, cancellationToken);
    }
}
