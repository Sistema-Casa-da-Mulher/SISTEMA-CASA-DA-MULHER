using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class ServicoSocial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cress",
                table: "FuncionariosConvites",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cress",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProntuariosSociais",
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
                    VulnerabilidadesAtuais = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProntuariosSociais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProntuariosSociais_Assistidas_AssistidaId",
                        column: x => x.AssistidaId,
                        principalTable: "Assistidas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AtendimentosSociais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    RealizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProfissionalUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ProfissionalIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ProfissionalNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProfissionalCress = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TipoOutro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Relato = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    Providencias = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtendimentosSociais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtendimentosSociais_ProntuariosSociais_ProntuarioSocialId",
                        column: x => x.ProntuarioSocialId,
                        principalTable: "ProntuariosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncaminhamentosRede",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    Destino = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DestinoOutro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ObservacaoStatus = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StatusAtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StatusAtualizadoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ProfissionalUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ProfissionalIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ProfissionalNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncaminhamentosRede", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncaminhamentosRede_ProntuariosSociais_ProntuarioSocialId",
                        column: x => x.ProntuarioSocialId,
                        principalTable: "ProntuariosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EstudosSociais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    Versao = table.Column<int>(type: "INTEGER", nullable: false),
                    PessoasNoDomicilio = table.Column<int>(type: "INTEGER", nullable: true),
                    ComposicaoFamiliar = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DependentesJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    FaixaRenda = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    SituacaoTrabalho = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Moradia = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    ConviveComAgressor = table.Column<bool>(type: "INTEGER", nullable: true),
                    Escolaridade = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    CadUnico = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Nis = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    Beneficios = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BeneficioOutro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    RedeApoio = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Vulnerabilidades = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Observacoes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ProfissionalUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ProfissionalIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ProfissionalNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstudosSociais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstudosSociais_ProntuariosSociais_ProntuarioSocialId",
                        column: x => x.ProntuarioSocialId,
                        principalTable: "ProntuariosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetasAcompanhamentoSocial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Prazo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ConcluidaEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CriadaPorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CriadaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CriadaPorNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    AtualizadaPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasAcompanhamentoSocial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetasAcompanhamentoSocial_ProntuariosSociais_ProntuarioSocialId",
                        column: x => x.ProntuarioSocialId,
                        principalTable: "ProntuariosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProntuariosSociaisAcessosEmergencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Identificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProntuariosSociaisAcessosEmergencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProntuariosSociaisAcessosEmergencia_ProntuariosSociais_ProntuarioSocialId",
                        column: x => x.ProntuarioSocialId,
                        principalTable: "ProntuariosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProntuariosSociaisResponsaveis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Identificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Cress = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    AtribuidoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AtribuidoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProntuariosSociaisResponsaveis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProntuariosSociaisResponsaveis_ProntuariosSociais_ProntuarioSocialId",
                        column: x => x.ProntuarioSocialId,
                        principalTable: "ProntuariosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnexosSociais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProntuarioSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtendimentoSocialId = table.Column<int>(type: "INTEGER", nullable: true),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NomeArquivo = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Visibilidade = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AutorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AutorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AutorNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Excluido = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcluidoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExcluidoPorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    MotivoExclusao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnexosSociais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnexosSociais_AtendimentosSociais_AtendimentoSocialId",
                        column: x => x.AtendimentoSocialId,
                        principalTable: "AtendimentosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnexosSociais_ProntuariosSociais_ProntuarioSocialId",
                        column: x => x.ProntuarioSocialId,
                        principalTable: "ProntuariosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AtendimentosSociaisAdendos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtendimentoSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    Texto = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AutorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AutorIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AutorNome = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtendimentosSociaisAdendos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtendimentosSociaisAdendos_AtendimentosSociais_AtendimentoSocialId",
                        column: x => x.AtendimentoSocialId,
                        principalTable: "AtendimentosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnexosSociaisConteudos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnexoSocialId = table.Column<int>(type: "INTEGER", nullable: false),
                    Dados = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnexosSociaisConteudos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnexosSociaisConteudos_AnexosSociais_AnexoSocialId",
                        column: x => x.AnexoSocialId,
                        principalTable: "AnexosSociais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assistidas_EncaminhadaAssistenciaSocialEm",
                table: "Assistidas",
                column: "EncaminhadaAssistenciaSocialEm");

            migrationBuilder.CreateIndex(
                name: "IX_AnexosSociais_AtendimentoSocialId",
                table: "AnexosSociais",
                column: "AtendimentoSocialId");

            migrationBuilder.CreateIndex(
                name: "IX_AnexosSociais_ProntuarioSocialId",
                table: "AnexosSociais",
                column: "ProntuarioSocialId");

            migrationBuilder.CreateIndex(
                name: "IX_AnexosSociaisConteudos_AnexoSocialId",
                table: "AnexosSociaisConteudos",
                column: "AnexoSocialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosSociais_ProntuarioSocialId_Numero",
                table: "AtendimentosSociais",
                columns: new[] { "ProntuarioSocialId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosSociais_RealizadoEm",
                table: "AtendimentosSociais",
                column: "RealizadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosSociaisAdendos_AtendimentoSocialId",
                table: "AtendimentosSociaisAdendos",
                column: "AtendimentoSocialId");

            migrationBuilder.CreateIndex(
                name: "IX_EncaminhamentosRede_Data",
                table: "EncaminhamentosRede",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_EncaminhamentosRede_ProntuarioSocialId",
                table: "EncaminhamentosRede",
                column: "ProntuarioSocialId");

            migrationBuilder.CreateIndex(
                name: "IX_EstudosSociais_ProntuarioSocialId_Versao",
                table: "EstudosSociais",
                columns: new[] { "ProntuarioSocialId", "Versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetasAcompanhamentoSocial_ProntuarioSocialId",
                table: "MetasAcompanhamentoSocial",
                column: "ProntuarioSocialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosSociais_Ano_Sequencial",
                table: "ProntuariosSociais",
                columns: new[] { "Ano", "Sequencial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosSociais_AssistidaId",
                table: "ProntuariosSociais",
                column: "AssistidaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosSociais_Numero",
                table: "ProntuariosSociais",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosSociaisAcessosEmergencia_ProntuarioSocialId_UserId",
                table: "ProntuariosSociaisAcessosEmergencia",
                columns: new[] { "ProntuarioSocialId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosSociaisResponsaveis_ProntuarioSocialId_UserId",
                table: "ProntuariosSociaisResponsaveis",
                columns: new[] { "ProntuarioSocialId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProntuariosSociaisResponsaveis_UserId",
                table: "ProntuariosSociaisResponsaveis",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnexosSociaisConteudos");

            migrationBuilder.DropTable(
                name: "AtendimentosSociaisAdendos");

            migrationBuilder.DropTable(
                name: "EncaminhamentosRede");

            migrationBuilder.DropTable(
                name: "EstudosSociais");

            migrationBuilder.DropTable(
                name: "MetasAcompanhamentoSocial");

            migrationBuilder.DropTable(
                name: "ProntuariosSociaisAcessosEmergencia");

            migrationBuilder.DropTable(
                name: "ProntuariosSociaisResponsaveis");

            migrationBuilder.DropTable(
                name: "AnexosSociais");

            migrationBuilder.DropTable(
                name: "AtendimentosSociais");

            migrationBuilder.DropTable(
                name: "ProntuariosSociais");

            migrationBuilder.DropIndex(
                name: "IX_Assistidas_EncaminhadaAssistenciaSocialEm",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "Cress",
                table: "FuncionariosConvites");

            migrationBuilder.DropColumn(
                name: "Cress",
                table: "AspNetUsers");
        }
    }
}
