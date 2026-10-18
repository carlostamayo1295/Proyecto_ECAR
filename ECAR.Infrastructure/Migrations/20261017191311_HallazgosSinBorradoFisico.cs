using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HallazgosSinBorradoFisico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hallazgos_Inspecciones_IdInspeccion",
                table: "Hallazgos");

            migrationBuilder.AddColumn<long>(
                name: "IdRespuesta",
                table: "Hallazgos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdInspeccion_IdPregunta",
                table: "Hallazgos",
                columns: new[] { "IdInspeccion", "IdPregunta" },
                unique: true,
                filter: "[IdPregunta] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdRespuesta",
                table: "Hallazgos",
                column: "IdRespuesta");

            migrationBuilder.AddForeignKey(
                name: "FK_Hallazgos_Inspecciones_IdInspeccion",
                table: "Hallazgos",
                column: "IdInspeccion",
                principalTable: "Inspecciones",
                principalColumn: "IdInspeccion",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hallazgos_RespuestasInspeccion_IdRespuesta",
                table: "Hallazgos",
                column: "IdRespuesta",
                principalTable: "RespuestasInspeccion",
                principalColumn: "IdRespuesta",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hallazgos_Inspecciones_IdInspeccion",
                table: "Hallazgos");

            migrationBuilder.DropForeignKey(
                name: "FK_Hallazgos_RespuestasInspeccion_IdRespuesta",
                table: "Hallazgos");

            migrationBuilder.DropIndex(
                name: "IX_Hallazgos_IdInspeccion_IdPregunta",
                table: "Hallazgos");

            migrationBuilder.DropIndex(
                name: "IX_Hallazgos_IdRespuesta",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "IdRespuesta",
                table: "Hallazgos");

            migrationBuilder.AddForeignKey(
                name: "FK_Hallazgos_Inspecciones_IdInspeccion",
                table: "Hallazgos",
                column: "IdInspeccion",
                principalTable: "Inspecciones",
                principalColumn: "IdInspeccion",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
