using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PresentaNovedadYSede : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PresentaNovedad",
                table: "RespuestasInspeccion",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sede",
                table: "Inspecciones",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresentaNovedad",
                table: "RespuestasInspeccion");

            migrationBuilder.DropColumn(
                name: "Sede",
                table: "Inspecciones");
        }
    }
}
