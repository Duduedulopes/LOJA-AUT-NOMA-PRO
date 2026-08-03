using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Representa a empresa dona de um catálogo (produtos, categorias). Existe pra deixar o sistema
/// padronizado — qualquer empresa que use a plataforma vira um registro aqui, em vez do catálogo
/// ser "hardcoded" para uma única loja.
/// </summary>
public class Company : Entity
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public bool IsActive { get; private set; }

    protected Company() { }

    public Company(string name, string? description = null, string? logoUrl = null, string? contactEmail = null, string? contactPhone = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da empresa não pode ser vazio.", nameof(name));

        Name = name;
        Description = description;
        LogoUrl = logoUrl;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        IsActive = true;
    }

    public void UpdateDetails(string name, string? description, string? logoUrl, string? contactEmail, string? contactPhone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da empresa não pode ser vazio.", nameof(name));

        Name = name;
        Description = description;
        LogoUrl = logoUrl;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
