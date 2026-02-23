using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RH_DO.Migrations
{
    /// <inheritdoc />
    public partial class solicitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comentarios",
                table: "SolicitudesEdicion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DatosAnteriores",
                table: "SolicitudesEdicion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaProcesamiento",
                table: "SolicitudesEdicion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioProceso",
                table: "SolicitudesEdicion",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comentarios",
                table: "SolicitudesEdicion");

            migrationBuilder.DropColumn(
                name: "DatosAnteriores",
                table: "SolicitudesEdicion");

            migrationBuilder.DropColumn(
                name: "FechaProcesamiento",
                table: "SolicitudesEdicion");

            migrationBuilder.DropColumn(
                name: "UsuarioProceso",
                table: "SolicitudesEdicion");
        }
    }
}
