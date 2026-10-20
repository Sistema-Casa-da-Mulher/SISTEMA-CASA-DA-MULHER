using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class PsicologiaECrp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Crp",
                table: "FuncionariosConvites",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EncaminhadaPsicologiaEm",
                table: "Assistidas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Crp",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProntuariosPsicologicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssistidaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Ano = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequencial = table.Column<int>(type: "INTEGER", nullable: false),
                    Visibilidade = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    MotivoEncerramento = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    EncerradoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RiscoAtual = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    RiscoAtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsentimentoSigiloEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsentimentoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProntuariosPsicologicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProntuariosPsicologicos_Assistidas_AssistidaId",
                        column: x => x.AssistidaId,
                        principalTable: "Assistidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProntuariosPsicologicosAcessosEmergencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioPsicologicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Identificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProntuariosPsicologicosAcessosEmergencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProntuariosPsicologicosAcessosEmergencia_ProntuariosPsicologicos_ProntuarioPsicologicoId",
                        column: x => x.ProntuarioPsicologicoId,
                        principalTable: "ProntuariosPsicologicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProntuariosPsicologicosResponsaveis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioPsicologicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Identificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Crp = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    AtribuidoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AtribuidoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProntuariosPsicologicosResponsaveis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProntuariosPsicologicosResponsaveis_ProntuariosPsicologicos_ProntuarioPsicologicoId",
                        column: x => x.ProntuarioPsicologicoId,
                        principalTable: "ProntuariosPsicologicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessoesPsicologicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioPsicologicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfissionalUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ProfissionalIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ProfissionalNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProfissionalCrp = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TipoAtendimento = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Presenca = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Modalidade = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Demanda = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Evolucao = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    Plano = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ProximaSessao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Risco = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    EncaminhamentosExternos = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EncaminhamentoExternoOutro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessoesPsicologicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessoesPsicologicas_ProntuariosPsicologicos_ProntuarioPsicologicoId",
                        column: x => x.ProntuarioPsicologicoId,
                        principalTable: "ProntuariosPsicologicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessoesPsicologicasAdendos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessaoPsicologicaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Texto = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AutorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AutorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AutorNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessoesPsicologicasAdendos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessoesPsicologicasAdendos_SessoesPsicologicas_SessaoPsicologicaId",
                        column: x => x.SessaoPsicologicaId,
                        principalTable: "SessoesPsicologicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assistidas_EncaminhadaPsicologiaEm",
                table: "Assistidas",
                column: "EncaminhadaPsicologiaEm");

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosPsicologicos_Ano_Sequencial",
                table: "ProntuariosPsicologicos",
                columns: new[] { "Ano", "Sequencial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosPsicologicos_AssistidaId",
                table: "ProntuariosPsicologicos",
                column: "AssistidaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosPsicologicos_Numero",
                table: "ProntuariosPsicologicos",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosPsicologicosAcessosEmergencia_ProntuarioPsicologicoId_UserId",
                table: "ProntuariosPsicologicosAcessosEmergencia",
                columns: new[] { "ProntuarioPsicologicoId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosPsicologicosResponsaveis_ProntuarioPsicologicoId_UserId",
                table: "ProntuariosPsicologicosResponsaveis",
                columns: new[] { "ProntuarioPsicologicoId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosPsicologicosResponsaveis_UserId",
                table: "ProntuariosPsicologicosResponsaveis",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesPsicologicas_CriadoEm",
                table: "SessoesPsicologicas",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesPsicologicas_ProntuarioPsicologicoId_Numero",
                table: "SessoesPsicologicas",
                columns: new[] { "ProntuarioPsicologicoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessoesPsicologicasAdendos_SessaoPsicologicaId",
                table: "SessoesPsicologicasAdendos",
                column: "SessaoPsicologicaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProntuariosPsicologicosAcessosEmergencia");

            migrationBuilder.DropTable(
                name: "ProntuariosPsicologicosResponsaveis");

            migrationBuilder.DropTable(
                name: "SessoesPsicologicasAdendos");

            migrationBuilder.DropTable(
                name: "SessoesPsicologicas");

            migrationBuilder.DropTable(
                name: "ProntuariosPsicologicos");

            migrationBuilder.DropIndex(
                name: "IX_Assistidas_EncaminhadaPsicologiaEm",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "Crp",
                table: "FuncionariosConvites");

            migrationBuilder.DropColumn(
                name: "EncaminhadaPsicologiaEm",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "Crp",
                table: "AspNetUsers");
        }
    }
}
