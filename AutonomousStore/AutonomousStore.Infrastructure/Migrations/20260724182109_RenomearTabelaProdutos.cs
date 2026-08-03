using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenomearTabelaProdutos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Products",
                table: "Products");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Produtos");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "Produtos",
                newName: "Preco");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Produtos",
                newName: "Nome");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Produtos",
                newName: "Ativo");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Produtos",
                newName: "DataCriacao");

            migrationBuilder.RenameColumn(
                name: "Barcode",
                table: "Produtos",
                newName: "CodigoBarras");

            migrationBuilder.RenameIndex(
                name: "IX_Products_Barcode",
                table: "Produtos",
                newName: "IX_Produtos_CodigoBarras");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Produtos",
                table: "Produtos",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Produtos",
                table: "Produtos");

            migrationBuilder.RenameTable(
                name: "Produtos",
                newName: "Products");

            migrationBuilder.RenameColumn(
                name: "Preco",
                table: "Products",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "Nome",
                table: "Products",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "DataCriacao",
                table: "Products",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CodigoBarras",
                table: "Products",
                newName: "Barcode");

            migrationBuilder.RenameColumn(
                name: "Ativo",
                table: "Products",
                newName: "IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_Produtos_CodigoBarras",
                table: "Products",
                newName: "IX_Products_Barcode");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Products",
                table: "Products",
                column: "Id");
        }
    }
}
