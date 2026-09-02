using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarUsuariosSuporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsuariosSuporte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosSuporte", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSuporte_Cpf",
                table: "UsuariosSuporte",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSuporte_Email",
                table: "UsuariosSuporte",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsuariosSuporte");
        }
    }
}
