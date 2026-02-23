using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RH_DO.Migrations
{
    /// <inheritdoc />
    public partial class PRECORREOS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Adjuntos",
                table: "Adjuntos");

            migrationBuilder.RenameTable(
                name: "Adjuntos",
                newName: "AdjuntosCorreos");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Ubicaciones",
                newName: "Id_UBI");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Sociedades",
                newName: "Id_SOC");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Gerencias",
                newName: "Id_GER");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Analistas",
                newName: "Id_ANA");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "AdjuntosCorreos",
                newName: "Nombre_Adjunto");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AdjuntosCorreos",
                newName: "Id_AC");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdjuntosCorreos",
                table: "AdjuntosCorreos",
                column: "Id_AC");

            migrationBuilder.CreateTable(
                name: "DestinatariosCorreo_PRE",
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
                    table.PrimaryKey("PK_DestinatariosCorreo_PRE", x => x.Id_DCP);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DestinatariosCorreo_PRE");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdjuntosCorreos",
                table: "AdjuntosCorreos");

            migrationBuilder.RenameTable(
                name: "AdjuntosCorreos",
                newName: "Adjuntos");

            migrationBuilder.RenameColumn(
                name: "Id_UBI",
                table: "Ubicaciones",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "Id_SOC",
                table: "Sociedades",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "Id_GER",
                table: "Gerencias",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "Id_ANA",
                table: "Analistas",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "Nombre_Adjunto",
                table: "Adjuntos",
                newName: "Nombre");

            migrationBuilder.RenameColumn(
                name: "Id_AC",
                table: "Adjuntos",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Adjuntos",
                table: "Adjuntos",
                column: "Id");
        }
    }
}
