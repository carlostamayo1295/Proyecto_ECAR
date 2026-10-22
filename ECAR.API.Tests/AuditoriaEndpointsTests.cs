using ClosedXML.Excel;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Endpoint 21 (BE-2): exportación de la auditoría con los filtros del endpoint 1. Los endpoints
/// 1 a 3 los prueban AuditoriaConsultaTests, AuditoriaHistorialTests y AuditoriaVerificacionTests (BE-1).
/// </summary>
public class AuditoriaEndpointsTests
{
    private sealed class Auditor : ICurrentUser
    {
        public long IdUsuario => 1;
        public string Nombre => "Diana Auditora";
        public IReadOnlyCollection<string> Roles => ["Supervisor"];
        public bool IsInRole(string role) => role == "Supervisor";
    }

    private sealed class Contexto : IContextoAuditoria
    {
        public long? IdUsuario => 1;
        public string Usuario => "Diana Auditora (diana@ecar.test)";
        public string? DireccionIp => "10.1.2.3";
        public string? AgenteUsuario => "Pruebas";
        public string? Motivo { get; set; }
    }

    private static async Task<(ECARDbContext Db, string Nombre)> BaseConCambiosAsync()
    {
        var nombre = Guid.NewGuid().ToString();
        var contexto = new Contexto();
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(nombre).Options, contexto);
        var equipo = new Equipo { CodigoInterno = "EQ-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza" };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();
        contexto.Motivo = "Recalibración trimestral";
        equipo.Marca = "Mettler";
        await db.SaveChangesAsync();
        contexto.Motivo = null;
        db.Roles.Add(new Rol { Nombre = "Supervisor" });
        await db.SaveChangesAsync();
        return (db, nombre);
    }

    [Fact]
    public async Task Exportar_a_Excel_trae_una_fila_por_cambio_y_queda_en_la_auditoria()
    {
        var (db, _) = await BaseConCambiosAsync();

        var archivo = Assert.IsType<FileContentResult>(
            await new AuditoriaController(db).Exportar(new AuditoriaFiltroDto { Tabla = "Equipos" }, new Auditor(), ReporteFormatos.Excel));

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", archivo.ContentType);
        using var libro = new XLWorkbook(new MemoryStream(archivo.FileContents));
        var hoja = libro.Worksheets.First();
        Assert.Equal("Auditoría", hoja.Cell(1, 1).GetString());
        Assert.Contains("tabla Equipos", hoja.Cell(2, 1).GetString());
        Assert.Equal(2, hoja.RangeUsed()!.RowsUsed().Count(r => r.Cell(3).GetString() == "Equipos"));
        var pieExcel = Fabrica.PieDeExcel(archivo.FileContents);
        Assert.Contains("Auditoría · Filtros: tabla Equipos", pieExcel);
        Assert.Contains("Página &P de &N", pieExcel);
        Assert.Equal(AuditoriaAcciones.Exportacion, (await db.Auditoria.OrderBy(a => a.IdAuditoria).LastAsync()).Accion);
    }

    [Fact]
    public async Task Exportar_a_PDF_genera_un_documento()
    {
        var (db, _) = await BaseConCambiosAsync();

        var archivo = Assert.IsType<FileContentResult>(await new AuditoriaController(db).Exportar(new AuditoriaFiltroDto(), new Auditor(), ReporteFormatos.Pdf));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(archivo.FileContents, 0, 4));
    }

    [Fact]
    public async Task Exportar_rechaza_los_mismos_filtros_invalidos_que_el_listado()
    {
        var (db, _) = await BaseConCambiosAsync();

        var resultado = await new AuditoriaController(db).Exportar(
            new AuditoriaFiltroDto { Tabla = "NoExiste" }, new Auditor(), ReporteFormatos.Excel);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }
}
