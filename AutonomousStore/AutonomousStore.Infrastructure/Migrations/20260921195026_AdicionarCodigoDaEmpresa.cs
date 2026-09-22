using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCodigoDaEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NASCE NULA e so depois vira obrigatoria: se nascesse "int NOT NULL DEFAULT 0", todas as
            // empresas que ja existem ficariam com 0, e o indice unico logo abaixo falharia assim que
            // houvesse mais de uma.
            migrationBuilder.AddColumn<int>(
                name: "Codigo",
                table: "Tenants",
                type: "int",
                nullable: true);

            // Numera as que ja existem (1, 2, 3...) na ordem em que foram criadas. A empresa padrao,
            // que e a mais antiga, fica com o 1.
            migrationBuilder.Sql(
                "WITH Numeradas AS (SELECT [Id], ROW_NUMBER() OVER (ORDER BY [DataCriacao], [Id]) AS [N] FROM [Tenants]) " +
                "UPDATE t SET t.[Codigo] = n.[N] FROM [Tenants] t INNER JOIN Numeradas n ON n.[Id] = t.[Id]");

            migrationBuilder.AlterColumn<int>(
                name: "Codigo",
                table: "Tenants",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Codigo",
                table: "Tenants",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Codigo",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Tenants");
        }
    }
}
