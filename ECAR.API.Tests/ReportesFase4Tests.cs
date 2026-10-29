using ClosedXML.Excel;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.API.Services.Documentos;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf.IO;
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

    private sealed record Datos(ECARDbContext Db, Equipo Balanza, Equipo Bomba, Equipo Varios, Usuario Tecnico);

    /// <summary>
    /// Balanza (QC): una conforme hoy y una con novedad hace 3 días, más una en curso.
    /// Bomba (Producción): ninguna inspección. Otro equipo en QC, sin inspecciones.
    /// </summary>
    private static async Task<Datos> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tecnico = new Usuario { Nombre = "Ana Técnica", Correo = "ana@ecar.test", PasswordHash = FirmaPruebas.PasswordHash };
        var qc = new Ubicacion { Planta = "Planta Principal", Area = "Control de Calidad" };
        var produccion = new Ubicacion { Planta = "Planta Principal", Area = "Producción" };
        var antes = DateTime.UtcNow.AddYears(-1);
        var balanza = new Equipo { CodigoInterno = "BAL-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Ubicacion = qc,
            FechaCreacion = antes, Marca = "Mettler", Modelo = "XS" };
        var bomba = new Equipo { CodigoInterno = "BOM-1", ActivoFijo = "AF-2", NombreEquipo = "Bomba", Ubicacion = produccion,
            FechaCreacion = antes };
        var varios = new Equipo { CodigoInterno = "VAR-1", ActivoFijo = "AF-3", NombreEquipo = "Varios", Ubicacion = qc, FechaCreacion = antes };
        var checklist = new Checklist { Nombre = "General", Version = "1" };
        db.AddRange(tecnico, balanza, bomba, varios, checklist);
        await db.SaveChangesAsync();

        Inspeccion Nueva(Equipo equipo, int diasAtras, string estado, string? resultado) => new()
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow.AddDays(-diasAtras), Estado = estado, Resultado = resultado,
            FechaCierre = InspeccionEstados.EstaFirmada(estado) ? DateTime.UtcNow.AddDays(-diasAtras) : null
        };

        var conNovedad = Nueva(balanza, 3, InspeccionEstados.Cerrada, InspeccionResultados.Conforme);
        db.Inspecciones.AddRange(
            Nueva(balanza, 0, InspeccionEstados.Cerrada, InspeccionResultados.Conforme),
            conNovedad,
            Nueva(balanza, 1, InspeccionEstados.EnCurso, null));
        await db.SaveChangesAsync();
        // Tuvo una novedad y ya se resolvió: por eso la inspección está Cerrada y Conforme.
        db.Novedades.Add(new Novedad
        {
            IdInspeccion = conNovedad.IdInspeccion, IdPregunta = 1, IdRespuesta = 1, Descripcion = "Plato descentrado",
            Criticidad = "Alta", Estado = NovedadEstados.Resuelta, FechaRegistro = conNovedad.FechaInspeccion,
            IdUsuarioRegistro = tecnico.IdUsuario, IdUsuarioResolucion = tecnico.IdUsuario, Solucion = "Se centró el plato",
            FechaResolucion = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return new Datos(db, balanza, bomba, varios, tecnico);
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
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, IdEquipos = [d.Balanza.IdEquipo] }, pageSize: 1));
        var soloHoy = Datos_<PagedResultDto<HistorialEquipoFilaDto>>(await Controlador(d.Db).GetHistorialEquipo(
            new ReporteFiltroDto { Desde = Hoy, Hasta = Hoy }));

        Assert.Equal(2, pagina.TotalCount); // la en curso no cuenta
        Assert.Single(pagina.Data);
        Assert.Equal("Planta Principal - Control de Calidad", pagina.Data[0].Area);
        Assert.Equal(1, soloHoy.TotalCount);
    }

    [Fact]
    public async Task Historial_por_area_y_por_usuario_filtran()
    {
        var d = await SeedAsync();
        var c = Controlador(d.Db);

        var produccion = Datos_<PagedResultDto<HistorialAreaFilaDto>>(await c.GetHistorialArea(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, Areas = ["Producción"] }));
        var porUsuario = Datos_<PagedResultDto<HistorialUsuarioFilaDto>>(await c.GetHistorialUsuario(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, IdUsuarios = [d.Tecnico.IdUsuario] }));

        Assert.Equal(0, produccion.TotalCount);
        Assert.Equal(2, porUsuario.TotalCount);
        Assert.All(porUsuario.Data, f => Assert.Equal("Ana Técnica", f.Usuario));
    }

    [Fact]
    public async Task Equipos_con_novedades_trae_responsable_y_estado()
    {
        var d = await SeedAsync();

        var filas = Datos_<PagedResultDto<EquipoNovedadFilaDto>>(await Controlador(d.Db).GetEquiposNovedades(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, Criticidades = ["Alta"] }));

        var fila = Assert.Single(filas.Data);
        Assert.Equal("BAL-1 - Balanza", fila.Equipo);
        Assert.Equal("Ana Técnica", fila.Responsable);
        Assert.Equal(NovedadEstados.Resuelta, fila.Estado);
    }

    [Fact]
    public async Task Inspecciones_por_fechas_cuenta_como_con_novedad_la_que_la_tuvo_aunque_ya_este_resuelta()
    {
        var d = await SeedAsync();

        var totales = Datos_<InspeccionesPorFechasDto>(await Controlador(d.Db).GetInspeccionesFechas(Mes()));

        Assert.Equal(2, totales.TotalInspecciones);
        Assert.Equal(1, totales.Satisfactorias);
        Assert.Equal(1, totales.ConNovedad);
        Assert.Single(totales.PorArea);
    }

    [Fact]
    public async Task Una_firmada_con_novedades_pendientes_cuenta_como_realizada_y_con_novedad()
    {
        var d = await SeedAsync();
        d.Db.Inspecciones.Add(new Inspeccion
        {
            IdEquipo = d.Bomba.IdEquipo, IdChecklist = d.Db.Checklists.First().IdChecklist, IdUsuario = d.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, FechaCierre = DateTime.UtcNow,
            Estado = InspeccionEstados.NovedadesPendientes, Resultado = InspeccionResultados.Pendiente
        });
        await d.Db.SaveChangesAsync();
        var c = Controlador(d.Db);

        var totales = Datos_<InspeccionesPorFechasDto>(await c.GetInspeccionesFechas(Mes()));
        var historial = Datos_<PagedResultDto<HistorialEquipoFilaDto>>(await c.GetHistorialEquipo(
            new ReporteFiltroDto { Desde = Mes().Desde, Hasta = Hoy, IdEquipos = [d.Bomba.IdEquipo] }));

        Assert.Equal(3, totales.TotalInspecciones);
        Assert.Equal(1, totales.Satisfactorias);
        Assert.Equal(2, totales.ConNovedad);
        Assert.Equal(InspeccionResultados.Pendiente, Assert.Single(historial.Data).Resultado);
    }

    [Fact]
    public void El_reporte_de_cumplimiento_ya_no_existe()
    {
        // Se retiró con la frecuencia de inspección (decisión de ECAR del 21/10/2026).
        Assert.DoesNotContain(typeof(ReportesController).GetMethods(), m => m.Name.Contains("Cumplimiento"));
        Assert.Null(typeof(Equipo).GetProperty("FrecuenciaInspeccion"));
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
        Assert.Contains(hoja.RowsUsed(), r => r.Cell(3).GetString() == "BAL-1"); // la 2 es la sede
        Assert.Equal(2, await d.Db.Auditoria.CountAsync(a => a.Accion == AuditoriaAcciones.Exportacion));
    }

    [Fact]
    public async Task El_PDF_de_un_reporte_sale_en_A4_horizontal_y_la_tabla_cabe_en_la_hoja()
    {
        // Con «Orientation = Landscape» la hoja salía vertical y la tabla, medida para la horizontal, se cortaba.
        var d = await SeedAsync();
        var pdf = Assert.IsType<FileContentResult>(await Controlador(d.Db).GetHistorialEquipo(Mes(), ReporteFormatos.Pdf));

        var (ancho, alto) = TamanoPrimeraPagina(pdf.FileContents);
        Assert.InRange(ancho, 841, 843); // 29,7 cm en puntos
        Assert.InRange(alto, 594, 596);  // 21 cm

        var pie = new PieDocumento("Prueba", "ninguno", "Carla Admin", DateTime.UtcNow);
        var pagina = ExportadorTabla.Documento("Prueba", pie, horizontal: true).LastSection.PageSetup;
        Assert.Equal(25.7, ExportadorTabla.AnchoUtilCm(pagina), 3); // MigraDoc guarda las medidas como float
    }

    [Fact]
    public async Task Los_reportes_de_totales_tambien_salen_en_archivo()
    {
        var d = await SeedAsync();
        var c = Controlador(d.Db);

        Assert.IsType<FileContentResult>(await c.GetInspeccionesFechas(Mes(), ReporteFormatos.Pdf));
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
            Respuestas = [new RespuestaInspeccion { IdPregunta = pregunta.IdPregunta, Respuesta = "No", Observacion = "Burbuja corrida", PresentaNovedad = true }],
            Evidencias = [new Evidencia { Archivo = "f.png", NombreOriginal = "f.png", TipoContenido = "image/png", IdUsuarioCarga = d.Tecnico.IdUsuario }]
        };
        d.Db.Inspecciones.Add(inspeccion);
        await d.Db.SaveChangesAsync();
        d.Db.ChangeTracker.Clear();
        var tecnico = new FakeTecnico(d.Tecnico);
        await Fabrica.Inspecciones(d.Db, tecnico, new FotoFija()).FirmarConPassword(d.Db, inspeccion.IdInspeccion,
            new FirmarInspeccionDto { Password = Fabrica.ClaveFirma, FirmaPngBase64 = Png }, new FotoFija());
        d.Db.ChangeTracker.Clear();

        var archivo = Assert.IsType<FileContentResult>(
            await Fabrica.Inspecciones(d.Db, tecnico, new FotoFija()).ObtenerRegistroPdf(inspeccion.IdInspeccion));

        Assert.Equal($"ECAR-INSP-{inspeccion.IdInspeccion}.pdf", archivo.FileDownloadName);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(archivo.FileContents, 0, 4));
        Assert.InRange(TamanoPrimeraPagina(archivo.FileContents).Ancho, 594, 596); // el registro sigue en vertical
        Assert.Contains(await d.Db.Auditoria.Select(a => a.Accion).ToListAsync(), a => a == AuditoriaAcciones.Exportacion);
    }

    // 29/10/2026: cada filtro admite varios valores; el reporte trae lo que coincide con cualquiera.
    [Fact]
    public async Task Los_filtros_admiten_varios_valores_a_la_vez()
    {
        var d = await SeedAsync();
        d.Db.Inspecciones.Add(new Inspeccion
        {
            IdEquipo = d.Bomba.IdEquipo, IdChecklist = d.Db.Checklists.First().IdChecklist, IdUsuario = d.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, FechaCierre = DateTime.UtcNow,
            Estado = InspeccionEstados.Cerrada, Resultado = InspeccionResultados.Satisfactorio
        });
        await d.Db.SaveChangesAsync();
        var c = Controlador(d.Db);

        async Task<int> EquiposAsync(ReporteFiltroDto f) =>
            Datos_<PagedResultDto<HistorialEquipoFilaDto>>(await c.GetHistorialEquipo(f)).TotalCount;
        ReporteFiltroDto Filtro() => new() { Desde = Mes().Desde, Hasta = Hoy };

        var dosEquipos = Filtro(); dosEquipos.IdEquipos = [d.Balanza.IdEquipo, d.Bomba.IdEquipo];
        var soloBomba = Filtro(); soloBomba.IdEquipos = [d.Bomba.IdEquipo];
        var dosAreas = Filtro(); dosAreas.Areas = ["Control de Calidad", " Producción "];
        var otraPlanta = Filtro(); otraPlanta.Plantas = ["Planta Principal", "Planta Norte"];
        var ninguna = Filtro(); ninguna.Plantas = ["Planta Norte", "Planta Sur"];

        Assert.Equal(3, await EquiposAsync(dosEquipos));
        Assert.Equal(1, await EquiposAsync(soloBomba));
        Assert.Equal(3, await EquiposAsync(dosAreas));
        Assert.Equal(3, await EquiposAsync(otraPlanta));
        Assert.Equal(0, await EquiposAsync(ninguna));

        var fechas = Filtro(); fechas.Areas = ["Producción"];
        var totales = Datos_<InspeccionesPorFechasDto>(await c.GetInspeccionesFechas(fechas));
        Assert.Equal((1, 1), (totales.TotalInspecciones, totales.Satisfactorias));
    }

    // 29/10/2026: la sede de la inspección sale en los reportes y se puede filtrar.
    [Fact]
    public async Task El_filtro_de_sede_trae_solo_las_de_esa_sede_y_la_fila_la_muestra()
    {
        var d = await SeedAsync();
        var firmadas = await d.Db.Inspecciones.Where(i => i.Estado == InspeccionEstados.Cerrada)
            .OrderBy(i => i.IdInspeccion).ToListAsync();
        firmadas[0].Sede = Sedes.Rionegro;
        firmadas[1].Sede = Sedes.Medellin;
        await d.Db.SaveChangesAsync();
        var filtro = Mes();
        filtro.Sedes = [Sedes.Medellin];

        var pagina = Datos_<PagedResultDto<HistorialEquipoFilaDto>>(await Controlador(d.Db).GetHistorialEquipo(filtro));

        Assert.Equal(Sedes.Medellin, Assert.Single(pagina.Data).Sede);
    }

    [Fact]
    public async Task El_pie_del_archivo_nombra_los_equipos_y_las_plantas_filtradas()
    {
        var d = await SeedAsync();
        var filtro = Mes();
        filtro.IdEquipos = [d.Bomba.IdEquipo, d.Balanza.IdEquipo];
        filtro.Plantas = ["Planta Principal"];

        Assert.IsType<FileContentResult>(await Controlador(d.Db).GetHistorialEquipo(filtro, ReporteFormatos.Pdf));

        var exportacion = await d.Db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AuditoriaAcciones.Exportacion);
        Assert.Contains("planta Planta Principal", exportacion.ValorNuevo);
        Assert.Contains("equipos BAL-1 y BOM-1", exportacion.ValorNuevo);
    }

    // 29/10/2026: el texto de los PDF ya no se sale de las celdas.
    [Fact]
    public void Las_palabras_mas_anchas_que_la_columna_se_parten_y_ningun_trozo_la_supera()
    {
        using var ajuste = new AjusteTexto(ExportadorTabla.TamanoTablaPt);
        var json = "{\"Nombre\":\"Balanza analítica de precisión\",\"Correo\":\"responsable.de.calidad@laboratoriosecar.com.co\"," +
                   "\"Hash\":\"" + new string('a', 64) + "\"}";
        const double limiteCm = 3;

        var trozos = ajuste.Partir("Valor nuevo " + json + " fin", limiteCm, negrita: false);

        Assert.True(trozos.Count > 1);
        Assert.All(trozos.SelectMany(t => t.Split(' ')), palabra => Assert.True(ajuste.AnchoCm(palabra) <= limiteCm,
            $"«{palabra}» mide {ajuste.AnchoCm(palabra):0.00} cm"));
        // Una columna que mide justo la palabra más su relleno no la parte (el redondeo partía «Códig / o»).
        var justa = ajuste.AnchoCm("Código", negrita: true) + AjusteTexto.RellenoCm;
        Assert.Single(ajuste.Partir("Código", justa - AjusteTexto.RellenoCm, negrita: true));

        // El texto no cambia: solo se reparte en renglones.
        Assert.Equal(("Valor nuevo " + json + " fin").Replace(" ", ""), string.Concat(trozos).Replace(" ", ""));
    }

    [Fact]
    public void Las_columnas_se_reparten_segun_el_contenido_y_la_de_mas_informacion_recibe_mas()
    {
        using var ajuste = new AjusteTexto(ExportadorTabla.TamanoTablaPt);
        string[] encabezados = ["#", "Fecha", "Observaciones", "Estado"];
        IReadOnlyList<string>[] filas =
        [
            ["1", "29/10/2026 14:30", string.Join(" ", Enumerable.Repeat("la balanza se calibró y se registró el resultado", 6)), "Cerrada"],
            ["2", "28/10/2026 09:05", "Sin novedad", "Anulada"]
        ];

        var anchos = ajuste.Anchos(encabezados, filas, 25.7);

        Assert.Equal(25.7, anchos.Sum(), 6);
        Assert.Equal(2, Enumerable.Range(0, 4).MaxBy(c => anchos[c]));
        // Las columnas cortas caben en una línea: la fecha completa no se parte.
        Assert.True(anchos[1] >= ajuste.AnchoCm("29/10/2026 14:30") + AjusteTexto.RellenoCm - 1e-9);
    }

    [Fact]
    public async Task Un_reporte_con_textos_largos_sin_espacios_sale_en_PDF()
    {
        var d = await SeedAsync();
        var inspeccion = await d.Db.Inspecciones.FirstAsync(i => i.Estado == InspeccionEstados.Cerrada);
        inspeccion.Observaciones = new string('x', 400) + " " + "correo.muy.largo.de.una.persona@laboratoriosecar.com.co";
        await d.Db.SaveChangesAsync();

        var pdf = Assert.IsType<FileContentResult>(await Controlador(d.Db).GetHistorialEquipo(Mes(), ReporteFormatos.Pdf));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
    }

    private static (double Ancho, double Alto) TamanoPrimeraPagina(byte[] pdf)
    {
        using var documento = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        var caja = documento.Pages[0].MediaBox;
        return (caja.Width, caja.Height);
    }

    private sealed class FakeTecnico(Usuario usuario) : ICurrentUser
    {
        public long IdUsuario => usuario.IdUsuario;
        public string Nombre => usuario.Nombre;
        public IReadOnlyCollection<string> Roles => ["Operador"];
        public bool IsInRole(string role) => role == "Operador";
    }
}
