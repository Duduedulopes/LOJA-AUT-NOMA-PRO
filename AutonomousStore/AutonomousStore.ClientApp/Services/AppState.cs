using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

/// <summary>
/// Guarda em memória quem é o cliente logado (com o token JWT) e a sessão de compra atual,
/// enquanto o app está aberto. Simplificação para o protótipo: não persiste entre recarregamentos
/// de página (isso pode evoluir depois para localStorage, se quiser manter o login entre visitas).
/// </summary>
public class AppState
{
    public string? Token { get; private set; }
    public Guid? CurrentCustomerId { get; private set; }
    public string? CurrentCustomerName { get; private set; }
    public Guid? CurrentSessionId { get; set; }

    // Dados temporários guardados entre a tentativa de login com Google e a tela que pede o CPF,
    // quando é um cliente novo (o Google não fornece CPF).
    public PendingGoogleSignup? PendingGoogleSignup { get; set; }

    public bool IsAuthenticated => Token is not null && CurrentCustomerId is not null;

    public event Action? OnChange;

    public void SetAuth(string token, Guid customerId, string customerName)
    {
        Token = token;
        CurrentCustomerId = customerId;
        CurrentCustomerName = customerName;
        PendingGoogleSignup = null;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        Token = null;
        CurrentCustomerId = null;
        CurrentCustomerName = null;
        CurrentSessionId = null;
        OnChange?.Invoke();
    }

    public void SetSession(Guid? sessionId)
    {
        CurrentSessionId = sessionId;
        OnChange?.Invoke();
    }

    public void UpdateCustomerName(string name)
    {
        CurrentCustomerName = name;
        OnChange?.Invoke();
    }
}
