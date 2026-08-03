using AutonomousStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Persistence;

public class AutonomousDbContext : DbContext
{
    public AutonomousDbContext(DbContextOptions<AutonomousDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<StoreSession> StoreSessions => Set<StoreSession>();
    public DbSet<SessionItem> SessionItems => Set<SessionItem>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Produtos");

            entity.Property(p => p.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(p => p.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(p => p.Name)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(p => p.Barcode)
                .HasColumnName("CodigoBarras")
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(p => p.Price)
                .HasColumnName("Preco")
                .HasColumnType("decimal(18,2)");

            entity.Property(p => p.IsActive)
                .HasColumnName("Ativo");

            entity.Property(p => p.Description)
                .HasColumnName("Descricao")
                .HasMaxLength(1000);

            entity.Property(p => p.ImageUrl)
                .HasColumnName("UrlImagem")
                .HasMaxLength(500);

            entity.Property(p => p.CompanyId)
                .HasColumnName("EmpresaId");

            entity.Property(p => p.CategoryId)
                .HasColumnName("CategoriaId");

            entity.Property(p => p.RfidTag)
                .HasColumnName("TagRfid")
                .HasMaxLength(100);

            entity.HasIndex(p => p.Barcode)
                .IsUnique();

            entity.HasIndex(p => p.RfidTag)
                .IsUnique()
                .HasFilter("[TagRfid] IS NOT NULL");

            entity.Property(p => p.StockQuantity)
                .HasColumnName("QuantidadeEstoque");

            entity.Property(p => p.MinimumStockThreshold)
                .HasColumnName("EstoqueMinimo");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Empresas");

            entity.Property(c => c.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(c => c.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(c => c.Name)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.Description)
                .HasColumnName("Descricao")
                .HasMaxLength(1000);

            entity.Property(c => c.LogoUrl)
                .HasColumnName("UrlLogo")
                .HasMaxLength(500);

            entity.Property(c => c.ContactEmail)
                .HasColumnName("EmailContato")
                .HasMaxLength(200);

            entity.Property(c => c.ContactPhone)
                .HasColumnName("TelefoneContato")
                .HasMaxLength(20);

            entity.Property(c => c.IsActive)
                .HasColumnName("Ativo");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categorias");

            entity.Property(c => c.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(c => c.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(c => c.CompanyId)
                .HasColumnName("EmpresaId");

            entity.Property(c => c.Name)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.Description)
                .HasColumnName("Descricao")
                .HasMaxLength(1000);

            entity.Property(c => c.DisplayOrder)
                .HasColumnName("Ordem");

            entity.Property(c => c.IsActive)
                .HasColumnName("Ativo");

            entity.HasOne<Company>()
                .WithMany()
                .HasForeignKey(c => c.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Clientes");

            entity.Property(c => c.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(c => c.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(c => c.Name)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.Email)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.PhoneNumber)
                .HasColumnName("Telefone")
                .HasMaxLength(20);

            entity.Property(c => c.Cpf)
                .HasColumnName("Cpf")
                .IsRequired()
                .HasMaxLength(11);

            entity.Property(c => c.PasswordHash)
                .HasColumnName("SenhaHash")
                .HasMaxLength(500);

            entity.Property(c => c.GoogleId)
                .HasColumnName("GoogleId")
                .HasMaxLength(100);

            entity.Property(c => c.PasswordResetToken)
                .HasColumnName("TokenRedefinicaoSenha")
                .HasMaxLength(200);

            entity.Property(c => c.PasswordResetTokenExpiresAt)
                .HasColumnName("TokenRedefinicaoSenhaExpiraEm");

            entity.Property(c => c.IsActive)
                .HasColumnName("Ativo");

            entity.HasIndex(c => c.Email)
                .IsUnique();

            entity.HasIndex(c => c.Cpf)
                .IsUnique();

            entity.HasIndex(c => c.GoogleId)
                .IsUnique()
                .HasFilter("[GoogleId] IS NOT NULL");

            // Customer exp�e PaymentMethods como IReadOnlyList apoiado no campo privado _paymentMethods.
            entity.HasMany(c => c.PaymentMethods)
                .WithOne()
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(c => c.PaymentMethods)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("FormasPagamento");

            entity.Property(p => p.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(p => p.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(p => p.CustomerId)
                .HasColumnName("ClienteId");

            entity.Property(p => p.Type)
                .HasColumnName("Tipo")
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(p => p.Provider)
                .HasColumnName("Provedor")
                .HasMaxLength(50);

            entity.Property(p => p.ProviderToken)
                .HasColumnName("TokenProvedor")
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(p => p.LastFourDigits)
                .HasColumnName("UltimosQuatroDigitos")
                .HasMaxLength(4);

            entity.Property(p => p.IsDefault)
                .HasColumnName("Padrao");
        });

        modelBuilder.Entity<StoreSession>(entity =>
        {
            entity.ToTable("SessoesCompra");

            entity.Property(s => s.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(s => s.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(s => s.CustomerId)
                .HasColumnName("ClienteId");

            entity.Property(s => s.QrCodeToken)
                .HasColumnName("QrCodeToken")
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(s => s.QrCodeExpiresAt)
                .HasColumnName("QrCodeExpiraEm");

            entity.Property(s => s.Status)
                .HasColumnName("Status")
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(s => s.EntryConfirmedAt)
                .HasColumnName("EntradaConfirmadaEm");

            entity.Property(s => s.ClosedAt)
                .HasColumnName("FechadaEm");

            entity.Property(s => s.PaymentMethodId)
                .HasColumnName("FormaPagamentoId");

            entity.Property(s => s.PaymentConfirmedAt)
                .HasColumnName("PagamentoConfirmadoEm");

            entity.HasIndex(s => s.QrCodeToken)
                .IsUnique();

            // StoreSession exp�e Items como IReadOnlyList apoiado no campo privado _items.
            entity.HasMany(s => s.Items)
                .WithOne()
                .HasForeignKey(i => i.StoreSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(s => s.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SessionItem>(entity =>
        {
            entity.ToTable("ItensSessao");

            entity.Property(i => i.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(i => i.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(i => i.StoreSessionId)
                .HasColumnName("SessaoId");

            entity.Property(i => i.ProductId)
                .HasColumnName("ProdutoId");

            entity.Property(i => i.ProductName)
                .HasColumnName("NomeProduto")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(i => i.UnitPrice)
                .HasColumnName("PrecoUnitario")
                .HasColumnType("decimal(18,2)");

            entity.Property(i => i.Quantity)
                .HasColumnName("Quantidade");
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("UsuariosAdmin");

            entity.Property(a => a.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(a => a.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(a => a.Name)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(a => a.Email)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(a => a.PasswordHash)
                .HasColumnName("SenhaHash")
                .IsRequired();

            entity.Property(a => a.IsActive)
                .HasColumnName("Ativo");

            entity.HasIndex(a => a.Email)
                .IsUnique();
        });
    }
}
