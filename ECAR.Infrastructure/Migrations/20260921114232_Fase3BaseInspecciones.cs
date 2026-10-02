using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase3BaseInspecciones : Migration
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

            migrationBuilder.RenameColumn(
                name: "UsuarioCarga",
                table: "Evidencias",
                newName: "UsuarioCargaAnterior");

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
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdUsuarioCarga",
                table: "Evidencias",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreOriginal",
                table: "Evidencias",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoContenido",
                table: "Evidencias",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "application/octet-stream");

            migrationBuilder.AddColumn<long>(
                name: "TamanoBytes",
                table: "Evidencias",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Inspecciones]) AND NOT EXISTS (SELECT 1 FROM [Checklists])
                BEGIN
                    INSERT INTO [Checklists] ([Nombre], [Version], [Activo], [FechaCreacion])
                    VALUES (N'Checklist migrado', N'1.0', 1, SYSUTCDATETIME());
                END;

                UPDATE i
                SET [IdChecklist] = COALESCE(
                    i.[ChecklistIdChecklist],
                    (SELECT TOP (1) c.[IdChecklist]
                     FROM [Checklists] c
                     ORDER BY c.[Activo] DESC, c.[IdChecklist]))
                FROM [Inspecciones] i;

                UPDATE e
                SET [IdUsuarioCarga] = i.[IdUsuario],
                    [NombreOriginal] = LEFT(e.[Archivo], 200)
                FROM [Evidencias] e
                INNER JOIN [Inspecciones] i ON i.[IdInspeccion] = e.[IdInspeccion];

                UPDATE [Inspecciones]
                SET [Estado] = N'Cerrada',
                    [FechaCierre] = COALESCE([FechaCierre], [FechaInspeccion])
                WHERE [FirmaDigital] IS NOT NULL
                  AND LEN(LTRIM(RTRIM([FirmaDigital]))) > 0;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "IdChecklist",
                table: "Inspecciones",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "IdUsuarioCarga",
                table: "Evidencias",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "ChecklistIdChecklist",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "UsuarioCargaAnterior",
                table: "Evidencias");

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

            migrationBuilder.AddColumn<long>(
                name: "ChecklistIdChecklist",
                table: "Inspecciones",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioCarga",
                table: "Evidencias",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE i
                SET [ChecklistIdChecklist] = i.[IdChecklist]
                FROM [Inspecciones] i;

                UPDATE e
                SET [UsuarioCarga] = u.[Nombre]
                FROM [Evidencias] e
                INNER JOIN [Usuarios] u ON u.[IdUsuario] = e.[IdUsuarioCarga];
                """);

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

            migrationBuilder.DropColumn(
                name: "TipoContenido",
                table: "Evidencias");

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
