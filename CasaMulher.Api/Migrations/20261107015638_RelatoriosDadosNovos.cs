using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class RelatoriosDadosNovos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Vagas",
                table: "Cursos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoSaida",
                table: "AssistidasCursos",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Situacao",
                table: "AssistidasCursos",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "cursando");

            migrationBuilder.AddColumn<DateTime>(
                name: "SituacaoAlteradaEm",
                table: "AssistidasCursos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SituacaoAlteradaPorIdentificador",
                table: "AssistidasCursos",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TerminoEm",
                table: "AssistidasCursos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JuridicoAgendadoEm",
                table: "Assistidas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JuridicoAgendadoPara",
                table: "Assistidas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JuridicoAgendadoPorIdentificador",
                table: "Assistidas",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JuridicoCanceladoPorIdentificador",
                table: "Assistidas",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JuridicoEncaminhamentoCanceladoEm",
                table: "Assistidas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JuridicoMotivoCancelamento",
                table: "Assistidas",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vagas",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "MotivoSaida",
                table: "AssistidasCursos");

            migrationBuilder.DropColumn(
                name: "Situacao",
                table: "AssistidasCursos");

            migrationBuilder.DropColumn(
                name: "SituacaoAlteradaEm",
                table: "AssistidasCursos");

            migrationBuilder.DropColumn(
                name: "SituacaoAlteradaPorIdentificador",
                table: "AssistidasCursos");

            migrationBuilder.DropColumn(
                name: "TerminoEm",
                table: "AssistidasCursos");

            migrationBuilder.DropColumn(
                name: "JuridicoAgendadoEm",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "JuridicoAgendadoPara",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "JuridicoAgendadoPorIdentificador",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "JuridicoCanceladoPorIdentificador",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "JuridicoEncaminhamentoCanceladoEm",
                table: "Assistidas");

            migrationBuilder.DropColumn(
                name: "JuridicoMotivoCancelamento",
                table: "Assistidas");
        }
    }
}
