using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RH_DO.Migrations
{
    /// <inheritdoc />
    public partial class NotificacionesPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorreosEnviados_POST",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RUT = table.Column<string>(type: "TEXT", nullable: false),
                    TipoCorreo = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmailDestinatario = table.Column<string>(type: "TEXT", nullable: false),
                    Completado = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorreosEnviados_POST", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorreosEnviados_POST");
        }
    }
}
