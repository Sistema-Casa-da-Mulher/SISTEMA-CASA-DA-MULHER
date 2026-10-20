using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class AssistidasEJuridico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assistidas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Cpf = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    DataNascimento = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Telefone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SemTelefoneSeguro = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContatoSeguro = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ContatoSeguroObservacao = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EnderecoSigiloso = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cep = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    Endereco = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EnderecoNumero = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Bairro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Cidade = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Uf = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    Direcionamento = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    EncaminhadaJuridicoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Observacoes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Origem = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CadastradaPorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CadastradaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CadastradaPorNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CadastradaPorEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AcessoRestrito = table.Column<bool>(type: "INTEGER", nullable: false),
                    MotivoRestricao = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    RestricaoAlteradaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    RestricaoAlteradaEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assistidas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CasosJuridicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssistidaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Visibilidade = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasosJuridicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasosJuridicos_Assistidas_AssistidaId",
                        column: x => x.AssistidaId,
                        principalTable: "Assistidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AtendimentosJuridicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CasoJuridicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Protocolo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Ano = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequencial = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfissionalUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ProfissionalIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ProfissionalNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TipoDemanda = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TipoDemandaOutro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    TemMedidaProtetiva = table.Column<bool>(type: "INTEGER", nullable: true),
                    NumeroProcesso = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Vara = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Encaminhamentos = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EncaminhamentoOutro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Relato = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    ProximoPasso = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DataRetorno = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtendimentosJuridicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtendimentosJuridicos_CasosJuridicos_CasoJuridicoId",
                        column: x => x.CasoJuridicoId,
                        principalTable: "CasosJuridicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CasosJuridicosAcessosEmergencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CasoJuridicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Identificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasosJuridicosAcessosEmergencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasosJuridicosAcessosEmergencia_CasosJuridicos_CasoJuridicoId",
                        column: x => x.CasoJuridicoId,
                        principalTable: "CasosJuridicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CasosJuridicosResponsaveis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CasoJuridicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Identificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    AtribuidoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AtribuidoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasosJuridicosResponsaveis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasosJuridicosResponsaveis_CasosJuridicos_CasoJuridicoId",
                        column: x => x.CasoJuridicoId,
                        principalTable: "CasosJuridicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AtendimentosJuridicosAdendos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtendimentoJuridicoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Texto = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AutorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AutorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AutorNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtendimentosJuridicosAdendos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtendimentosJuridicosAdendos_AtendimentosJuridicos_AtendimentoJuridicoId",
                        column: x => x.AtendimentoJuridicoId,
                        principalTable: "AtendimentosJuridicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assistidas_Cpf",
                table: "Assistidas",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assistidas_EncaminhadaJuridicoEm",
                table: "Assistidas",
                column: "EncaminhadaJuridicoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Assistidas_Nome",
                table: "Assistidas",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosJuridicos_Ano_Sequencial",
                table: "AtendimentosJuridicos",
                columns: new[] { "Ano", "Sequencial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosJuridicos_CasoJuridicoId",
                table: "AtendimentosJuridicos",
                column: "CasoJuridicoId");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosJuridicos_CriadoEm",
                table: "AtendimentosJuridicos",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosJuridicos_Protocolo",
                table: "AtendimentosJuridicos",
                column: "Protocolo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosJuridicosAdendos_AtendimentoJuridicoId",
                table: "AtendimentosJuridicosAdendos",
                column: "AtendimentoJuridicoId");

            migrationBuilder.CreateIndex(
                name: "IX_CasosJuridicos_AssistidaId",
                table: "CasosJuridicos",
                column: "AssistidaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CasosJuridicosAcessosEmergencia_CasoJuridicoId_UserId",
                table: "CasosJuridicosAcessosEmergencia",
                columns: new[] { "CasoJuridicoId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CasosJuridicosResponsaveis_CasoJuridicoId_UserId",
                table: "CasosJuridicosResponsaveis",
                columns: new[] { "CasoJuridicoId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CasosJuridicosResponsaveis_UserId",
                table: "CasosJuridicosResponsaveis",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtendimentosJuridicosAdendos");

            migrationBuilder.DropTable(
                name: "CasosJuridicosAcessosEmergencia");

            migrationBuilder.DropTable(
                name: "CasosJuridicosResponsaveis");

            migrationBuilder.DropTable(
                name: "AtendimentosJuridicos");

            migrationBuilder.DropTable(
                name: "CasosJuridicos");

            migrationBuilder.DropTable(
                name: "Assistidas");
        }
    }
}
