using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <summary>
    /// Modelo de datos de la Fase 4 (PLAN_FASE4_TAREAS §3.1): auditoría con usuario, motivo, IP,
    /// agente y cadena de hashes; ciclo de vida de los hallazgos; anulación de inspecciones;
    /// evidencias retiradas; seguridad de cuentas; frecuencia de inspección. Además convierte los
    /// datos existentes y crea el trigger que impide modificar o borrar la auditoría.
    /// </summary>
    public partial class Fase4HallazgosAuditoria : Migration
    {
        private const string Trigger = "TR_Auditoria_SoloInsercion";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BloqueadoHasta",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DebeCambiarPassword",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCambioPassword",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntentosFallidos",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Usuarios",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Un sello distinto por usuario, como el que pone Usuario.NuevoSecurityStamp().
            migrationBuilder.Sql(
                "UPDATE [Usuarios] SET [SecurityStamp] = LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''));");

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoAcceso",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAnulacion",
                table: "Inspecciones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaAlgoritmo",
                table: "Inspecciones",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaNombre",
                table: "Inspecciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmaSignificado",
                table: "Inspecciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdUsuarioAnulacion",
                table: "Inspecciones",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Inspecciones",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Los hallazgos sin estado pasan a Abierto (§3.1).
            migrationBuilder.Sql("UPDATE [Hallazgos] SET [Estado] = N'Abierto' WHERE [Estado] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Hallazgos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Abierto",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccionCorrectiva",
                table: "Hallazgos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCierre",
                table: "Hallazgos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCompromiso",
                table: "Hallazgos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdPregunta",
                table: "Hallazgos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdUsuarioCierre",
                table: "Hallazgos",
                type: "bigint",
                nullable: true);

            // Se crea admitiendo nulos para rellenarla con el inspector de cada hallazgo existente,
            // y después se vuelve obligatoria.
            migrationBuilder.AddColumn<long>(
                name: "IdUsuarioRegistro",
                table: "Hallazgos",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE h SET h.[IdUsuarioRegistro] = i.[IdUsuario] " +
                "FROM [Hallazgos] h INNER JOIN [Inspecciones] i ON i.[IdInspeccion] = h.[IdInspeccion];");

            migrationBuilder.AlterColumn<long>(
                name: "IdUsuarioRegistro",
                table: "Hallazgos",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdUsuarioResponsable",
                table: "Hallazgos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Hallazgos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "Hallazgos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRetiro",
                table: "Evidencias",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdUsuarioRetiro",
                table: "Evidencias",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRetiro",
                table: "Evidencias",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Retirada",
                table: "Evidencias",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FrecuenciaInspeccion",
                table: "Equipos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgenteUsuario",
                table: "Auditoria",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionIp",
                table: "Auditoria",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            // Se crea admitiendo nulos para sellar las filas existentes y después se vuelve obligatoria.
            migrationBuilder.AddColumn<string>(
                name: "Hash",
                table: "Auditoria",
                type: "char(64)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashAnterior",
                table: "Auditoria",
                type: "char(64)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdUsuario",
                table: "Auditoria",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "Auditoria",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Sella las filas anteriores a la Fase 4 en orden de IdAuditoria con la fórmula de
            // CadenaAuditoria: SHA-256 en UTF-16LE (HASHBYTES sobre nvarchar) de
            // HashAnterior|Tabla|RegistroId|Accion|ValorAnterior|ValorNuevo|IdUsuario|FechaHora|Motivo.
            // Así la verificación recorre la tabla entera con una sola regla.
            migrationBuilder.Sql(@"
DECLARE @id bigint, @anterior char(64) = NULL, @hash char(64);
DECLARE filas CURSOR LOCAL FAST_FORWARD FOR SELECT [IdAuditoria] FROM [Auditoria] ORDER BY [IdAuditoria];
OPEN filas;
FETCH NEXT FROM filas INTO @id;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @hash = CONVERT(char(64), HASHBYTES('SHA2_256', CONCAT(
            ISNULL(CONVERT(nvarchar(64), @anterior), N''), N'|',
            [Tabla], N'|',
            CONVERT(nvarchar(20), [RegistroId]), N'|',
            [Accion], N'|',
            ISNULL([ValorAnterior], N''), N'|',
            ISNULL([ValorNuevo], N''), N'|',
            ISNULL(CONVERT(nvarchar(20), [IdUsuario]), N''), N'|',
            FORMAT([FechaHora], N'yyyy-MM-dd''T''HH'':''mm'':''ss''.''fffffff', 'en-US'), N'|',
            ISNULL([Motivo], N''))), 2)
    FROM [Auditoria] WHERE [IdAuditoria] = @id;

    UPDATE [Auditoria] SET [HashAnterior] = @anterior, [Hash] = @hash WHERE [IdAuditoria] = @id;
    SET @anterior = @hash;
    FETCH NEXT FROM filas INTO @id;
END
CLOSE filas;
DEALLOCATE filas;");

            migrationBuilder.AlterColumn<string>(
                name: "Hash",
                table: "Auditoria",
                type: "char(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "HistorialPasswords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<long>(type: "bigint", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialPasswords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialPasswords_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_IdUsuarioAnulacion",
                table: "Inspecciones",
                column: "IdUsuarioAnulacion");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdPregunta",
                table: "Hallazgos",
                column: "IdPregunta");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdUsuarioCierre",
                table: "Hallazgos",
                column: "IdUsuarioCierre");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdUsuarioRegistro",
                table: "Hallazgos",
                column: "IdUsuarioRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdUsuarioResponsable",
                table: "Hallazgos",
                column: "IdUsuarioResponsable");

            migrationBuilder.CreateIndex(
                name: "IX_Evidencias_IdUsuarioRetiro",
                table: "Evidencias",
                column: "IdUsuarioRetiro");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_IdUsuario",
                table: "Auditoria",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPasswords_IdUsuario_Fecha",
                table: "HistorialPasswords",
                columns: new[] { "IdUsuario", "Fecha" });

            migrationBuilder.AddForeignKey(
                name: "FK_Auditoria_Usuarios_IdUsuario",
                table: "Auditoria",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Evidencias_Usuarios_IdUsuarioRetiro",
                table: "Evidencias",
                column: "IdUsuarioRetiro",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hallazgos_PreguntasChecklist_IdPregunta",
                table: "Hallazgos",
                column: "IdPregunta",
                principalTable: "PreguntasChecklist",
                principalColumn: "IdPregunta",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hallazgos_Usuarios_IdUsuarioCierre",
                table: "Hallazgos",
                column: "IdUsuarioCierre",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hallazgos_Usuarios_IdUsuarioRegistro",
                table: "Hallazgos",
                column: "IdUsuarioRegistro",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hallazgos_Usuarios_IdUsuarioResponsable",
                table: "Hallazgos",
                column: "IdUsuarioResponsable",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecciones_Usuarios_IdUsuarioAnulacion",
                table: "Inspecciones",
                column: "IdUsuarioAnulacion",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            // Parte 11 §11.10(e): las filas de auditoría no se pueden modificar ni borrar, tampoco
            // por SQL directo. Va al final: el sellado de arriba necesita hacer UPDATE.
            migrationBuilder.Sql($@"
CREATE TRIGGER [dbo].[{Trigger}] ON [dbo].[Auditoria]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N'La auditoría solo admite inserciones: sus filas no se pueden modificar ni borrar.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS [dbo].[{Trigger}];");

            migrationBuilder.DropForeignKey(
                name: "FK_Auditoria_Usuarios_IdUsuario",
                table: "Auditoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Evidencias_Usuarios_IdUsuarioRetiro",
                table: "Evidencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Hallazgos_PreguntasChecklist_IdPregunta",
                table: "Hallazgos");

            migrationBuilder.DropForeignKey(
                name: "FK_Hallazgos_Usuarios_IdUsuarioCierre",
                table: "Hallazgos");

            migrationBuilder.DropForeignKey(
                name: "FK_Hallazgos_Usuarios_IdUsuarioRegistro",
                table: "Hallazgos");

            migrationBuilder.DropForeignKey(
                name: "FK_Hallazgos_Usuarios_IdUsuarioResponsable",
                table: "Hallazgos");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspecciones_Usuarios_IdUsuarioAnulacion",
                table: "Inspecciones");

            migrationBuilder.DropTable(
                name: "HistorialPasswords");

            migrationBuilder.DropIndex(
                name: "IX_Inspecciones_IdUsuarioAnulacion",
                table: "Inspecciones");

            migrationBuilder.DropIndex(
                name: "IX_Hallazgos_IdPregunta",
                table: "Hallazgos");

            migrationBuilder.DropIndex(
                name: "IX_Hallazgos_IdUsuarioCierre",
                table: "Hallazgos");

            migrationBuilder.DropIndex(
                name: "IX_Hallazgos_IdUsuarioRegistro",
                table: "Hallazgos");

            migrationBuilder.DropIndex(
                name: "IX_Hallazgos_IdUsuarioResponsable",
                table: "Hallazgos");

            migrationBuilder.DropIndex(
                name: "IX_Evidencias_IdUsuarioRetiro",
                table: "Evidencias");

            migrationBuilder.DropIndex(
                name: "IX_Auditoria_IdUsuario",
                table: "Auditoria");

            migrationBuilder.DropColumn(
                name: "BloqueadoHasta",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DebeCambiarPassword",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FechaCambioPassword",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "IntentosFallidos",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "UltimoAcceso",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FechaAnulacion",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "FirmaAlgoritmo",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "FirmaNombre",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "FirmaSignificado",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "IdUsuarioAnulacion",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Inspecciones");

            migrationBuilder.DropColumn(
                name: "AccionCorrectiva",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "FechaCierre",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "FechaCompromiso",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "IdPregunta",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "IdUsuarioCierre",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "IdUsuarioRegistro",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "IdUsuarioResponsable",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "Hallazgos");

            migrationBuilder.DropColumn(
                name: "FechaRetiro",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "IdUsuarioRetiro",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "MotivoRetiro",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "Retirada",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "FrecuenciaInspeccion",
                table: "Equipos");

            migrationBuilder.DropColumn(
                name: "AgenteUsuario",
                table: "Auditoria");

            migrationBuilder.DropColumn(
                name: "DireccionIp",
                table: "Auditoria");

            migrationBuilder.DropColumn(
                name: "Hash",
                table: "Auditoria");

            migrationBuilder.DropColumn(
                name: "HashAnterior",
                table: "Auditoria");

            migrationBuilder.DropColumn(
                name: "IdUsuario",
                table: "Auditoria");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "Auditoria");

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Hallazgos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Abierto");
        }
    }
}
