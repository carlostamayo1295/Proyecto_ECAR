using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTokenQrEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes de esta migración QRCode era texto libre del formulario de equipo, nunca un token
            // válido. Se limpia para poder acortar la columna e indexarla.
            migrationBuilder.Sql("UPDATE [Equipos] SET [QRCode] = NULL");

            // Las preguntas creadas antes de la columna Orden quedaron en 0: se numeran por
            // checklist según su id para que el orden sea único y el servidor pueda continuar en max + 1.
            migrationBuilder.Sql(@"
                ;WITH numeradas AS (
                    SELECT [IdPregunta], ROW_NUMBER() OVER (PARTITION BY [IdChecklist] ORDER BY [IdPregunta]) AS Nuevo
                    FROM [PreguntasChecklist]
                )
                UPDATE p SET p.[Orden] = n.Nuevo
                FROM [PreguntasChecklist] p
                INNER JOIN numeradas n ON n.[IdPregunta] = p.[IdPregunta]
                WHERE p.[Orden] = 0");

            migrationBuilder.AlterColumn<string>(
                name: "QRCode",
                table: "Equipos",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_QRCode",
                table: "Equipos",
                column: "QRCode",
                unique: true,
                filter: "[QRCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Equipos_QRCode",
                table: "Equipos");

            migrationBuilder.AlterColumn<string>(
                name: "QRCode",
                table: "Equipos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
