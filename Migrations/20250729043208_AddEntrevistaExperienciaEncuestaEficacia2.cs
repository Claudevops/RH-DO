using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RH_DO.Migrations
{
    /// <inheritdoc />
    public partial class AddEntrevistaExperienciaEncuestaEficacia2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Encuesta_Eficacia",
                table: "Empleados",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Entrevista_Experiencia",
                table: "Empleados",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Encuesta_Eficacia",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "Entrevista_Experiencia",
                table: "Empleados");
        }
    }
}
