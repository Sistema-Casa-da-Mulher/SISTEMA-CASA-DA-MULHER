using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaMulher.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgendaFuncionamentoDaCasa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Funcionamento",
                table: "AgendaDiasEspeciais",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HorarioFim",
                table: "AgendaDiasEspeciais",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HorarioInicio",
                table: "AgendaDiasEspeciais",
                type: "TEXT",
                nullable: true);

            // "Unidade fechada" deixa de ser um tipo de data: vira data comum com a Casa fechada.
            migrationBuilder.Sql("UPDATE \"AgendaDiasEspeciais\" SET \"Funcionamento\" = 'fechada', \"Tipo\" = 'outra' WHERE \"Tipo\" = 'fechada';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Funcionamento",
                table: "AgendaDiasEspeciais");

            migrationBuilder.DropColumn(
                name: "HorarioFim",
                table: "AgendaDiasEspeciais");

            migrationBuilder.DropColumn(
                name: "HorarioInicio",
                table: "AgendaDiasEspeciais");
        }
    }
}
