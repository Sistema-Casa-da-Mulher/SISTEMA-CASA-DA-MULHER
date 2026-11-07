using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgendaCalendario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgendaAgendamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Area = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AssistidaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ResponsavelUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FimUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeslocamentoMinutos = table.Column<int>(type: "INTEGER", nullable: false),
                    Presencial = table.Column<bool>(type: "INTEGER", nullable: false),
                    AvisarRecepcao = table.Column<bool>(type: "INTEGER", nullable: false),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    RemarcadoDeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChegadaEstado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ChegouEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChegadaMarcadaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    RecebidaEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Justificativa = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ExcecaoMotivo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ExcecaoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CriadoPorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CriadoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaAgendamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaAgendamentos_Assistidas_AssistidaId",
                        column: x => x.AssistidaId,
                        principalTable: "Assistidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgendaCompromissos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DonoUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    RegistradoPorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FimUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DiaInteiro = table.Column<bool>(type: "INTEGER", nullable: false),
                    OcupaHorario = table.Column<bool>(type: "INTEGER", nullable: false),
                    Local = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    OrigemArea = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    AssistidaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Visibilidade = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SituacaoAusencia = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    AusenciaConfirmadaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SerieId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Justificativa = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ExcecaoMotivo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ExcecaoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CursoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Comunicacao = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaCompromissos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgendaConfiguracoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LimiteCompromissosDia = table.Column<int>(type: "INTEGER", nullable: false),
                    LimiteHorasDia = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpedienteInicio = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    ExpedienteFim = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaConfiguracoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgendaDiasEspeciais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Nota = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ReabertaMotivo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ReabertaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ReabertaEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CriadoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaDiasEspeciais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AulasCancelamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CursoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Escopo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DesfeitoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AulasCancelamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CursosHorarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CursoId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiaSemana = table.Column<int>(type: "INTEGER", nullable: false),
                    Inicio = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    Fim = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    Sala = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ValidoDesde = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CursosHorarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CursosHorarios_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReavaliacoesRiscoPsicologicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioPsicologicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Classificacao = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ProximaAcao = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ProfissionalUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ProfissionalIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ProfissionalNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReavaliacoesRiscoPsicologicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReavaliacoesRiscoPsicologicas_ProntuariosPsicologicos_ProntuarioPsicologicoId",
                        column: x => x.ProntuarioPsicologicoId,
                        principalTable: "ProntuariosPsicologicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgendaAgendamentosHistorico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgendaAgendamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Texto = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    PorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaAgendamentosHistorico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaAgendamentosHistorico_AgendaAgendamentos_AgendaAgendamentoId",
                        column: x => x.AgendaAgendamentoId,
                        principalTable: "AgendaAgendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgendaRevisoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgendaAgendamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ParaUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvidoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaRevisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaRevisoes_AgendaAgendamentos_AgendaAgendamentoId",
                        column: x => x.AgendaAgendamentoId,
                        principalTable: "AgendaAgendamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgendaCompromissosPessoas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgendaCompromissoId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Papel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaCompromissosPessoas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaCompromissosPessoas_AgendaCompromissos_AgendaCompromissoId",
                        column: x => x.AgendaCompromissoId,
                        principalTable: "AgendaCompromissos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaAgendamentos_AssistidaId_InicioUtc",
                table: "AgendaAgendamentos",
                columns: new[] { "AssistidaId", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaAgendamentos_ResponsavelUserId_InicioUtc",
                table: "AgendaAgendamentos",
                columns: new[] { "ResponsavelUserId", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaAgendamentosHistorico_AgendaAgendamentoId",
                table: "AgendaAgendamentosHistorico",
                column: "AgendaAgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaCompromissos_DonoUserId_InicioUtc",
                table: "AgendaCompromissos",
                columns: new[] { "DonoUserId", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaCompromissos_InicioUtc",
                table: "AgendaCompromissos",
                column: "InicioUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaCompromissosPessoas_AgendaCompromissoId_UserId",
                table: "AgendaCompromissosPessoas",
                columns: new[] { "AgendaCompromissoId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgendaCompromissosPessoas_UserId",
                table: "AgendaCompromissosPessoas",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaDiasEspeciais_Data",
                table: "AgendaDiasEspeciais",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaRevisoes_AgendaAgendamentoId",
                table: "AgendaRevisoes",
                column: "AgendaAgendamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaRevisoes_ParaUserId_Situacao",
                table: "AgendaRevisoes",
                columns: new[] { "ParaUserId", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_AulasCancelamentos_CursoId_Data",
                table: "AulasCancelamentos",
                columns: new[] { "CursoId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_CursosHorarios_CursoId",
                table: "CursosHorarios",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReavaliacoesRiscoPsicologicas_ProntuarioPsicologicoId",
                table: "ReavaliacoesRiscoPsicologicas",
                column: "ProntuarioPsicologicoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgendaAgendamentosHistorico");

            migrationBuilder.DropTable(
                name: "AgendaCompromissosPessoas");

            migrationBuilder.DropTable(
                name: "AgendaConfiguracoes");

            migrationBuilder.DropTable(
                name: "AgendaDiasEspeciais");

            migrationBuilder.DropTable(
                name: "AgendaRevisoes");

            migrationBuilder.DropTable(
                name: "AulasCancelamentos");

            migrationBuilder.DropTable(
                name: "CursosHorarios");

            migrationBuilder.DropTable(
                name: "ReavaliacoesRiscoPsicologicas");

            migrationBuilder.DropTable(
                name: "AgendaCompromissos");

            migrationBuilder.DropTable(
                name: "AgendaAgendamentos");
        }
    }
}
