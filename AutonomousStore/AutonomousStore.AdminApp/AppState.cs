namespace AutonomousStore.AdminApp;

/// <summary>Guarda o estado de login em memória — some se a página for recarregada (protótipo).</summary>
public class AppState
{
    public string? Token { get; private set; }
    public string? AdminName { get; private set; }
    public string? AdminEmail { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? OnChange;

    public void Login(string token, string name, string email)
    {
        Token = token;
        AdminName = name;
        AdminEmail = email;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        Token = null;
        AdminName = null;
        AdminEmail = null;
        OnChange?.Invoke();
    }
}
