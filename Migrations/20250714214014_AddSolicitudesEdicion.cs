using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RH_DO.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitudesEdicion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaModificacion",
                table: "Empleados",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioModifico",
                table: "Empleados",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SolicitudesEdicion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RutEmpleado = table.Column<string>(type: "TEXT", nullable: false),
                    DatosSolicitados = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioSolicitante = table.Column<string>(type: "TEXT", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Procesada = table.Column<bool>(type: "INTEGER", nullable: false),
                    Aprobada = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesEdicion", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesEdicion");

            migrationBuilder.DropColumn(
                name: "UltimaModificacion",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "UsuarioModifico",
                table: "Empleados");
        }
    }
}
