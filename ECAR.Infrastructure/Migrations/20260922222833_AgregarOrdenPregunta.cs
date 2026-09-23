using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrdenPregunta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inspecciones_Checklists_ChecklistIdChecklist",
                table: "Inspecciones");

            migrationBuilder.DropIndex(
                name: "IX_Inspecciones_ChecklistIdChecklist",
                table: "Inspecciones");

            migrationBuilder.DropIndex(
                name: "IX_Evidencias_UsuarioCarga",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "ChecklistIdChecklist",
                table: "Inspecciones");

            migrationBuilder.RenameColumn(
                name: "UsuarioCarga",
                table: "Evidencias",
                newName: "TipoContenido");

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Inspecciones",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "EnCurso");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCierre",
                table: "Inspecciones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaHash",
                table: "Inspecciones",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdChecklist",
                table: "Inspecciones",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "IdUsuarioCarga",
                table: "Evidencias",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "NombreOriginal",
                table: "Evidencias",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "TamanoBytes",
                table: "Evidencias",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_Estado",
                table: "Inspecciones",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_IdChecklist",
                table: "Inspecciones",
                column: "IdChecklist");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_IdUsuario_Estado",
                table: "Inspecciones",
                columns: new[] { "IdUsuario", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Evidencias_IdUsuarioCarga",
                table: "Evidencias",
                column: "IdUsuarioCarga");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidencias_Usuarios_IdUsuarioCarga",
                table: "Evidencias",
                column: "IdUsuarioCarga",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecciones_Checklists_IdChecklist",
                table: "Inspecciones",
                column: "IdChecklist",
                principalTable: "Checklists",
                principalColumn: "IdChecklist",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evidencias_Usuarios_IdUsuarioCarga",
                table: "Evidencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspecciones_Checklists_IdChecklist",
                table: "Inspecciones");

            migrationBuilder.DropIndex(
                name: "IX_Inspecciones_Estado",
                table: "Inspecciones");

            migrationBuilder.DropIndex(
                name: "IX_Inspecciones_IdChecklist",
                table: "Inspecciones");

            migrationBuilder.DropIndex(
                name: "IX_Inspecciones_IdUsuario_Estado",
                table: "Inspecciones");

            migrationBuilder.DropIndex(
                name: "IX_Evidencias_IdUsuarioCarga",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "FechaCierre",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "FirmaHash",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "IdChecklist",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "IdUsuarioCarga",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "NombreOriginal",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "TamanoBytes",
                table: "Evidencias");

            migrationBuilder.RenameColumn(
                name: "TipoContenido",
                table: "Evidencias",
                newName: "UsuarioCarga");

            migrationBuilder.AddColumn<long>(
                name: "ChecklistIdChecklist",
                table: "Inspecciones",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_ChecklistIdChecklist",
                table: "Inspecciones",
                column: "ChecklistIdChecklist");

            migrationBuilder.CreateIndex(
                name: "IX_Evidencias_UsuarioCarga",
                table: "Evidencias",
                column: "UsuarioCarga");

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecciones_Checklists_ChecklistIdChecklist",
                table: "Inspecciones",
                column: "ChecklistIdChecklist",
                principalTable: "Checklists",
                principalColumn: "IdChecklist");
        }
    }
}
