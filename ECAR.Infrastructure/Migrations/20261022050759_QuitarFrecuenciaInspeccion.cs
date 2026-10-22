using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <summary>
    /// Se quita la frecuencia de inspección de los equipos y, con ella, el indicador y el reporte de
    /// cumplimiento (decisión de ECAR del 21/10/2026). Los valores anteriores siguen en la auditoría
    /// de cada equipo.
    /// </summary>
    public partial class QuitarFrecuenciaInspeccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrecuenciaInspeccion",
                table: "Equipos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FrecuenciaInspeccion",
                table: "Equipos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
