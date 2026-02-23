using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RH_DO.Migrations
{
    /// <inheritdoc />
    public partial class MailsPost2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdjuntoCorreo_post",
                columns: table => new
                {
                    Id_AC = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre_Adjunto = table.Column<string>(type: "TEXT", nullable: true),
                    ArchivoPath = table.Column<string>(type: "TEXT", nullable: true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaSubida = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjuntoCorreo_post", x => x.Id_AC);
                });

            migrationBuilder.CreateTable(
                name: "DestinatariosCorreo_POST",
                columns: table => new
                {
                    Id_DCP = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo_DCP = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinatariosFijos = table.Column<string>(type: "TEXT", nullable: true),
                    CCFijos = table.Column<string>(type: "TEXT", nullable: true),
                    CCOFijos = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinatariosCorreo_POST", x => x.Id_DCP);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaCorreo_POST",
                columns: table => new
                {
                    Id_PC = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo_PC = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre_PC = table.Column<string>(type: "TEXT", nullable: false),
                    HtmlContenido = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaCorreo_POST", x => x.Id_PC);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjuntoCorreo_post");

            migrationBuilder.DropTable(
                name: "DestinatariosCorreo_POST");

            migrationBuilder.DropTable(
                name: "PlantillaCorreo_POST");
        }
    }
}
