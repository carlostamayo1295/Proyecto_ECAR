using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.API.Services.Documentos;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using MigraDoc.DocumentObjectModel.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// B-03 (ECAR-224): el tablero en PDF A4 con los filtros de B-01, su contenido y su registro en la
/// auditoría como Exportacion. Los filtros acotan igual la pantalla y el PDF.
/// </summary>
public class DashboardPdfTests
{
    private sealed class Supervisor : ICurrentUser
    {
        public long IdUsuario => 7;
        public string Nombre => "Sara Supervisora";
        public IReadOnlyCollection<string> Roles => ["Supervisor"];
        public bool IsInRole(string role) => role == "Supervisor";
    }

    private static readonly DateTime Hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;

    /// <summary>
    /// Balanza (Planta 1 - Pesaje, Alta): una cerrada hoy y una en curso. Bomba (Planta 2 -
    /// Producción, Baja): una firmada hace 40 días con una novedad pendiente.
    /// </summary>
    private static async Task<ECARDbContext> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tecnico = new Usuario { Nombre = "Ana", Correo = "ana@ecar.test", PasswordHash = "x" };
        var balanza = new Equipo { CodigoInterno = "BAL-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Criticidad = Criticidades.Alta,
            Ubicacion = new Ubicacion { Planta = "Planta 1", Area = "Pesaje" } };
        var bomba = new Equipo { CodigoInterno = "BOM-1", ActivoFijo = "AF-2", NombreEquipo = "Bomba", Criticidad = Criticidades.Baja,
            Ubicacion = new Ubicacion { Planta = "Planta 2", Area = "Producción" } };
        var checklist = new Checklist { Nombre = "General", Version = "1" };
        db.AddRange(tecnico, balanza, bomba, checklist);
        await db.SaveChangesAsync();

        Inspeccion Nueva(Equipo equipo, int diasAtras, string estado) => new()
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow.AddDays(-diasAtras), Estado = estado
        };

        var conNovedad = Nueva(bomba, 40, InspeccionEstados.NovedadesPendientes);
        db.Inspecciones.AddRange(Nueva(balanza, 0, InspeccionEstados.Cerrada), Nueva(balanza, 0, InspeccionEstados.EnCurso), conNovedad);
        await db.SaveChangesAsync();
        db.Novedades.Add(new Novedad
        {
            IdInspeccion = conNovedad.IdInspeccion, IdPregunta = 1, IdRespuesta = 1, Descripcion = "Fuga en el sello",
            Criticidad = Criticidades.Baja, FechaRegistro = conNovedad.FechaInspeccion, IdUsuarioRegistro = tecnico.IdUsuario
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static DashboardController Controlador(ECARDbContext db) => new(db, new Supervisor());

    private static async Task<DashboardDto> TableroAsync(ECARDbContext db, DashboardFiltroDto? filtro = null)
    {
        var resultado = await Controlador(db).GetDashboard(filtro);
        return Assert.IsType<ApiResponse<DashboardDto>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
    }

    [Fact]
    public async Task El_PDF_sale_en_A4_vertical_no_vacio_y_con_el_titulo_del_tablero()
    {
        var db = await SeedAsync();

        var pdf = Assert.IsType<FileContentResult>(await Controlador(db).GetDashboardPdf(new DashboardFiltroDto { Planta = "Planta 1" }));

        Assert.Equal(ExportadorTabla.TipoPdf, pdf.ContentType);
        Assert.Equal($"ECAR_tablero_{Hoy:yyyyMMdd}.pdf", pdf.FileDownloadName);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
        Assert.True(pdf.FileContents.Length > 1000);
        using var documento = PdfReader.Open(new MemoryStream(pdf.FileContents), PdfDocumentOpenMode.Import);
        Assert.Equal(TableroPdf.Titulo, documento.Info.Title);
        Assert.Equal("Sara Supervisora", documento.Info.Author);
        Assert.InRange(documento.Pages[0].MediaBox.Width, 594, 596);   // 21 cm en puntos
        Assert.InRange(documento.Pages[0].MediaBox.Height, 841, 843);  // 29,7 cm
    }

    [Fact]
    public async Task El_PDF_lleva_los_indicadores_las_areas_y_los_filtros_en_el_pie()
    {
        var db = await SeedAsync();
        var tablero = await TableroAsync(db);

        var ddl = DdlWriter.WriteToString(TableroPdf.Crear(tablero, "planta Planta 1", "Sara Supervisora", DateTime.UtcNow));

        Assert.Contains("Tablero de indicadores", ddl);
        Assert.Contains("Equipos activos", ddl);
        Assert.Contains("Inspecciones con novedades pendientes", ddl);
        Assert.Contains("Novedades pendientes", ddl);
        Assert.Contains("Planta 1 - Pesaje", ddl);
        Assert.Contains("Planta 2 - Producci", ddl);
        Assert.Contains("Filtros: planta Planta 1", ddl);
        Assert.Contains("Generado por Sara Supervisora", ddl);
    }

    [Fact]
    public async Task Cada_PDF_queda_en_la_auditoria_como_Exportacion()
    {
        var db = await SeedAsync();

        await Controlador(db).GetDashboardPdf(new DashboardFiltroDto { Criticidad = Criticidades.Alta });

        var evento = Assert.Single(await db.Auditoria.Where(a => a.Accion == AuditoriaAcciones.Exportacion).ToListAsync());
        Assert.Equal(AuditoriaTablas.Sistema, evento.Tabla);
        Assert.Equal(7, evento.RegistroId);
        Assert.Contains("\"tablero\"", evento.ValorNuevo);
        Assert.Contains("criticidad Alta", evento.ValorNuevo);
    }

    [Fact]
    public async Task Sin_filtros_el_tablero_cuenta_todo()
    {
        var t = await TableroAsync(await SeedAsync());

        Assert.Equal(2, t.TotalEquipos);
        Assert.Equal(2, t.InspeccionesRealizadas);
        Assert.Equal(1, t.InspeccionesEnCurso);
        Assert.Equal(1, t.InspeccionesConNovedadesPendientes);
        Assert.Equal(1, t.NovedadesPendientes);
    }

    [Fact]
    public async Task Planta_area_y_criticidad_acotan_equipos_inspecciones_y_novedades()
    {
        var db = await SeedAsync();

        var pesaje = await TableroAsync(db, new DashboardFiltroDto { Planta = "Planta 1", Area = "Pesaje" });
        var baja = await TableroAsync(db, new DashboardFiltroDto { Criticidad = Criticidades.Baja });

        Assert.Equal(1, pesaje.TotalEquipos);
        Assert.Equal(1, pesaje.InspeccionesRealizadas);
        Assert.Equal(1, pesaje.InspeccionesEnCurso);
        Assert.Equal(0, pesaje.NovedadesPendientes);
        Assert.Equal("Planta 1 - Pesaje", Assert.Single(pesaje.EquiposPorArea).Etiqueta);
        Assert.Equal(1, baja.TotalEquipos);
        Assert.Equal(1, baja.InspeccionesConNovedadesPendientes);
        Assert.Equal(1, baja.NovedadesPendientes);
        Assert.Equal(0, baja.InspeccionesEnCurso);
    }

    [Fact]
    public async Task Las_fechas_acotan_inspecciones_y_novedades_pero_no_el_inventario()
    {
        var t = await TableroAsync(await SeedAsync(), new DashboardFiltroDto { Desde = Hoy.AddDays(-7), Hasta = Hoy });

        Assert.Equal(2, t.TotalEquipos);
        Assert.Equal(1, t.InspeccionesRealizadas);
        Assert.Equal(0, t.InspeccionesConNovedadesPendientes);
        Assert.Equal(0, t.NovedadesPendientes);
    }

    [Fact]
    public async Task Filtros_invalidos_dan_400_y_no_dejan_auditoria()
    {
        var db = await SeedAsync();
        var c = Controlador(db);

        Assert.IsType<BadRequestObjectResult>(await c.GetDashboardPdf(new DashboardFiltroDto { Desde = Hoy, Hasta = Hoy.AddDays(-1) }));
        Assert.IsType<BadRequestObjectResult>(await c.GetDashboardPdf(new DashboardFiltroDto { Criticidad = "Urgente" }));
        Assert.IsType<BadRequestObjectResult>((await c.GetDashboard(new DashboardFiltroDto { Criticidad = "Urgente" })).Result);
        Assert.False(await db.Auditoria.AnyAsync(a => a.Accion == AuditoriaAcciones.Exportacion));
    }
}
