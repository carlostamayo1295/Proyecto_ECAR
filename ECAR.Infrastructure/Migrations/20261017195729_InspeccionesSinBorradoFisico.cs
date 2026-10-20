using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InspeccionesSinBorradoFisico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evidencias_Inspecciones_IdInspeccion",
                table: "Evidencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspecciones_Equipos_IdEquipo",
                table: "Inspecciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspecciones_Usuarios_IdUsuario",
                table: "Inspecciones");

            migrationBuilder.DropForeignKey(
                name: "FK_PreguntasChecklist_Checklists_IdChecklist",
                table: "PreguntasChecklist");

            migrationBuilder.DropForeignKey(
                name: "FK_RespuestasInspeccion_Inspecciones_IdInspeccion",
                table: "RespuestasInspeccion");

            migrationBuilder.DropForeignKey(
                name: "FK_RespuestasInspeccion_PreguntasChecklist_IdPregunta",
                table: "RespuestasInspeccion");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidencias_Inspecciones_IdInspeccion",
                table: "Evidencias",
                column: "IdInspeccion",
                principalTable: "Inspecciones",
                principalColumn: "IdInspeccion",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecciones_Equipos_IdEquipo",
                table: "Inspecciones",
                column: "IdEquipo",
                principalTable: "Equipos",
                principalColumn: "IdEquipo",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecciones_Usuarios_IdUsuario",
                table: "Inspecciones",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PreguntasChecklist_Checklists_IdChecklist",
                table: "PreguntasChecklist",
                column: "IdChecklist",
                principalTable: "Checklists",
                principalColumn: "IdChecklist",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RespuestasInspeccion_Inspecciones_IdInspeccion",
                table: "RespuestasInspeccion",
                column: "IdInspeccion",
                principalTable: "Inspecciones",
                principalColumn: "IdInspeccion",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RespuestasInspeccion_PreguntasChecklist_IdPregunta",
                table: "RespuestasInspeccion",
                column: "IdPregunta",
                principalTable: "PreguntasChecklist",
                principalColumn: "IdPregunta",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evidencias_Inspecciones_IdInspeccion",
                table: "Evidencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspecciones_Equipos_IdEquipo",
                table: "Inspecciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspecciones_Usuarios_IdUsuario",
                table: "Inspecciones");

            migrationBuilder.DropForeignKey(
                name: "FK_PreguntasChecklist_Checklists_IdChecklist",
                table: "PreguntasChecklist");

            migrationBuilder.DropForeignKey(
                name: "FK_RespuestasInspeccion_Inspecciones_IdInspeccion",
                table: "RespuestasInspeccion");

            migrationBuilder.DropForeignKey(
                name: "FK_RespuestasInspeccion_PreguntasChecklist_IdPregunta",
                table: "RespuestasInspeccion");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidencias_Inspecciones_IdInspeccion",
                table: "Evidencias",
                column: "IdInspeccion",
                principalTable: "Inspecciones",
                principalColumn: "IdInspeccion",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecciones_Equipos_IdEquipo",
                table: "Inspecciones",
                column: "IdEquipo",
                principalTable: "Equipos",
                principalColumn: "IdEquipo",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecciones_Usuarios_IdUsuario",
                table: "Inspecciones",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PreguntasChecklist_Checklists_IdChecklist",
                table: "PreguntasChecklist",
                column: "IdChecklist",
                principalTable: "Checklists",
                principalColumn: "IdChecklist",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RespuestasInspeccion_Inspecciones_IdInspeccion",
                table: "RespuestasInspeccion",
                column: "IdInspeccion",
                principalTable: "Inspecciones",
                principalColumn: "IdInspeccion",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RespuestasInspeccion_PreguntasChecklist_IdPregunta",
                table: "RespuestasInspeccion",
                column: "IdPregunta",
                principalTable: "PreguntasChecklist",
                principalColumn: "IdPregunta",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
