using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class TermoSigiloAceite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TermoSigiloAceites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    IdentificadorFuncionario = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Perfil = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    VersaoTermo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    HashTermo = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NomeAssinado = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AceitoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EnderecoIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermoSigiloAceites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermoSigiloAceites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermoSigiloAceites_AceitoEm",
                table: "TermoSigiloAceites",
                column: "AceitoEm");

            migrationBuilder.CreateIndex(
                name: "IX_TermoSigiloAceites_UserId_VersaoTermo",
                table: "TermoSigiloAceites",
                columns: new[] { "UserId", "VersaoTermo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TermoSigiloAceites");
        }
    }
}
