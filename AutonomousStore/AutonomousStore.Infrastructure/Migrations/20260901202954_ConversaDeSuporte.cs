using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConversaDeSuporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbertoPor",
                table: "Ocorrencias",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MensagensDeSuporte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OcorrenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuandoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutorNome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AutorEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Texto = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensagensDeSuporte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensagensDeSuporte_Ocorrencias_OcorrenciaId",
                        column: x => x.OcorrenciaId,
                        principalTable: "Ocorrencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_AbertoPor",
                table: "Ocorrencias",
                column: "AbertoPor");

            migrationBuilder.CreateIndex(
                name: "IX_MensagensDeSuporte_OcorrenciaId_QuandoUtc",
                table: "MensagensDeSuporte",
                columns: new[] { "OcorrenciaId", "QuandoUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensagensDeSuporte");

            migrationBuilder.DropIndex(
                name: "IX_Ocorrencias_AbertoPor",
                table: "Ocorrencias");

            migrationBuilder.DropColumn(
                name: "AbertoPor",
                table: "Ocorrencias");
        }
    }
}
