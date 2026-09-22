namespace AutonomousStore.CriadorApp;

public class AppState
{
    public string? Token { get; private set; }
    public string? CriadorName { get; private set; }
    public string? CriadorEmail { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? OnChange;

    public void Login(string token, string name, string email)
    {
        Token = token;
        CriadorName = name;
        CriadorEmail = email;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        Token = null;
        CriadorName = null;
        CriadorEmail = null;
        OnChange?.Invoke();
    }
}
