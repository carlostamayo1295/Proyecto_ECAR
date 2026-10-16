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
/// BE-2, Fase 4 (endpoints 14 a 20, PLAN_FASE4_TAREAS §3.9): totales con datos sembrados, filtros,
/// validación de fechas, PDF y Excel no vacíos con su pie, y cada archivo en la auditoría.
/// </summary>
public class ReportesFase4Tests
{
    private sealed class Admin : ICurrentUser
    {
        public long IdUsuario => 1;
        public string Nombre => "Carla Admin";
        public IReadOnlyCollection<string> Roles => ["Administrador"];
        public bool IsInRole(string role) => role == "Administrador";
    }

    private sealed class FotoFija : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => Task.FromResult("x" + extension);
        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream(Convert.FromBase64String(Png)));
        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }

    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static readonly DateTime Hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;

    private sealed record Datos(ECARDbContext Db, Equipo Balanza, Equipo Bomba, Equipo SinFrecuencia, Usuario Tecnico);

    /// <summary>
    /// Balanza (Mensual, QC): una conforme hoy y una con novedad hace 3 días, más una en curso.
    /// Bomba (Mensual, Producción): ninguna inspección. Otro equipo sin frecuencia.
    /// </summary>
    private static async Task<Datos> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tecnico = new Usuario { Nombre = "Ana Técnica", Correo = "ana@ecar.test", PasswordHash = "x" };
        var qc = new Ubicacion { Planta = "Planta Principal", Area = "Control de Calidad" };
        var produccion = new Ubicacion { Planta = "Planta Principal", Area = "Producción" };
        var antes = DateTime.UtcNow.AddYears(-1);
        var balanza = new Equipo { CodigoInterno = "BAL-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Ubicacion = qc,
            FrecuenciaInspeccion = FrecuenciasInspeccion.Mensual, FechaCreacion = antes, Marca = "Mettler", Modelo = "XS" };
        var bomba = new Equipo { CodigoInterno = "BOM-1", ActivoFijo = "AF-2", NombreEquipo = "Bomba", Ubicacion = produccion,
            FrecuenciaInspeccion = FrecuenciasInspeccion.Mensual, FechaCreacion = antes };
        var sinFrecuencia = new Equipo { CodigoInterno = "VAR-1", ActivoFijo = "AF-3", NombreEquipo = "Varios", Ubicacion = qc, FechaCreacion = antes };
        var checklist = new Checklist { Nombre = "General", Version = "1" };
        db.AddRange(tecnico, balanza, bomba, sinFrecuencia, checklist);
        await db.SaveChangesAsync();

        Inspeccion Nueva(Equipo equipo, int diasAtras, string estado, string? resultado) => new()
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow.AddDays(-diasAtras), Estado = estado, Resultado = resultado,
            FechaCierre = estado == InspeccionEstados.Cerrada ? DateTime.UtcNow.AddDays(-diasAtras) : null
        };

        var conNovedad = Nueva(balanza, 3, InspeccionEstados.Cerrada, InspeccionResultados.ConNovedad);
        db.Inspecciones.AddRange(
            Nueva(balanza, 0, InspeccionEstados.Cerrada, InspeccionResultados.Conforme),
            conNovedad,
            Nueva(balanza, 1, InspeccionEstados.EnCurso, null));
        await db.SaveChangesAsync();
        db.Hallazgos.Add(new Hallazgo
        {
            IdInspeccion = conNovedad.IdInspeccion, Descripcion = "Plato descentrado", Criticidad = "Alta",
            Estado = HallazgoEstados.Abierto, Origen = HallazgoOrigenes.Novedad, IdUsuarioRegistro = tecnico.IdUsuario,
            IdUsuarioResponsable = tecnico.IdUsuario
        });
        await db.SaveChangesAsync();
        return new Datos(db, balanza, bomba, sinFrecuencia, tecnico);
    }

    private static ReportesController Controlador(ECARDbContext db) => new(new ReporteService(db), db, new Admin());

    private static ReporteFiltroDto Mes() => new() { Desde = Hoy.AddDays(-Hoy.Day + 1).AddMonths(-1), Hasta = Hoy };

    private static T Datos_<T>(IActionResult resultado) =>
        Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(resultado).Value).Data!;

    [Fact]
    public async Task Las_fechas_son_obligatorias_con_un_anio_como_maximo()
    {
        var d = await SeedAsync();
        var c = Controlador(d.Db);

        Assert.IsType<BadRequestObjectResult>(await c.GetHistorialEquipo(new ReporteFiltroDto()));
        Assert.IsType<BadRequestObjectResult>(await c.GetHistorialEquipo(new ReporteFiltroDto { Desde = Hoy, Hasta = Hoy.AddDays(-1) }));
        Assert.IsType<BadRequestObjectResult>(await c.GetHistorialEquipo(new ReporteFiltroDto { Desde = Hoy.AddYears(-2), Hasta = Hoy }));
        Assert.IsType<BadRequestObjectResult>(await c.GetHistorialEquipo(Mes(), formato: "docx"));
    }

    [Fact]
    public async Task Historial_por_equipo_trae_solo_las_firmadas_del_rango_y_pagina()
    {
        var d = await SeedAsync();

        var pagina = Datos_<PagedResultDto<HistorialEquipoFilaDto>>(await Controlador(d.Db).GetHistorialEquipo(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, IdEquipo = d.Balanza.IdEquipo }, pageSize: 1));
        var soloHoy = Datos_<PagedResultDto<HistorialEquipoFilaDto>>(await Controlador(d.Db).GetHistorialEquipo(
            new ReporteFiltroDto { Desde = Hoy, Hasta = Hoy }));

        Assert.Equal(2, pagina.TotalCount); // la en curso no cuenta
        Assert.Single(pagina.Data);
        Assert.Equal("Planta Principal - Control de Calidad", pagina.Data[0].Area);
        Assert.Equal(1, soloHoy.TotalCount);
    }

    [Fact]
    public async Task Historial_por_area_y_por_tecnico_filtran()
    {
        var d = await SeedAsync();
        var c = Controlador(d.Db);

        var produccion = Datos_<PagedResultDto<HistorialAreaFilaDto>>(await c.GetHistorialArea(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, Area = "Producción" }));
        var tecnico = Datos_<PagedResultDto<HistorialTecnicoFilaDto>>(await c.GetHistorialTecnico(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, IdUsuario = d.Tecnico.IdUsuario }));

        Assert.Equal(0, produccion.TotalCount);
        Assert.Equal(2, tecnico.TotalCount);
        Assert.All(tecnico.Data, f => Assert.Equal("Ana Técnica", f.Tecnico));
    }

    [Fact]
    public async Task Equipos_con_novedades_trae_responsable_y_estado()
    {
        var d = await SeedAsync();

        var filas = Datos_<PagedResultDto<EquipoNovedadFilaDto>>(await Controlador(d.Db).GetEquiposNovedades(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, Criticidad = "Alta" }));

        var fila = Assert.Single(filas.Data);
        Assert.Equal("BAL-1 - Balanza", fila.Equipo);
        Assert.Equal("Ana Técnica", fila.Responsable);
        Assert.Equal(HallazgoEstados.Abierto, fila.Estado);
    }

    [Fact]
    public async Task Inspecciones_por_fechas_cuenta_conformes_y_con_novedad()
    {
        var d = await SeedAsync();

        var totales = Datos_<InspeccionesPorFechasDto>(await Controlador(d.Db).GetInspeccionesFechas(Mes()));

        Assert.Equal(2, totales.TotalInspecciones);
        Assert.Equal(1, totales.Satisfactorias);
        Assert.Equal(1, totales.ConNovedad);
        Assert.Single(totales.PorArea);
    }

    [Fact]
    public async Task Cumplimiento_usa_la_frecuencia_de_cada_equipo()
    {
        var d = await SeedAsync();

        var filas = Datos_<PagedResultDto<CumplimientoFilaDto>>(await Controlador(d.Db).GetCumplimiento(
            new ReporteFiltroDto { Desde = Hoy.AddDays(-Hoy.Day + 1), Hasta = Hoy })).Data;

        // Este mes: la balanza (QC) tiene inspección firmada; la bomba (Producción) no.
        var qc = Assert.Single(filas, f => f.Area == "Control de Calidad");
        var produccion = Assert.Single(filas, f => f.Area == "Producción");
        Assert.Equal((1, 1, 100m), (qc.EquiposProgramados, qc.EquiposInspeccionados, qc.PorcentajeCumplimiento!.Value));
        Assert.Equal((1, 0, 0m), (produccion.EquiposProgramados, produccion.EquiposInspeccionados, produccion.PorcentajeCumplimiento!.Value));
    }

    [Fact]
    public async Task El_PDF_y_el_Excel_no_estan_vacios_llevan_el_pie_y_quedan_en_la_auditoria()
    {
        var d = await SeedAsync();
        var c = Controlador(d.Db);

        var pdf = Assert.IsType<FileContentResult>(await c.GetHistorialEquipo(Mes(), ReporteFormatos.Pdf));
        var excel = Assert.IsType<FileContentResult>(await c.GetHistorialEquipo(Mes(), ReporteFormatos.Excel));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
        Assert.True(pdf.FileContents.Length > 1000);
        Assert.StartsWith("ECAR_historial-equipo_", pdf.FileDownloadName);
        using var libro = new XLWorkbook(new MemoryStream(excel.FileContents));
        var hoja = libro.Worksheets.First();
        Assert.Equal("Historial por equipo", hoja.Cell(1, 1).GetString());
        Assert.Contains("Generado por Carla Admin", hoja.Cell(3, 1).GetString());
        Assert.Contains("Historial por equipo · Filtros:", Fabrica.PieDeExcel(excel.FileContents));
        Assert.Contains(hoja.RowsUsed(), r => r.Cell(2).GetString() == "BAL-1");
        Assert.Equal(2, await d.Db.Auditoria.CountAsync(a => a.Accion == AuditoriaAcciones.Exportacion));
    }

    [Fact]
    public async Task Los_reportes_de_totales_tambien_salen_en_archivo()
    {
        var d = await SeedAsync();
        var c = Controlador(d.Db);

        Assert.IsType<FileContentResult>(await c.GetCumplimiento(Mes(), ReporteFormatos.Pdf));
        Assert.IsType<FileContentResult>(await c.GetInspeccionesFechas(Mes(), ReporteFormatos.Excel));
    }

    [Fact]
    public async Task El_registro_PDF_de_una_inspeccion_firmada_sale_con_su_firma_y_queda_en_la_auditoria()
    {
        var d = await SeedAsync();
        var pregunta = new PreguntaChecklist { Pregunta = "¿Nivelada?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 1 };
        var checklist = new Checklist { Nombre = "Balanza", Version = "2", Preguntas = [pregunta] };
        d.Db.Checklists.Add(checklist);
        await d.Db.SaveChangesAsync();
        var inspeccion = new Inspeccion
        {
            IdEquipo = d.Balanza.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = d.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Respuestas = [new RespuestaInspeccion { IdPregunta = pregunta.IdPregunta, Respuesta = "No", Observacion = "Burbuja corrida" }],
            Evidencias = [new Evidencia { Archivo = "f.png", NombreOriginal = "f.png", TipoContenido = "image/png", IdUsuarioCarga = d.Tecnico.IdUsuario }]
        };
        d.Db.Inspecciones.Add(inspeccion);
        await d.Db.SaveChangesAsync();
        d.Db.ChangeTracker.Clear();
        var tecnico = new FakeTecnico(d.Tecnico);
        await Fabrica.Inspecciones(d.Db, tecnico, new FotoFija()).FirmarInspeccion(inspeccion.IdInspeccion,
            new FirmarInspeccionDto { Password = Fabrica.ClaveFirma, FirmaPngBase64 = Png });
        d.Db.ChangeTracker.Clear();

        var archivo = Assert.IsType<FileContentResult>(
            await Fabrica.Inspecciones(d.Db, tecnico, new FotoFija()).ObtenerRegistroPdf(inspeccion.IdInspeccion));

        Assert.Equal($"ECAR-INSP-{inspeccion.IdInspeccion}.pdf", archivo.FileDownloadName);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(archivo.FileContents, 0, 4));
        Assert.Contains(await d.Db.Auditoria.Select(a => a.Accion).ToListAsync(), a => a == AuditoriaAcciones.Exportacion);
    }

    private sealed class FakeTecnico(Usuario usuario) : ICurrentUser
    {
        public long IdUsuario => usuario.IdUsuario;
        public string Nombre => usuario.Nombre;
        public IReadOnlyCollection<string> Roles => ["Técnico"];
        public bool IsInRole(string role) => role == "Técnico";
    }
}
