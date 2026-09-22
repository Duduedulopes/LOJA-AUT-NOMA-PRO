using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Persistence;

public class AutonomousDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public AutonomousDbContext(DbContextOptions<AutonomousDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    // Lidos pelo filtro global a CADA consulta, e nao uma vez so na criacao do
    // modelo: o EF troca estas duas propriedades por parametros da instancia,
    // entao cada requisicao filtra pela SUA empresa, mesmo com o modelo em cache.
    private Guid? TenantAtual => _tenant.TenantId;
    private bool VeTodas => _tenant.VeTodasAsEmpresas;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<CriadorUser> CriadorUsers => Set<CriadorUser>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<StoreSession> StoreSessions => Set<StoreSession>();
    public DbSet<SessionItem> SessionItems => Set<SessionItem>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<SuporteUser> SuporteUsers => Set<SuporteUser>();
    public DbSet<Ocorrencia> Ocorrencias => Set<Ocorrencia>();
    public DbSet<MensagemDeSuporte> MensagensDeSuporte => Set<MensagemDeSuporte>();

    // ── isolamento por empresa: o carimbo ─────────────────────────────────
    //
    // Toda gravacao passa por aqui. E o par do filtro global: o filtro impede
    // LER dado de outra empresa, o carimbo impede GRAVAR nele.
    //
    // Nada disso depende de o controller lembrar de fazer alguma coisa. Um
    // endpoint novo, escrito amanha por quem nao leu este arquivo, ja nasce
    // isolado — porque a regra esta na porta do banco, e nao em cada rota.

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarTenantNasMudancas();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarTenantNasMudancas();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AplicarTenantNasMudancas()
    {
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    CarimbarRegistroNovo(entry.Entity);
                    break;

                case EntityState.Modified when entry.Property(e => e.TenantId).IsModified:
                    throw new InvalidOperationException(
                        "A empresa dona de um registro não pode ser trocada.");
            }
        }
    }

    private void CarimbarRegistroNovo(TenantEntity registro)
    {
        if (registro.TenantId is null)
        {
            // O caso comum: a empresa da requisicao vira a dona do registro novo.
            if (_tenant.TenantId is { } empresa)
            {
                registro.PertenceAoTenant(empresa);
                return;
            }

            // Ocorrencia tambem registra fato da plataforma (uma excecao na
            // partida, um erro de infraestrutura), que nao e de empresa nenhuma.
            if (registro is Ocorrencia)
                return;

            // Sem empresa e sem ser ocorrencia: gravar assim criaria um registro
            // que ninguem enxerga. Melhor falhar alto do que sumir com o dado.
            throw new InvalidOperationException(
                $"Não há empresa definida para gravar {registro.GetType().Name}. " +
                "Quem opera a plataforma precisa dizer em nome de qual empresa está gravando.");
        }

        // O registro ja veio com empresa (o Criador cadastrando algo em nome de
        // uma delas). So quem opera a plataforma pode escolher a empresa; uma
        // empresa comum so grava na propria.
        if (!_tenant.VeTodasAsEmpresas && registro.TenantId != _tenant.TenantId)
            throw new InvalidOperationException("Não é permitido gravar dados de outra empresa.");
    }

    // ── isolamento por empresa: a configuracao ────────────────────────────

    /// <param name="obrigatorio">Falso só onde a plataforma também grava (ocorrências).</param>
    /// <param name="comFiltro">
    /// Falso para o AdminUser: ele é procurado PELO E-MAIL para descobrir a
    /// empresa no login, quando ainda não há empresa nenhuma na requisição.
    /// </param>
    private void ConfigurarTenant<T>(ModelBuilder modelBuilder, bool obrigatorio = true, bool comFiltro = true)
        where T : TenantEntity
    {
        modelBuilder.Entity<T>(entity =>
        {
            entity.Property(e => e.TenantId)
                .HasColumnName("TenantId")
                .IsRequired(obrigatorio);

            // Restrict: apagar uma empresa nao pode levar os dados dela junto
            // por cascata. Suspender e o caminho; apagar exige limpar antes, de proposito.
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            if (comFiltro)
            {
                // FALHA FECHADA: sem empresa definida (TenantAtual nulo) e sem ser
                // da plataforma, nada e visivel. O `TenantAtual != null` explicito
                // importa: sem ele, "nulo == nulo" viraria verdadeiro e uma
                // requisicao anonima enxergaria as linhas sem empresa.
                entity.HasQueryFilter(e => VeTodas || (TenantAtual != null && e.TenantId == TenantAtual));
            }
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ocorrencia>(entity =>
        {
            entity.ToTable("Ocorrencias");

            entity.Property(o => o.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(o => o.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(o => o.QuandoUtc)
                .HasColumnName("QuandoUtc")
                .IsRequired();

            entity.Property(o => o.Sistema)
                .HasColumnName("Sistema")
                .IsRequired()
                .HasMaxLength(60);

            entity.Property(o => o.Modulo)
                .HasColumnName("Modulo")
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(o => o.Operacao)
                .HasColumnName("Operacao")
                .IsRequired()
                .HasMaxLength(120);

            // Enum como TEXTO, igual ao Status e ao Tipo das outras tabelas.
            // Um log que guarda 9 no lugar de "Roubo" precisa do codigo em
            // maos para ser lido — e log serve justamente para quando o
            // codigo nao esta em maos.
            entity.Property(o => o.Tipo)
                .HasColumnName("Tipo")
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(o => o.Severidade)
                .HasColumnName("Severidade")
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(o => o.Recomendacao)
                .HasColumnName("Recomendacao")
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(o => o.Estado)
                .HasColumnName("Estado")
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(o => o.Descricao)
                .HasColumnName("Descricao")
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(o => o.DadosEnvolvidosJson)
                .HasColumnName("DadosEnvolvidos");

            entity.Property(o => o.SequenciaJson)
                .HasColumnName("Sequencia");

            entity.Property(o => o.CausaProvavel)
                .HasColumnName("CausaProvavel")
                .HasMaxLength(1000);

            entity.Property(o => o.CausaRaiz)
                .HasColumnName("CausaRaiz")
                .HasMaxLength(1000);

            entity.Property(o => o.Impacto)
                .HasColumnName("Impacto")
                .HasMaxLength(500);

            entity.Property(o => o.AcaoExecutada)
                .HasColumnName("AcaoExecutada")
                .HasMaxLength(1000);

            entity.Property(o => o.Resultado)
                .HasColumnName("Resultado")
                .HasMaxLength(1000);

            entity.Property(o => o.CorrelationId)
                .HasColumnName("CorrelationId");

            entity.Property(o => o.VistaEm).HasColumnName("VistaEm");
            entity.Property(o => o.ResolvidaEm).HasColumnName("ResolvidaEm");

            entity.Property(o => o.ResolvidaPor)
                .HasColumnName("ResolvidaPor")
                .HasMaxLength(200);

            entity.Property(o => o.NotaDoAdmin)
                .HasColumnName("NotaDoAdmin")
                .HasMaxLength(2000);

            entity.Property(o => o.Chave)
                .HasColumnName("Chave")
                .HasMaxLength(200);

            // Quantas vezes o MESMO fato aconteceu, e quando foi a ultima.
            // `QuandoUtc` continua sendo a primeira vez: sem as duas pontas,
            // um erro que parou ontem e um que ainda esta acontecendo agora
            // ficam iguais na lista.
            entity.Property(o => o.VezesVistas)
                .HasColumnName("VezesVistas")
                .HasDefaultValue(1);

            entity.Property(o => o.UltimaVezUtc)
                .HasColumnName("UltimaVezUtc");

            // De quem é o chamado. Nulo em tudo que veio de detector — só o
            // pedido escrito por uma pessoa tem dono.
            entity.Property(o => o.AbertoPor)
                .HasColumnName("AbertoPor")
                .HasMaxLength(200);

            // "Meus chamados" é a consulta do cliente e do admin. Sem índice,
            // ela varre a tabela inteira de ocorrências — que é a que mais
            // cresce no sistema.
            entity.HasIndex(o => o.AbertoPor);

            // A conversa, apoiada no campo privado _mensagens.
            entity.HasMany(o => o.Mensagens)
                .WithOne()
                .HasForeignKey(m => m.OcorrenciaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(o => o.Mensagens)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            // A consulta mais frequente e "as mais recentes primeiro" — e o
            // que o sino pergunta a cada 20 segundos, em toda tela aberta.
            entity.HasIndex(o => o.QuandoUtc);

            // O sino conta as NOVAS. Sem indice, contar novas custa varrer a
            // tabela inteira — e esta tabela so cresce.
            entity.HasIndex(o => o.Estado);

            // O rastro do suporte: todas as ocorrencias de uma mesma sessao.
            entity.HasIndex(o => o.CorrelationId);

            // A chave e o que impede a mesma ocorrencia cem vezes. Unica, e
            // com filtro porque ocorrencia SEM chave (uma excecao solta, por
            // exemplo) e sempre nova: duas excecoes iguais em momentos
            // diferentes sao dois fatos, nao um repetido.
            //
            // Por empresa: o mesmo erro acontecendo em duas empresas sao dois
            // fatos, cada um no painel de quem o sofreu.
            entity.HasIndex(o => new { o.TenantId, o.Chave })
                .IsUnique()
                .HasFilter("[Chave] IS NOT NULL");
        });

        modelBuilder.Entity<MensagemDeSuporte>(entity =>
        {
            entity.ToTable("MensagensDeSuporte");

            entity.Property(m => m.Id).HasColumnName("Id").ValueGeneratedNever();
            entity.Property(m => m.CreatedAt).HasColumnName("DataCriacao");

            entity.Property(m => m.OcorrenciaId).HasColumnName("OcorrenciaId").IsRequired();
            entity.Property(m => m.QuandoUtc).HasColumnName("QuandoUtc").IsRequired();

            // Enum como TEXTO, igual ao resto desta base: "Cliente", nunca 1.
            entity.Property(m => m.Autor)
                .HasColumnName("Autor")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(m => m.AutorNome).HasColumnName("AutorNome").HasMaxLength(120).IsRequired();
            entity.Property(m => m.AutorEmail).HasColumnName("AutorEmail").HasMaxLength(200);
            entity.Property(m => m.Texto).HasColumnName("Texto").HasMaxLength(4000).IsRequired();

            // Toda leitura de conversa é "as mensagens DESTE chamado, em
            // ordem". Sem o índice, cada abertura de chamado varre a tabela
            // de mensagens inteira.
            entity.HasIndex(m => new { m.OcorrenciaId, m.QuandoUtc });
        });

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

            // Unicos POR EMPRESA: duas lojas de empresas diferentes vendem o mesmo
            // produto (mesmo codigo de barras) sem uma barrar a outra.
            entity.HasIndex(p => new { p.TenantId, p.Barcode })
                .IsUnique();

            entity.HasIndex(p => new { p.TenantId, p.RfidTag })
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

            // Unicos POR EMPRESA: o comprador pertence a uma empresa so, e a mesma
            // pessoa (mesmo e-mail, mesmo CPF) pode ter um cadastro em cada uma.
            entity.HasIndex(c => new { c.TenantId, c.Email })
                .IsUnique();

            entity.HasIndex(c => new { c.TenantId, c.Cpf })
                .IsUnique();

            entity.HasIndex(c => new { c.TenantId, c.GoogleId })
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

        modelBuilder.Entity<SuporteUser>(entity =>
        {
            entity.ToTable("UsuariosSuporte");

            entity.Property(s => s.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(s => s.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(s => s.Name)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(s => s.Email)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(s => s.PhoneNumber)
                .HasColumnName("Telefone")
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(s => s.Cpf)
                .HasColumnName("Cpf")
                .IsRequired()
                .HasMaxLength(11);

            entity.Property(s => s.PasswordHash)
                .HasColumnName("SenhaHash")
                .IsRequired();

            entity.Property(s => s.IsActive)
                .HasColumnName("Ativo");

            entity.HasIndex(s => s.Cpf)
                .IsUnique();

            entity.HasIndex(s => s.Email)
                .IsUnique();
        });

        modelBuilder.Entity<CriadorUser>(entity =>
        {
            entity.ToTable("UsuariosCriador");

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

            entity.Property(c => c.PasswordHash)
                .HasColumnName("SenhaHash")
                .IsRequired();

            entity.Property(c => c.IsActive)
                .HasColumnName("Ativo");

            entity.HasIndex(c => c.Email)
                .IsUnique();
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");

            entity.Property(t => t.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(t => t.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(t => t.Nome)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Slug)
                .HasColumnName("Slug")
                .IsRequired()
                .HasMaxLength(40);

            // O numero que o suporte usa no lugar de qualquer dado da empresa.
            entity.Property(t => t.Codigo)
                .HasColumnName("Codigo");

            entity.HasIndex(t => t.Codigo)
                .IsUnique();

            // Enum como TEXTO, igual ao resto desta base: "Suspensa", nunca 2.
            entity.Property(t => t.Status)
                .HasColumnName("Status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.LimiteDeLojas)
                .HasColumnName("LimiteDeLojas");

            // O slug e o endereco publico da empresa: dois iguais seriam duas
            // empresas disputando o mesmo link.
            entity.HasIndex(t => t.Slug)
                .IsUnique();

            // Calculadas, nao guardadas: o Status ja diz, e o rotulo e Codigo + Nome.
            entity.Ignore(t => t.EstaAtiva);
            entity.Ignore(t => t.Rotulo);
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.ToTable("Lojas");

            entity.Property(s => s.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(s => s.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(s => s.Nome)
                .HasColumnName("Nome")
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(s => s.Segmento)
                .HasColumnName("Segmento")
                .HasMaxLength(100);

            entity.Property(s => s.IsActive)
                .HasColumnName("Ativa");
        });

        modelBuilder.Entity<Auditoria>(entity =>
        {
            entity.ToTable("Auditorias");

            entity.Property(a => a.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever();

            entity.Property(a => a.CreatedAt)
                .HasColumnName("DataCriacao");

            entity.Property(a => a.QuandoUtc).HasColumnName("QuandoUtc").IsRequired();
            entity.Property(a => a.AtorId).HasColumnName("AtorId");
            entity.Property(a => a.AtorPapel).HasColumnName("AtorPapel").IsRequired().HasMaxLength(30);
            entity.Property(a => a.AtorNome).HasColumnName("AtorNome").IsRequired().HasMaxLength(200);
            entity.Property(a => a.TenantAlvoId).HasColumnName("TenantAlvoId");
            entity.Property(a => a.Acao).HasColumnName("Acao").IsRequired().HasMaxLength(80);
            entity.Property(a => a.Recurso).HasColumnName("Recurso").IsRequired().HasMaxLength(80);
            entity.Property(a => a.RecursoId).HasColumnName("RecursoId").HasMaxLength(100);
            entity.Property(a => a.Motivo).HasColumnName("Motivo").HasMaxLength(500);
            entity.Property(a => a.Ip).HasColumnName("Ip").HasMaxLength(64);

            // Sem chave estrangeira para a empresa, de proposito: a auditoria
            // tem de sobreviver a qualquer coisa que aconteca com a empresa.

            // "O que o suporte fez, mais recente primeiro" e "o que foi feito
            // nesta empresa" sao as duas perguntas do painel do Criador.
            entity.HasIndex(a => a.QuandoUtc);
            entity.HasIndex(a => a.TenantAlvoId);
            entity.HasIndex(a => a.AtorId);
        });

        // ── o filtro e o vinculo com a empresa, em um lugar so ────────────
        ConfigurarTenant<Product>(modelBuilder);
        ConfigurarTenant<Company>(modelBuilder);
        ConfigurarTenant<Category>(modelBuilder);
        ConfigurarTenant<Customer>(modelBuilder);
        ConfigurarTenant<StoreSession>(modelBuilder);
        ConfigurarTenant<Store>(modelBuilder);
        ConfigurarTenant<Ocorrencia>(modelBuilder, obrigatorio: false);
        ConfigurarTenant<AdminUser>(modelBuilder, comFiltro: false);
    }
}
