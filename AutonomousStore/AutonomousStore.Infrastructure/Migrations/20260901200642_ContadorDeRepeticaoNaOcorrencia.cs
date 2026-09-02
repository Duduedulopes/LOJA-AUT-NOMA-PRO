using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContadorDeRepeticaoNaOcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaVezUtc",
                table: "Ocorrencias",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VezesVistas",
                table: "Ocorrencias",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimaVezUtc",
                table: "Ocorrencias");

            migrationBuilder.DropColumn(
                name: "VezesVistas",
                table: "Ocorrencias");
        }
    }
}
