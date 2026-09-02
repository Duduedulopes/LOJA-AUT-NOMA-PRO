using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarOcorrencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ocorrencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuandoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sistema = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Operacao = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Severidade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DadosEnvolvidos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sequencia = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CausaProvavel = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CausaRaiz = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Impacto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Recomendacao = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AcaoExecutada = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VistaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvidaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvidaPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NotaDoAdmin = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Chave = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ocorrencias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_Chave",
                table: "Ocorrencias",
                column: "Chave",
                unique: true,
                filter: "[Chave] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_CorrelationId",
                table: "Ocorrencias",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_Estado",
                table: "Ocorrencias",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_QuandoUtc",
                table: "Ocorrencias",
                column: "QuandoUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ocorrencias");
        }
    }
}
