using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    IdAuditoria = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tabla = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RegistroId = table.Column<long>(type: "bigint", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ValorAnterior = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorNuevo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.IdAuditoria);
                });

            migrationBuilder.CreateTable(
                name: "CategoriasEquipo",
                columns: table => new
                {
                    IdCategoria = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasEquipo", x => x.IdCategoria);
                });

            migrationBuilder.CreateTable(
                name: "Checklists",
                columns: table => new
                {
                    IdChecklist = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checklists", x => x.IdChecklist);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    IdRol = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.IdRol);
                });

            migrationBuilder.CreateTable(
                name: "Ubicaciones",
                columns: table => new
                {
                    IdUbicacion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Planta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ubicaciones", x => x.IdUbicacion);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    IdUsuario = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UsuarioAD = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.IdUsuario);
                });

            migrationBuilder.CreateTable(
                name: "PreguntasChecklist",
                columns: table => new
                {
                    IdPregunta = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdChecklist = table.Column<long>(type: "bigint", nullable: false),
                    Pregunta = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoRespuesta = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Obligatoria = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreguntasChecklist", x => x.IdPregunta);
                    table.ForeignKey(
                        name: "FK_PreguntasChecklist_Checklists_IdChecklist",
                        column: x => x.IdChecklist,
                        principalTable: "Checklists",
                        principalColumn: "IdChecklist",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Equipos",
                columns: table => new
                {
                    IdEquipo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoInterno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActivoFijo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SerialFabricante = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NombreEquipo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Fabricante = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Criticidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IdCategoria = table.Column<long>(type: "bigint", nullable: true),
                    IdUbicacion = table.Column<long>(type: "bigint", nullable: true),
                    QRCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipos", x => x.IdEquipo);
                    table.ForeignKey(
                        name: "FK_Equipos_CategoriasEquipo_IdCategoria",
                        column: x => x.IdCategoria,
                        principalTable: "CategoriasEquipo",
                        principalColumn: "IdCategoria");
                    table.ForeignKey(
                        name: "FK_Equipos_Ubicaciones_IdUbicacion",
                        column: x => x.IdUbicacion,
                        principalTable: "Ubicaciones",
                        principalColumn: "IdUbicacion");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioRol",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<long>(type: "bigint", nullable: false),
                    IdRol = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioRol", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Roles_IdRol",
                        column: x => x.IdRol,
                        principalTable: "Roles",
                        principalColumn: "IdRol",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inspecciones",
                columns: table => new
                {
                    IdInspeccion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEquipo = table.Column<long>(type: "bigint", nullable: false),
                    IdUsuario = table.Column<long>(type: "bigint", nullable: false),
                    FechaInspeccion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirmaDigital = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChecklistIdChecklist = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspecciones", x => x.IdInspeccion);
                    table.ForeignKey(
                        name: "FK_Inspecciones_Checklists_ChecklistIdChecklist",
                        column: x => x.ChecklistIdChecklist,
                        principalTable: "Checklists",
                        principalColumn: "IdChecklist");
                    table.ForeignKey(
                        name: "FK_Inspecciones_Equipos_IdEquipo",
                        column: x => x.IdEquipo,
                        principalTable: "Equipos",
                        principalColumn: "IdEquipo",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inspecciones_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evidencias",
                columns: table => new
                {
                    IdEvidencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInspeccion = table.Column<long>(type: "bigint", nullable: false),
                    Archivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioCarga = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidencias", x => x.IdEvidencia);
                    table.ForeignKey(
                        name: "FK_Evidencias_Inspecciones_IdInspeccion",
                        column: x => x.IdInspeccion,
                        principalTable: "Inspecciones",
                        principalColumn: "IdInspeccion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hallazgos",
                columns: table => new
                {
                    IdHallazgo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInspeccion = table.Column<long>(type: "bigint", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Criticidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hallazgos", x => x.IdHallazgo);
                    table.ForeignKey(
                        name: "FK_Hallazgos_Inspecciones_IdInspeccion",
                        column: x => x.IdInspeccion,
                        principalTable: "Inspecciones",
                        principalColumn: "IdInspeccion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RespuestasInspeccion",
                columns: table => new
                {
                    IdRespuesta = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdInspeccion = table.Column<long>(type: "bigint", nullable: false),
                    IdPregunta = table.Column<long>(type: "bigint", nullable: false),
                    Respuesta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestasInspeccion", x => x.IdRespuesta);
                    table.ForeignKey(
                        name: "FK_RespuestasInspeccion_Inspecciones_IdInspeccion",
                        column: x => x.IdInspeccion,
                        principalTable: "Inspecciones",
                        principalColumn: "IdInspeccion",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RespuestasInspeccion_PreguntasChecklist_IdPregunta",
                        column: x => x.IdPregunta,
                        principalTable: "PreguntasChecklist",
                        principalColumn: "IdPregunta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Accion",
                table: "Auditoria",
                column: "Accion");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_FechaHora",
                table: "Auditoria",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_RegistroId",
                table: "Auditoria",
                column: "RegistroId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Tabla",
                table: "Auditoria",
                column: "Tabla");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Tabla_RegistroId_FechaHora",
                table: "Auditoria",
                columns: new[] { "Tabla", "RegistroId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Usuario",
                table: "Auditoria",
                column: "Usuario");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasEquipo_Nombre",
                table: "CategoriasEquipo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_Activo",
                table: "Checklists",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_Nombre_Version",
                table: "Checklists",
                columns: new[] { "Nombre", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_ActivoFijo",
                table: "Equipos",
                column: "ActivoFijo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_CodigoInterno",
                table: "Equipos",
                column: "CodigoInterno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_Criticidad",
                table: "Equipos",
                column: "Criticidad");

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_IdCategoria",
                table: "Equipos",
                column: "IdCategoria");

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_IdUbicacion",
                table: "Equipos",
                column: "IdUbicacion");

            migrationBuilder.CreateIndex(
                name: "IX_Evidencias_FechaCarga",
                table: "Evidencias",
                column: "FechaCarga");

            migrationBuilder.CreateIndex(
                name: "IX_Evidencias_IdInspeccion",
                table: "Evidencias",
                column: "IdInspeccion");

            migrationBuilder.CreateIndex(
                name: "IX_Evidencias_UsuarioCarga",
                table: "Evidencias",
                column: "UsuarioCarga");

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
                name: "IX_Inspecciones_ChecklistIdChecklist",
                table: "Inspecciones",
                column: "ChecklistIdChecklist");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_FechaInspeccion",
                table: "Inspecciones",
                column: "FechaInspeccion");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_IdEquipo",
                table: "Inspecciones",
                column: "IdEquipo");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_IdUsuario",
                table: "Inspecciones",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecciones_Resultado",
                table: "Inspecciones",
                column: "Resultado");

            migrationBuilder.CreateIndex(
                name: "IX_PreguntasChecklist_IdChecklist",
                table: "PreguntasChecklist",
                column: "IdChecklist");

            migrationBuilder.CreateIndex(
                name: "IX_PreguntasChecklist_TipoRespuesta",
                table: "PreguntasChecklist",
                column: "TipoRespuesta");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasInspeccion_IdInspeccion",
                table: "RespuestasInspeccion",
                column: "IdInspeccion");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasInspeccion_IdInspeccion_IdPregunta",
                table: "RespuestasInspeccion",
                columns: new[] { "IdInspeccion", "IdPregunta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasInspeccion_IdPregunta",
                table: "RespuestasInspeccion",
                column: "IdPregunta");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Nombre",
                table: "Roles",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ubicaciones_Planta_Area",
                table: "Ubicaciones",
                columns: new[] { "Planta", "Area" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRol_IdRol",
                table: "UsuarioRol",
                column: "IdRol");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRol_IdUsuario_IdRol",
                table: "UsuarioRol",
                columns: new[] { "IdUsuario", "IdRol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_UsuarioAD",
                table: "Usuarios",
                column: "UsuarioAD",
                unique: true,
                filter: "[UsuarioAD] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Evidencias");

            migrationBuilder.DropTable(
                name: "Hallazgos");

            migrationBuilder.DropTable(
                name: "RespuestasInspeccion");

            migrationBuilder.DropTable(
                name: "UsuarioRol");

            migrationBuilder.DropTable(
                name: "Inspecciones");

            migrationBuilder.DropTable(
                name: "PreguntasChecklist");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Equipos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Checklists");

            migrationBuilder.DropTable(
                name: "CategoriasEquipo");

            migrationBuilder.DropTable(
                name: "Ubicaciones");
        }
    }
}
