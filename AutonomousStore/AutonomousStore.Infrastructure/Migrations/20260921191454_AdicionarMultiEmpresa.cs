using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMultiEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Produtos_CodigoBarras",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_TagRfid",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Ocorrencias_Chave",
                table: "Ocorrencias");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_Cpf",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_Email",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_GoogleId",
                table: "Clientes");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UsuariosAdmin",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SessoesCompra",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Produtos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Ocorrencias",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Empresas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Clientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Categorias",
                type: "uniqueidentifier",
                nullable: true);

            // ── dados que já existiam ─────────────────────────────────────
            //
            // Tudo o que existe hoje foi criado por UMA instalação, então passa
            // a ser da empresa padrão ("Loja Piloto"). As colunas acima nascem
            // NULAS de propósito: se nascessem obrigatórias, o EF as preencheria
            // com 00000000-0000-…, e a chave estrangeira para Tenants — criada
            // mais abaixo — falharia em qualquer banco que já tenha dados.
            //
            // A empresa em si é inserida mais abaixo, depois de a tabela existir.
            // Aqui só se carimba; a chave estrangeira só é criada depois dela.
            migrationBuilder.Sql("UPDATE [UsuariosAdmin] SET [TenantId] = '00000000-0000-0000-0000-000000000001' WHERE [TenantId] IS NULL");
            migrationBuilder.Sql("UPDATE [SessoesCompra] SET [TenantId] = '00000000-0000-0000-0000-000000000001' WHERE [TenantId] IS NULL");
            migrationBuilder.Sql("UPDATE [Produtos] SET [TenantId] = '00000000-0000-0000-0000-000000000001' WHERE [TenantId] IS NULL");
            migrationBuilder.Sql("UPDATE [Empresas] SET [TenantId] = '00000000-0000-0000-0000-000000000001' WHERE [TenantId] IS NULL");
            migrationBuilder.Sql("UPDATE [Clientes] SET [TenantId] = '00000000-0000-0000-0000-000000000001' WHERE [TenantId] IS NULL");
            migrationBuilder.Sql("UPDATE [Categorias] SET [TenantId] = '00000000-0000-0000-0000-000000000001' WHERE [TenantId] IS NULL");
            migrationBuilder.Sql("UPDATE [Ocorrencias] SET [TenantId] = '00000000-0000-0000-0000-000000000001' WHERE [TenantId] IS NULL");

            // Agora que nenhuma linha está sem empresa, a coluna vira obrigatória.
            // (Ocorrencias fica opcional: também guarda eventos da plataforma.)
            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "UsuariosAdmin",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "SessoesCompra",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Produtos",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Empresas",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Clientes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Categorias",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);


            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuandoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtorPapel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AtorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantAlvoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Acao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Recurso = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RecursoId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LimiteDeLojas = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosCriador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosCriador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lojas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Segmento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lojas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lojas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── a empresa e a loja que já existiam ────────────────────────
            //
            // Tem de vir DEPOIS de as tabelas Tenants e Lojas existirem e ANTES
            // das chaves estrangeiras abaixo, que apontam para esta linha.
            migrationBuilder.Sql(
                "INSERT INTO [Tenants] ([Id], [Nome], [Slug], [Status], [LimiteDeLojas], [DataCriacao]) " +
                "VALUES ('00000000-0000-0000-0000-000000000001', N'Loja Piloto', N'piloto', N'Ativa', 1, SYSUTCDATETIME())");

            migrationBuilder.Sql(
                "INSERT INTO [Lojas] ([Id], [Nome], [Segmento], [Ativa], [DataCriacao], [TenantId]) " +
                "VALUES ('00000000-0000-0000-0000-000000000002', N'Loja Piloto', N'Conveniência', 1, SYSUTCDATETIME(), " +
                "'00000000-0000-0000-0000-000000000001')");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosAdmin_TenantId",
                table: "UsuariosAdmin",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesCompra_TenantId",
                table: "SessoesCompra",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_TenantId_CodigoBarras",
                table: "Produtos",
                columns: new[] { "TenantId", "CodigoBarras" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_TenantId_TagRfid",
                table: "Produtos",
                columns: new[] { "TenantId", "TagRfid" },
                unique: true,
                filter: "[TagRfid] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_TenantId_Chave",
                table: "Ocorrencias",
                columns: new[] { "TenantId", "Chave" },
                unique: true,
                filter: "[Chave] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_TenantId",
                table: "Empresas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_TenantId_Cpf",
                table: "Clientes",
                columns: new[] { "TenantId", "Cpf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_TenantId_Email",
                table: "Clientes",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_TenantId_GoogleId",
                table: "Clientes",
                columns: new[] { "TenantId", "GoogleId" },
                unique: true,
                filter: "[GoogleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_TenantId",
                table: "Categorias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_AtorId",
                table: "Auditorias",
                column: "AtorId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_QuandoUtc",
                table: "Auditorias",
                column: "QuandoUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_TenantAlvoId",
                table: "Auditorias",
                column: "TenantAlvoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lojas_TenantId",
                table: "Lojas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCriador_Email",
                table: "UsuariosCriador",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categorias_Tenants_TenantId",
                table: "Categorias",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Tenants_TenantId",
                table: "Clientes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Empresas_Tenants_TenantId",
                table: "Empresas",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ocorrencias_Tenants_TenantId",
                table: "Ocorrencias",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Produtos_Tenants_TenantId",
                table: "Produtos",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessoesCompra_Tenants_TenantId",
                table: "SessoesCompra",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosAdmin_Tenants_TenantId",
                table: "UsuariosAdmin",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categorias_Tenants_TenantId",
                table: "Categorias");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Tenants_TenantId",
                table: "Clientes");

            migrationBuilder.DropForeignKey(
                name: "FK_Empresas_Tenants_TenantId",
                table: "Empresas");

            migrationBuilder.DropForeignKey(
                name: "FK_Ocorrencias_Tenants_TenantId",
                table: "Ocorrencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Produtos_Tenants_TenantId",
                table: "Produtos");

            migrationBuilder.DropForeignKey(
                name: "FK_SessoesCompra_Tenants_TenantId",
                table: "SessoesCompra");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosAdmin_Tenants_TenantId",
                table: "UsuariosAdmin");

            migrationBuilder.DropTable(
                name: "Auditorias");

            migrationBuilder.DropTable(
                name: "Lojas");

            migrationBuilder.DropTable(
                name: "UsuariosCriador");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_UsuariosAdmin_TenantId",
                table: "UsuariosAdmin");

            migrationBuilder.DropIndex(
                name: "IX_SessoesCompra_TenantId",
                table: "SessoesCompra");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_TenantId_CodigoBarras",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_TenantId_TagRfid",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Ocorrencias_TenantId_Chave",
                table: "Ocorrencias");

            migrationBuilder.DropIndex(
                name: "IX_Empresas_TenantId",
                table: "Empresas");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_TenantId_Cpf",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_TenantId_Email",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_TenantId_GoogleId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_TenantId",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UsuariosAdmin");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SessoesCompra");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Ocorrencias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Categorias");

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_CodigoBarras",
                table: "Produtos",
                column: "CodigoBarras",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_TagRfid",
                table: "Produtos",
                column: "TagRfid",
                unique: true,
                filter: "[TagRfid] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_Chave",
                table: "Ocorrencias",
                column: "Chave",
                unique: true,
                filter: "[Chave] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Cpf",
                table: "Clientes",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Email",
                table: "Clientes",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_GoogleId",
                table: "Clientes",
                column: "GoogleId",
                unique: true,
                filter: "[GoogleId] IS NOT NULL");
        }
    }
}
