namespace AutonomousStore.SuporteApp;

public class AppState
{
    public string? Token { get; private set; }
    public string? SuporteName { get; private set; }
    public string? SuporteEmail { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? OnChange;

    public void Login(string token, string name, string email)
    {
        Token = token;
        SuporteName = name;
        SuporteEmail = email;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        Token = null;
        SuporteName = null;
        SuporteEmail = null;
        OnChange?.Invoke();
    }
}
