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
/// Endpoints 1, 2, 3 (BE-1) y 21 (BE-2): filtros, historial de un registro, verificación de la
/// cadena y exportación de la auditoría.
/// </summary>
public class AuditoriaEndpointsTests
{
    private sealed class Auditor : ICurrentUser
    {
        public long IdUsuario => 1;
        public string Nombre => "Diana Auditora";
        public IReadOnlyCollection<string> Roles => ["Auditor"];
        public bool IsInRole(string role) => role == "Auditor";
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
        db.Roles.Add(new Rol { Nombre = "Auditor" });
        await db.SaveChangesAsync();
        return (db, nombre);
    }

    private static T Ok<T>(ActionResult<ApiResponse<T>> resultado) =>
        Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;

    [Fact]
    public async Task El_listado_filtra_y_trae_ip_motivo_y_hash()
    {
        var (db, _) = await BaseConCambiosAsync();
        var controlador = new AuditoriaController(db, new Auditor());

        var equipos = Ok(await controlador.GetAuditoria(tabla: "Equipos"));
        var modificaciones = Ok(await controlador.GetAuditoria(accion: AuditoriaAcciones.Modificar));
        var porTexto = Ok(await controlador.GetAuditoria(search: "Recalibración"));

        Assert.Equal(2, equipos.TotalCount);
        var fila = Assert.Single(modificaciones.Data);
        Assert.Equal("10.1.2.3", fila.DireccionIp);
        Assert.Equal("Recalibración trimestral", fila.Motivo);
        Assert.Equal(64, fila.Hash!.Length);
        Assert.Equal(1, fila.IdUsuario);
        Assert.Equal(1, porTexto.TotalCount);
    }

    [Fact]
    public async Task El_historial_de_un_registro_va_en_orden()
    {
        var (db, _) = await BaseConCambiosAsync();
        var idEquipo = (await db.Equipos.SingleAsync()).IdEquipo;

        var historial = Ok(await new AuditoriaController(db, new Auditor()).GetHistorialRegistro("Equipos", idEquipo));

        Assert.Equal([AuditoriaAcciones.Crear, AuditoriaAcciones.Modificar], historial.Select(h => h.Accion));
    }

    [Fact]
    public async Task Verificar_dice_que_la_cadena_esta_integra_y_queda_en_la_auditoria()
    {
        var (db, _) = await BaseConCambiosAsync();

        var resultado = Ok(await new AuditoriaController(db, new Auditor()).Verificar());

        Assert.True(resultado.Valida);
        Assert.Equal(3, resultado.FilasRevisadas);
        Assert.Null(resultado.PrimeraFilaRota);
        Assert.Equal(AuditoriaAcciones.VerificacionIntegridad, (await db.Auditoria.OrderBy(a => a.IdAuditoria).LastAsync()).Accion);
    }

    [Fact]
    public async Task Verificar_detecta_una_fila_alterada()
    {
        var (db, nombre) = await BaseConCambiosAsync();
        // El proveedor en memoria no tiene el trigger: sirve para simular a quien lo desactiva.
        // ECARDbContext no audita los cambios a la propia tabla Auditoria, así que el cambio pasa.
        var idAlterada = await db.Auditoria.OrderBy(a => a.IdAuditoria).Skip(1).Select(a => a.IdAuditoria).FirstAsync();
        await using (var directo = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(nombre).Options))
        {
            var alterar = await directo.Auditoria.SingleAsync(a => a.IdAuditoria == idAlterada);
            alterar.ValorNuevo = "{\"Marca\":\"Otra\"}";
            await directo.SaveChangesAsync();
        }

        var resultado = Ok(await new AuditoriaController(db, new Auditor()).Verificar());

        Assert.False(resultado.Valida);
        Assert.Equal(idAlterada, resultado.PrimeraFilaRota);
    }

    [Fact]
    public async Task Exportar_a_Excel_trae_una_fila_por_cambio_y_queda_en_la_auditoria()
    {
        var (db, _) = await BaseConCambiosAsync();

        var archivo = Assert.IsType<FileContentResult>(
            await new AuditoriaController(db, new Auditor()).Exportar(ReporteFormatos.Excel, tabla: "Equipos"));

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

        var archivo = Assert.IsType<FileContentResult>(await new AuditoriaController(db, new Auditor()).Exportar(ReporteFormatos.Pdf));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(archivo.FileContents, 0, 4));
    }
}
