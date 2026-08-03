using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Entities;

public class Customer : Entity
{
    private readonly List<PaymentMethod> _paymentMethods = [];

    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PhoneNumber { get; private set; }
    public string Cpf { get; private set; }
    public bool IsActive { get; private set; }

    // Null quando o cliente só usa login social (Google). Nunca guarda a senha em texto puro,
    // só o hash gerado pelo PasswordHasher no lado da WebApi.
    public string? PasswordHash { get; private set; }

    // Id da conta Google vinculada, se houver. Permite logar tanto com senha quanto com Google.
    public string? GoogleId { get; private set; }

    // Token de uso único enviado por e-mail pra confirmar a troca de senha ("esqueci minha senha").
    // Fica nulo depois de usado ou expirado.
    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }

    public IReadOnlyList<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    protected Customer() { }

    private Customer(string name, string email, string phoneNumber, string cpf)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome não pode ser vazio.", nameof(name));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O e-mail não pode ser vazio.", nameof(email));

        if (!CpfValidation.IsValid(cpf))
            throw new ArgumentException("CPF inválido.", nameof(cpf));

        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Cpf = CpfValidation.Normalize(cpf);
        IsActive = true;
    }

    /// <summary>Cadastro tradicional, com senha. O <paramref name="passwordHash"/> já deve chegar criptografado.</summary>
    public static Customer RegisterWithPassword(string name, string email, string phoneNumber, string cpf, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A senha não pode ser vazia.", nameof(passwordHash));

        var customer = new Customer(name, email, phoneNumber, cpf)
        {
            PasswordHash = passwordHash
        };

        return customer;
    }

    /// <summary>Cadastro via login social do Google — não tem senha própria.</summary>
    public static Customer RegisterWithGoogle(string name, string email, string phoneNumber, string cpf, string googleId)
    {
        if (string.IsNullOrWhiteSpace(googleId))
            throw new ArgumentException("O id da conta Google é obrigatório.", nameof(googleId));

        var customer = new Customer(name, email, phoneNumber, cpf)
        {
            GoogleId = googleId
        };

        return customer;
    }

    /// <summary>Vincula uma conta Google a um cadastro que já existia (feito por senha).</summary>
    public void LinkGoogleAccount(string googleId)
    {
        if (string.IsNullOrWhiteSpace(googleId))
            throw new ArgumentException("O id da conta Google é obrigatório.", nameof(googleId));

        GoogleId = googleId;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A senha não pode ser vazia.", nameof(passwordHash));

        PasswordHash = passwordHash;
    }

    /// <summary>Atualiza nome e telefone do cliente (dados de perfil que não exigem verificação extra).</summary>
    public void UpdateProfile(string name, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome não pode ser vazio.", nameof(name));

        Name = name;
        PhoneNumber = phoneNumber;
    }

    /// <summary>Troca o e-mail do cliente. A unicidade é verificada na camada de aplicação (WebApi).</summary>
    public void ChangeEmail(string newEmail)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("O e-mail não pode ser vazio.", nameof(newEmail));

        Email = newEmail;
    }

    /// <summary>Gera um novo token de redefinição de senha, substituindo qualquer um anterior.</summary>
    public void SetPasswordResetToken(string token, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("O token não pode ser vazio.", nameof(token));

        PasswordResetToken = token;
        PasswordResetTokenExpiresAt = expiresAtUtc;
    }

    /// <summary>
    /// Confere se o token de redefinição informado é válido (bate com o gerado e ainda não expirou).
    /// </summary>
    public bool IsPasswordResetTokenValid(string token)
    {
        return !string.IsNullOrEmpty(PasswordResetToken)
            && PasswordResetTokenExpiresAt is not null
            && PasswordResetTokenExpiresAt > DateTime.UtcNow
            && PasswordResetToken == token;
    }

    public void ClearPasswordResetToken()
    {
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
    }

    public PaymentMethod AddPaymentMethod(
        PaymentMethodType type,
        string provider,
        string providerToken,
        string lastFourDigits)
    {
        var isFirst = _paymentMethods.Count == 0;

        var paymentMethod = new PaymentMethod(
            Id,
            type,
            provider,
            providerToken,
            lastFourDigits,
            isDefault: isFirst);

        _paymentMethods.Add(paymentMethod);
        return paymentMethod;
    }

    public void RemovePaymentMethod(Guid paymentMethodId)
    {
        var paymentMethod = _paymentMethods.FirstOrDefault(p => p.Id == paymentMethodId);

        if (paymentMethod is null)
            return;

        _paymentMethods.Remove(paymentMethod);

        if (paymentMethod.IsDefault && _paymentMethods.Count > 0)
            _paymentMethods[0].SetAsDefault();
    }

    public void SetDefaultPaymentMethod(Guid paymentMethodId)
    {
        var target = _paymentMethods.FirstOrDefault(p => p.Id == paymentMethodId)
            ?? throw new InvalidOperationException("Forma de pagamento não encontrada para este cliente.");

        foreach (var method in _paymentMethods)
            method.UnsetAsDefault();

        target.SetAsDefault();
    }

    public void Deactivate() => IsActive = false;
}