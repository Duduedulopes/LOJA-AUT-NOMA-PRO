using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutonomousStore.Infrastructure.Persistence;

/// <summary>
/// Usada só em tempo de design (Add-Migration, Update-Database) — o EF Core chama isso
/// automaticamente quando não consegue montar o DbContext através do DI normal do app
/// (o que acontece quando há mais de um projeto de inicialização configurado na Solution).
/// </summary>
public class AutonomousDbContextFactory : IDesignTimeDbContextFactory<AutonomousDbContext>
{
    public AutonomousDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AutonomousDbContext>();

        // A connection string vem da variável de ambiente AUTONOMOUSSTORE_CONNECTION,
        // para não ficar gravada no código. Definir uma vez, no PowerShell:
        //
        //   [Environment]::SetEnvironmentVariable(
        //       "AUTONOMOUSSTORE_CONNECTION",
        //       "Server=SEU_SERVIDOR\SQLEXPRESS;Database=AutonomousStoreDb;Trusted_Connection=True;TrustServerCertificate=True;",
        //       "User")
        //
        // Depois de definir, feche e reabra o Visual Studio para ele enxergar a variável.
        var connectionString = Environment.GetEnvironmentVariable("AUTONOMOUSSTORE_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A variável de ambiente AUTONOMOUSSTORE_CONNECTION não está definida. " +
                "Ela é necessária para os comandos de migration do EF Core. " +
                "Veja o comentário no topo deste método.");
        }

        optionsBuilder.UseSqlServer(
            connectionString,
            sqlOptions => sqlOptions.MigrationsHistoryTable("MigrationHistory"));

        return new AutonomousDbContext(optionsBuilder.Options);
    }
}