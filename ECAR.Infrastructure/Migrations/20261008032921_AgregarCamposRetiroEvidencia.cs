using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECAR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposRetiroEvidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_Evidencias_IdUsuarioRetiro",
                table: "Evidencias",
                column: "IdUsuarioRetiro");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidencias_Usuarios_IdUsuarioRetiro",
                table: "Evidencias",
                column: "IdUsuarioRetiro",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evidencias_Usuarios_IdUsuarioRetiro",
                table: "Evidencias");

            migrationBuilder.DropIndex(
                name: "IX_Evidencias_IdUsuarioRetiro",
                table: "Evidencias");

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
        }
    }
}
