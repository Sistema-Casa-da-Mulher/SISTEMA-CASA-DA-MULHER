using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class CursosAulasChamada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EncaminhadaAssistenciaSocialEm",
                table: "Assistidas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cursos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistidasCursos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssistidaId = table.Column<int>(type: "INTEGER", nullable: false),
                    CursoId = table.Column<int>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistidasCursos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistidasCursos_Assistidas_AssistidaId",
                        column: x => x.AssistidaId,
                        principalTable: "Assistidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistidasCursos_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Chamadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CursoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tema = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PlanoAulaId = table.Column<int>(type: "INTEGER", nullable: true),
                    AtividadesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RegistradaPorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    RegistradaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RegistradaPorNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chamadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chamadas_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanosAula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CursoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tema = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AtividadesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Observacoes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Origem = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CriadoPorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CriadoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanosAula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanosAula_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfessoresCursos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CursoId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtribuidoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessoresCursos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessoresCursos_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChamadasRegistros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChamadaId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssistidaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChamadasRegistros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChamadasRegistros_Chamadas_ChamadaId",
                        column: x => x.ChamadaId,
                        principalTable: "Chamadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistidasCursos_AssistidaId_CursoId",
                table: "AssistidasCursos",
                columns: new[] { "AssistidaId", "CursoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistidasCursos_CursoId",
                table: "AssistidasCursos",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_Chamadas_CursoId_Data",
                table: "Chamadas",
                columns: new[] { "CursoId", "Data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChamadasRegistros_ChamadaId_AssistidaId",
                table: "ChamadasRegistros",
                columns: new[] { "ChamadaId", "AssistidaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_Nome",
                table: "Cursos",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_PlanosAula_CursoId_Data",
                table: "PlanosAula",
                columns: new[] { "CursoId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessoresCursos_CursoId",
                table: "ProfessoresCursos",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessoresCursos_UserId_CursoId",
                table: "ProfessoresCursos",
                columns: new[] { "UserId", "CursoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistidasCursos");

            migrationBuilder.DropTable(
                name: "ChamadasRegistros");

            migrationBuilder.DropTable(
                name: "PlanosAula");

            migrationBuilder.DropTable(
                name: "ProfessoresCursos");

            migrationBuilder.DropTable(
                name: "Chamadas");

            migrationBuilder.DropTable(
                name: "Cursos");

            migrationBuilder.DropColumn(
                name: "EncaminhadaAssistenciaSocialEm",
                table: "Assistidas");
        }
    }
}
