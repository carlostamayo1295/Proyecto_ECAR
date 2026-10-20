using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <summary>
    /// La tabla Novedades reemplaza a Hallazgos: una novedad por cada respuesta "No", resuelta con
    /// firma electrónica. Hallazgos se borra sin pasar sus filas (decisión del 19/10/2026: la base es
    /// de prueba); las inspecciones ya firmadas conservan su estado, su resultado y su sello.
    /// </summary>
    public partial class NovedadesReemplazanHallazgos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hallazgos");

            migrationBuilder.CreateTable(
                name: "Novedades",
                columns: table => new
                {
                    IdNovedad = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInspeccion = table.Column<long>(type: "bigint", nullable: false),
                    IdPregunta = table.Column<long>(type: "bigint", nullable: false),
                    IdRespuesta = table.Column<long>(type: "bigint", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Criticidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdUsuarioRegistro = table.Column<long>(type: "bigint", nullable: false),
                    Solucion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUsuarioResolucion = table.Column<long>(type: "bigint", nullable: true),
                    FirmaDigital = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirmaNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FirmaSignificado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FirmaAlgoritmo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FirmaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdUsuarioAnulacion = table.Column<long>(type: "bigint", nullable: true),
                    FechaNotificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Novedades", x => x.IdNovedad);
                    table.ForeignKey(
                        name: "FK_Novedades_Inspecciones_IdInspeccion",
                        column: x => x.IdInspeccion,
                        principalTable: "Inspecciones",
                        principalColumn: "IdInspeccion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedades_PreguntasChecklist_IdPregunta",
                        column: x => x.IdPregunta,
                        principalTable: "PreguntasChecklist",
                        principalColumn: "IdPregunta",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedades_RespuestasInspeccion_IdRespuesta",
                        column: x => x.IdRespuesta,
                        principalTable: "RespuestasInspeccion",
                        principalColumn: "IdRespuesta",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedades_Usuarios_IdUsuarioAnulacion",
                        column: x => x.IdUsuarioAnulacion,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedades_Usuarios_IdUsuarioRegistro",
                        column: x => x.IdUsuarioRegistro,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedades_Usuarios_IdUsuarioResolucion",
                        column: x => x.IdUsuarioResolucion,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_Criticidad",
                table: "Novedades",
                column: "Criticidad");

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_Estado",
                table: "Novedades",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_FechaRegistro",
                table: "Novedades",
                column: "FechaRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_IdInspeccion_IdPregunta",
                table: "Novedades",
                columns: new[] { "IdInspeccion", "IdPregunta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_IdPregunta",
                table: "Novedades",
                column: "IdPregunta");

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_IdRespuesta",
                table: "Novedades",
                column: "IdRespuesta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_IdUsuarioAnulacion",
                table: "Novedades",
                column: "IdUsuarioAnulacion");

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_IdUsuarioRegistro",
                table: "Novedades",
                column: "IdUsuarioRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_Novedades_IdUsuarioResolucion",
                table: "Novedades",
                column: "IdUsuarioResolucion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Novedades");

            migrationBuilder.CreateTable(
                name: "Hallazgos",
                columns: table => new
                {
                    IdHallazgo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInspeccion = table.Column<long>(type: "bigint", nullable: false),
                    IdPregunta = table.Column<long>(type: "bigint", nullable: true),
                    IdRespuesta = table.Column<long>(type: "bigint", nullable: true),
                    IdUsuarioCierre = table.Column<long>(type: "bigint", nullable: true),
                    IdUsuarioRegistro = table.Column<long>(type: "bigint", nullable: false),
                    IdUsuarioResponsable = table.Column<long>(type: "bigint", nullable: true),
                    AccionCorrectiva = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Criticidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Abierto"),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCompromiso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hallazgos", x => x.IdHallazgo);
                    table.ForeignKey(
                        name: "FK_Hallazgos_Inspecciones_IdInspeccion",
                        column: x => x.IdInspeccion,
                        principalTable: "Inspecciones",
                        principalColumn: "IdInspeccion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hallazgos_PreguntasChecklist_IdPregunta",
                        column: x => x.IdPregunta,
                        principalTable: "PreguntasChecklist",
                        principalColumn: "IdPregunta",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hallazgos_RespuestasInspeccion_IdRespuesta",
                        column: x => x.IdRespuesta,
                        principalTable: "RespuestasInspeccion",
                        principalColumn: "IdRespuesta",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hallazgos_Usuarios_IdUsuarioCierre",
                        column: x => x.IdUsuarioCierre,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hallazgos_Usuarios_IdUsuarioRegistro",
                        column: x => x.IdUsuarioRegistro,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hallazgos_Usuarios_IdUsuarioResponsable",
                        column: x => x.IdUsuarioResponsable,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_Criticidad",
                table: "Hallazgos",
                column: "Criticidad");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_Estado",
                table: "Hallazgos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_FechaRegistro",
                table: "Hallazgos",
                column: "FechaRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdInspeccion",
                table: "Hallazgos",
                column: "IdInspeccion");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdInspeccion_IdPregunta",
                table: "Hallazgos",
                columns: new[] { "IdInspeccion", "IdPregunta" },
                unique: true,
                filter: "[IdPregunta] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdPregunta",
                table: "Hallazgos",
                column: "IdPregunta");

            migrationBuilder.CreateIndex(
                name: "IX_Hallazgos_IdRespuesta",
                table: "Hallazgos",
                column: "IdRespuesta");

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
        }
    }
}
