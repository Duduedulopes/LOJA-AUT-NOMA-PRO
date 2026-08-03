using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

public class Category : Entity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    protected Category() { }

    public Category(Guid companyId, string name, string? description = null, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da categoria não pode ser vazio.", nameof(name));

        CompanyId = companyId;
        Name = name;
        Description = description;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public void UpdateDetails(string name, string? description, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da categoria não pode ser vazio.", nameof(name));

        Name = name;
        Description = description;
        DisplayOrder = displayOrder;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
