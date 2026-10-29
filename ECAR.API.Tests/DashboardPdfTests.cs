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
/// B-03 (ECAR-224): el tablero en PDF A4 con los filtros de B-01 (desde, hasta y planta), su
/// contenido y su registro en la auditoría como Exportacion. Los filtros en sí los prueba
/// CorreccionesPruebasTests (B-01).
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
    private static readonly DateTime InicioMes = new(Hoy.Year, Hoy.Month, 1);

    /// <summary>
    /// Balanza (Planta 1 - Pesaje): una cerrada hoy y una en curso. Bomba (Planta 2 - Producción):
    /// una firmada hace 40 días, fuera del mes en curso, con una novedad pendiente.
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

    private static async Task<DashboardDto> TableroAsync(ECARDbContext db, DateTime? desde = null, DateTime? hasta = null,
        string? planta = null)
    {
        var resultado = await new DashboardController(db).GetDashboard(desde, hasta, planta);
        return Assert.IsType<ApiResponse<DashboardDto>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
    }

    [Fact]
    public async Task Sin_filtros_el_PDF_es_del_mes_en_curso_en_A4_vertical_y_no_esta_vacio()
    {
        var db = await SeedAsync();

        var pdf = Assert.IsType<FileContentResult>(await new DashboardController(db).GetDashboardPdf(new Supervisor()));

        Assert.Equal(ExportadorTabla.TipoPdf, pdf.ContentType);
        Assert.Equal($"ECAR_tablero_{InicioMes:yyyyMMdd}_{Hoy:yyyyMMdd}.pdf", pdf.FileDownloadName);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
        Assert.True(pdf.FileContents.Length > 1000);
        using var documento = PdfReader.Open(new MemoryStream(pdf.FileContents), PdfDocumentOpenMode.Import);
        Assert.Equal(TableroPdf.Titulo, documento.Info.Title);
        Assert.Equal("Sara Supervisora", documento.Info.Author);
        Assert.InRange(documento.Pages[0].MediaBox.Width, 594, 596);   // 21 cm en puntos
        Assert.InRange(documento.Pages[0].MediaBox.Height, 841, 843);  // 29,7 cm
    }

    [Fact]
    public async Task El_PDF_lleva_los_indicadores_el_periodo_las_areas_y_los_filtros_en_el_pie()
    {
        var db = await SeedAsync();
        var tablero = await TableroAsync(db);
        var filtros = DashboardController.TextoFiltros(tablero.Filtros);

        var ddl = DdlWriter.WriteToString(TableroPdf.Crear(tablero, filtros, "Sara Supervisora", DateTime.UtcNow));

        Assert.Equal($"desde {InicioMes:dd/MM/yyyy}, hasta {Hoy:dd/MM/yyyy}, todas las plantas", filtros);
        Assert.Contains("Tablero de indicadores", ddl);
        Assert.Contains(tablero.Periodo, ddl);
        Assert.Contains("Equipos activos", ddl);
        Assert.Contains("Inspecciones realizadas en el periodo", ddl);
        Assert.Contains("Novedades resueltas en el periodo", ddl);
        Assert.Contains("Planta 1 - Pesaje", ddl);
        Assert.Contains("Planta 2 - Producci", ddl);
        Assert.Contains("Filtros: " + filtros, ddl);
        Assert.Contains("Generado por Sara Supervisora", ddl);
    }

    [Fact]
    public async Task El_PDF_usa_la_planta_del_filtro_igual_que_la_pantalla()
    {
        var db = await SeedAsync();
        var desde = Hoy.AddDays(-60);
        var tablero = await TableroAsync(db, desde, Hoy, " Planta 2 ");

        var ddl = DdlWriter.WriteToString(TableroPdf.Crear(tablero, DashboardController.TextoFiltros(tablero.Filtros),
            "Sara Supervisora", DateTime.UtcNow));
        var pdf = Assert.IsType<FileContentResult>(await new DashboardController(db).GetDashboardPdf(new Supervisor(), desde, Hoy, " Planta 2 "));

        Assert.Equal(1, tablero.TotalEquipos);
        Assert.Equal(1, tablero.NovedadesPendientes);
        Assert.Contains("planta Planta 2", ddl);
        Assert.Contains("Planta 2 - Producci", ddl);
        Assert.DoesNotContain("Planta 1 - Pesaje", ddl);
        Assert.Equal($"ECAR_tablero_{desde:yyyyMMdd}_{Hoy:yyyyMMdd}.pdf", pdf.FileDownloadName);
    }

    [Fact]
    public async Task Cada_PDF_queda_en_la_auditoria_como_Exportacion()
    {
        var db = await SeedAsync();

        await new DashboardController(db).GetDashboardPdf(new Supervisor(), planta: "Planta 1");

        var evento = Assert.Single(await db.Auditoria.Where(a => a.Accion == AuditoriaAcciones.Exportacion).ToListAsync());
        Assert.Equal(AuditoriaTablas.Sistema, evento.Tabla);
        Assert.Equal(7, evento.RegistroId);
        Assert.Contains("\"tablero\"", evento.ValorNuevo);
        Assert.Contains("planta Planta 1", evento.ValorNuevo);
    }

    [Fact]
    public async Task Un_rango_invalido_da_400_y_no_deja_auditoria()
    {
        var db = await SeedAsync();
        var c = new DashboardController(db);

        Assert.IsType<BadRequestObjectResult>(await c.GetDashboardPdf(new Supervisor(), Hoy, Hoy.AddDays(-1)));
        Assert.IsType<BadRequestObjectResult>(await c.GetDashboardPdf(new Supervisor(), Hoy.AddYears(-2), Hoy));
        Assert.False(await db.Auditoria.AnyAsync(a => a.Accion == AuditoriaAcciones.Exportacion));
    }

    // El periodo del título (pantalla y PDF) es el nombre del mes solo si es el mes entero o el mes en
    // curso hasta hoy; un rango dentro de un mes salía antes como el mes completo.
    [Theory]
    [InlineData("2026-03-01", "2026-03-31", "marzo de 2026")]
    [InlineData("2026-10-01", "2026-10-29", "octubre de 2026")]
    [InlineData("2026-10-05", "2026-10-10", "05/10/2026 al 10/10/2026")]
    [InlineData("2026-10-01", "2026-10-15", "01/10/2026 al 15/10/2026")]
    [InlineData("2026-09-15", "2026-10-14", "15/09/2026 al 14/10/2026")]
    public void El_periodo_lleva_el_nombre_del_mes_solo_si_es_el_mes_entero_o_el_en_curso(string desde, string hasta,
        string esperado)
    {
        var hoy = new DateTime(2026, 10, 29);

        Assert.Equal(esperado, DashboardController.TextoPeriodo(DateTime.Parse(desde), DateTime.Parse(hasta), hoy));
    }
}
