using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Técnico de suporte que atende várias lojas. Separado do AdminUser de propósito:
/// admin é o dono da loja (cuida da sua loja), suporte é uma população externa que
/// precisa ver o que o dono não vê, e o dono da loja não pode criar um usuário
/// de suporte pela tela dele.
/// </summary>
public class SuporteUser : Entity
{
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";

    /// <summary>Para ligar. Técnico é gente que alguém precisa alcançar.</summary>
    public string PhoneNumber { get; private set; } = "";

    /// <summary>Guardado só com os 11 dígitos, sem pontuação.</summary>
    /// <remarks>
    /// NORMALIZADO NA ENTRADA, não na consulta. "123.456.789-00" e
    /// "12345678900" são a mesma pessoa; se cada tela gravasse do seu jeito,
    /// a busca por CPF acharia um e não o outro — e ninguém descobriria,
    /// porque a consulta simplesmente voltaria vazia. É o mesmo motivo pelo
    /// qual o `Customer` normaliza.
    /// </remarks>
    public string Cpf { get; private set; } = "";

    public string PasswordHash { get; private set; } = "";
    public bool IsActive { get; private set; }

    protected SuporteUser() { }

    public SuporteUser(string name, string email, string phoneNumber, string cpf, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome não pode ser vazio.", nameof(name));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O e-mail não pode ser vazio.", nameof(email));

        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("O telefone não pode ser vazio.", nameof(phoneNumber));

        if (!CpfValidation.IsValid(cpf))
            throw new ArgumentException("CPF inválido.", nameof(cpf));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A senha não pode ser vazia.", nameof(passwordHash));

        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Cpf = CpfValidation.Normalize(cpf);
        PasswordHash = passwordHash;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
