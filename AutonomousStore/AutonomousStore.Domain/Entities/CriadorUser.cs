using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// O dono da plataforma. Tabela própria, e não um "AdminUser sem empresa", de
/// propósito: se poder total fosse "empresa nula", esquecer de preencher a
/// empresa de um Admin comum o transformaria em Criador sem ninguém perceber.
/// </summary>
public class CriadorUser : Entity
{
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public bool IsActive { get; private set; }

    protected CriadorUser() { }

    public CriadorUser(string name, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome não pode ser vazio.", nameof(name));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O e-mail não pode ser vazio.", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A senha não pode ser vazia.", nameof(passwordHash));

        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
