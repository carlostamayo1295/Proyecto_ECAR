using System.Globalization;
using ECAR.API.Services;
using ECAR.API.Services.Documentos;
using ECAR.Infrastructure.Data;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

/// <summary>
/// Indicadores de la portada (SRS §6 "Dashboard"): equipos, inspecciones y novedades. Son conteos
/// sin datos personales, así que los
/// ven todos los roles. Una inspección con novedades pendientes ya está firmada: cuenta como realizada.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RolesSistema.Todos)]
public class DashboardController : ControllerBase
{
    private const string SinCriticidad = "Sin criticidad";
    private const string SinUbicacion = "Sin ubicación";

    /// <summary>Hasta tres meses la tendencia va por semana (unas 13); en un periodo más largo, por mes.</summary>
    internal const int DiasMaximosPorSemana = 92;

    /// <summary>Valor de "Reporte" en la auditoría de la exportación.</summary>
    public const string ReporteTablero = "tablero";

    private readonly ECARDbContext _context;

    public DashboardController(ECARDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Sin fechas, el mes en curso. Con <paramref name="total"/> («Total general», 30/10/2026), todo el historial
    /// y las fechas no cuentan.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard(
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null,
        [FromQuery(Name = "planta")] List<string>? plantas = null, [FromQuery] bool total = false)
    {
        var (dto, error) = await CalcularAsync(desde, hasta, plantas, total);
        if (error != null)
            return BadRequest(ApiResponse<DashboardDto>.ErrorResponse(error));

        return Ok(ApiResponse<DashboardDto>.SuccessResponse(dto!));
    }

    /// <summary>
    /// El tablero en A4 (B-03) con los mismos filtros y el mismo cálculo que la pantalla (B-01), así
    /// que no pueden discrepar. Cada PDF queda en la auditoría como Exportacion.
    /// </summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> GetDashboardPdf([FromServices] ICurrentUser currentUser,
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null,
        [FromQuery(Name = "planta")] List<string>? plantas = null, [FromQuery] bool total = false)
    {
        var (tablero, error) = await CalcularAsync(desde, hasta, plantas, total);
        if (error != null)
            return BadRequest(ApiResponse<DashboardDto>.ErrorResponse(error));

        var filtros = TextoFiltros(tablero!.Filtros);
        var bytes = TableroPdf.Generar(tablero, filtros, currentUser.Nombre, tablero.GeneradoEn);

        await _context.RegistrarEventoAsync(AuditoriaAcciones.Exportacion, currentUser.IdUsuario,
            new { Reporte = ReporteTablero, Formato = ReporteFormatos.Pdf, Filtros = filtros });

        return File(bytes, ExportadorTabla.TipoPdf,
            $"ECAR_{ReporteTablero}_{tablero.Filtros.Desde:yyyyMMdd}_{tablero.Filtros.Hasta:yyyyMMdd}.pdf");
    }

    private async Task<(DashboardDto? Tablero, string? Error)> CalcularAsync(DateTime? desde, DateTime? hasta,
        List<string>? plantasPedidas, bool total)
    {
        var ahora = DateTime.UtcNow;
        var hoy = HoraColombia.DesdeUtc(ahora).Date;
        // Varias plantas a la vez (29/10/2026): cuenta lo de cualquiera de ellas. Sin plantas, todas.
        var plantas = ReporteService.Valores(plantasPedidas);
        var (desdeAplicado, hastaAplicado, error) = await PeriodoAsync(desde, hasta, total, plantas, hoy);
        if (error != null)
            return (null, error);

        var (inicioUtc, finUtc) = HoraColombia.RangoUtc(desdeAplicado, hastaAplicado);
        var inicio = inicioUtc!.Value;
        var fin = finUtc!.Value;

        var equiposQuery = _context.Equipos.AsNoTracking().Where(e => e.Activo);
        if (plantas.Count > 0)
            equiposQuery = equiposQuery.Where(e => e.Ubicacion != null && plantas.Contains(e.Ubicacion.Planta));

        var equipos = await equiposQuery
            .Select(e => new
            {
                e.IdEquipo,
                e.Criticidad,
                Area = e.Ubicacion != null ? e.Ubicacion.Planta + " - " + e.Ubicacion.Area : SinUbicacion
            })
            .ToListAsync();

        var porCriticidad = equipos
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Criticidad) ? SinCriticidad : e.Criticidad)
            .ToDictionary(g => g.Key, g => g.Count());
        var criticidades = Criticidades.Todas.Append(SinCriticidad)
            .Select(c => new ConteoDto { Etiqueta = c, Cantidad = porCriticidad.GetValueOrDefault(c) })
            .Where(c => c.Cantidad > 0 || c.Etiqueta != SinCriticidad)
            .ToList();

        var areas = equipos
            .GroupBy(e => e.Area)
            .Select(g => new ConteoDto { Etiqueta = g.Key, Cantidad = g.Count() })
            .OrderByDescending(c => c.Cantidad)
            .ThenBy(c => c.Etiqueta)
            .ToList();

        var inspeccionesQuery = _context.Inspecciones.AsNoTracking()
            .Where(i => i.FechaInspeccion >= inicio && i.FechaInspeccion < fin);
        if (plantas.Count > 0)
            inspeccionesQuery = inspeccionesQuery.Where(i =>
                i.Equipo.Ubicacion != null && plantas.Contains(i.Equipo.Ubicacion.Planta));

        var estadosInspeccion = await inspeccionesQuery
            .GroupBy(i => i.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);

        var equiposInspeccionados = await inspeccionesQuery
            .Where(i => i.Equipo.Activo &&
                        (i.Estado == InspeccionEstados.Cerrada || i.Estado == InspeccionEstados.NovedadesPendientes))
            .Select(i => i.IdEquipo)
            .Distinct()
            .CountAsync();

        var firmadasTotalesQuery = _context.Inspecciones.AsNoTracking()
            .Where(i => i.Estado == InspeccionEstados.Cerrada ||
                        i.Estado == InspeccionEstados.NovedadesPendientes);
        if (plantas.Count > 0)
            firmadasTotalesQuery = firmadasTotalesQuery.Where(i =>
                i.Equipo.Ubicacion != null && plantas.Contains(i.Equipo.Ubicacion.Planta));
        var firmadasTotales = await firmadasTotalesQuery.CountAsync();

        var novedadesQuery = _context.Novedades.AsNoTracking();
        if (plantas.Count > 0)
            novedadesQuery = novedadesQuery.Where(n =>
                n.Inspeccion.Equipo.Ubicacion != null && plantas.Contains(n.Inspeccion.Equipo.Ubicacion.Planta));

        var novedadesPendientes = await novedadesQuery.CountAsync(n =>
            n.Estado == NovedadEstados.Pendiente &&
            n.FechaRegistro >= inicio && n.FechaRegistro < fin);
        var novedadesResueltas = await novedadesQuery.CountAsync(n =>
            n.Estado == NovedadEstados.Resuelta && n.FechaResolucion.HasValue &&
            n.FechaResolucion.Value >= inicio && n.FechaResolucion.Value < fin);
        var conPendientes = estadosInspeccion.GetValueOrDefault(InspeccionEstados.NovedadesPendientes);
        var firmadas = estadosInspeccion.GetValueOrDefault(InspeccionEstados.Cerrada) + conPendientes;

        var dto = new DashboardDto
        {
            Filtros = new DashboardFiltroAplicadoDto
            {
                Desde = desdeAplicado,
                Hasta = hastaAplicado,
                Plantas = plantas,
                Total = total
            },
            TotalEquipos = equipos.Count,
            EquiposInspeccionados = equiposInspeccionados,
            EquiposPorCriticidad = criticidades,
            EquiposPorArea = areas,
            InspeccionesRealizadas = firmadasTotales,
            // Se conserva el campo para no romper el frontend existente; desde Fase 5 representa
            // las inspecciones realizadas en el periodo aplicado, no necesariamente un mes.
            InspeccionesRealizadasMes = firmadas,
            InspeccionesEnCurso = estadosInspeccion.GetValueOrDefault(InspeccionEstados.EnCurso),
            InspeccionesConNovedadesPendientes = conPendientes,
            NovedadesPendientes = novedadesPendientes,
            NovedadesResueltas = novedadesResueltas,
            Periodo = total
                ? $"Total general (del {desdeAplicado:dd/MM/yyyy} al {hastaAplicado:dd/MM/yyyy})"
                : TextoPeriodo(desdeAplicado, hastaAplicado, hoy),
            GeneradoEn = ahora
        };

        return (dto, null);
    }

    /// <summary>
    /// Tendencia del tablero. Con <paramref name="desde"/>, <paramref name="hasta"/> o <paramref name="total"/>
    /// (30/10/2026) sigue el periodo del tablero, con sus mismas reglas: por semana hasta 92 días y por mes en un
    /// periodo más largo o en el total general. Sin ellos, las últimas <paramref name="semanas"/> semanas, incluida
    /// la actual. Las semanas van de lunes a domingo en hora de Colombia; las fechas UTC usan un rango [inicio, fin).
    /// </summary>
    [HttpGet("tendencia")]
    public async Task<ActionResult<ApiResponse<DashboardTendenciaDto>>> GetTendencia(
        [FromQuery] int semanas = 8, [FromQuery(Name = "planta")] List<string>? plantasPedidas = null,
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null, [FromQuery] bool total = false)
    {
        var plantas = ReporteService.Valores(plantasPedidas);
        var hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;
        DateTime desdeLocal;
        DateTime hastaLocal;
        bool porMes;
        if (total || desde.HasValue || hasta.HasValue)
        {
            var (desdeAplicado, hastaAplicado, error) = await PeriodoAsync(desde, hasta, total, plantas, hoy);
            if (error != null)
                return BadRequest(ApiResponse<DashboardTendenciaDto>.ErrorResponse(error));

            desdeLocal = desdeAplicado;
            hastaLocal = hastaAplicado;
            porMes = total || (hastaLocal - desdeLocal).Days + 1 > DiasMaximosPorSemana;
        }
        else
        {
            if (semanas is < 1 or > 52)
            {
                return BadRequest(ApiResponse<DashboardTendenciaDto>.ErrorResponse(
                    "La cantidad de semanas debe estar entre 1 y 52"));
            }

            var lunesActual = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
            desdeLocal = lunesActual.AddDays(-7 * (semanas - 1));
            hastaLocal = lunesActual.AddDays(6);
            porMes = false;
        }

        var (inicioUtc, finUtc) = HoraColombia.RangoUtc(desdeLocal, hastaLocal);
        var inicio = inicioUtc!.Value;
        var fin = finUtc!.Value;

        var inspeccionesQuery = _context.Inspecciones.AsNoTracking()
            .Where(i => i.FechaCierre.HasValue && i.FechaCierre.Value >= inicio && i.FechaCierre.Value < fin &&
                        (i.Estado == InspeccionEstados.Cerrada ||
                         i.Estado == InspeccionEstados.NovedadesPendientes));
        var novedadesQuery = _context.Novedades.AsNoTracking()
            .Where(n => n.FechaRegistro >= inicio && n.FechaRegistro < fin ||
                        n.FechaResolucion.HasValue && n.FechaResolucion.Value >= inicio && n.FechaResolucion.Value < fin);

        if (plantas.Count > 0)
        {
            inspeccionesQuery = inspeccionesQuery.Where(i =>
                i.Equipo.Ubicacion != null && plantas.Contains(i.Equipo.Ubicacion.Planta));
            novedadesQuery = novedadesQuery.Where(n =>
                n.Inspeccion.Equipo.Ubicacion != null && plantas.Contains(n.Inspeccion.Equipo.Ubicacion.Planta));
        }

        var cierres = await inspeccionesQuery.Select(i => i.FechaCierre!.Value).ToListAsync();
        var novedades = await novedadesQuery
            .Select(n => new { n.FechaRegistro, n.FechaResolucion })
            .ToListAsync();

        var datos = Tramos(desdeLocal, hastaLocal, porMes)
            .Select(tramo =>
            {
                var fin = tramo.Hasta.AddDays(1);
                return new DashboardSemanaDto
                {
                    Desde = tramo.Desde,
                    Hasta = tramo.Hasta,
                    InspeccionesFirmadas = cierres.Count(f => EstaEnTramo(f, tramo.Desde, fin)),
                    NovedadesRegistradas = novedades.Count(n => EstaEnTramo(n.FechaRegistro, tramo.Desde, fin)),
                    NovedadesResueltas = novedades.Count(n => n.FechaResolucion.HasValue &&
                        EstaEnTramo(n.FechaResolucion.Value, tramo.Desde, fin))
                };
            })
            .ToList();

        return Ok(ApiResponse<DashboardTendenciaDto>.SuccessResponse(new DashboardTendenciaDto
        {
            Semanas = datos.Count,
            Desde = desdeLocal,
            Hasta = hastaLocal,
            Plantas = plantas,
            Agrupacion = porMes ? TendenciaAgrupaciones.Mes : TendenciaAgrupaciones.Semana,
            Total = total,
            Datos = datos
        }));
    }

    /// <summary>
    /// Días de Colombia que se aplican. Sin fechas, el mes en curso hasta hoy. Con <paramref name="total"/>, desde
    /// el primer día del mes de la primera inspección de las plantas elegidas hasta hoy, sin el límite de un año.
    /// </summary>
    private async Task<(DateTime Desde, DateTime Hasta, string? Error)> PeriodoAsync(DateTime? desde, DateTime? hasta,
        bool total, List<string> plantas, DateTime hoy)
    {
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        if (total)
        {
            var inspecciones = _context.Inspecciones.AsNoTracking();
            if (plantas.Count > 0)
                inspecciones = inspecciones.Where(i =>
                    i.Equipo.Ubicacion != null && plantas.Contains(i.Equipo.Ubicacion.Planta));

            var primera = await inspecciones.MinAsync(i => (DateTime?)i.FechaInspeccion);
            var dia = primera is { } utc ? HoraColombia.DesdeUtc(utc).Date : inicioMes;
            var mesPrimera = new DateTime(dia.Year, dia.Month, 1);
            return (mesPrimera < inicioMes ? mesPrimera : inicioMes, hoy, null);
        }

        var desdeAplicado = desde?.Date ?? inicioMes;
        var hastaAplicado = hasta?.Date ?? hoy;
        return (desdeAplicado, hastaAplicado, ValidarRango(desdeAplicado, hastaAplicado));
    }

    /// <summary>
    /// Tramos de la tendencia en días de Colombia: semanas de lunes a domingo o meses, recortados a
    /// [<paramref name="desde"/>, <paramref name="hasta"/>], así que el primero y el último pueden ser más cortos.
    /// </summary>
    internal static List<(DateTime Desde, DateTime Hasta)> Tramos(DateTime desde, DateTime hasta, bool porMes)
    {
        var tramos = new List<(DateTime Desde, DateTime Hasta)>();
        for (var inicio = desde.Date; inicio <= hasta.Date;)
        {
            var siguiente = porMes
                ? new DateTime(inicio.Year, inicio.Month, 1).AddMonths(1)
                : inicio.AddDays(7 - ((int)inicio.DayOfWeek + 6) % 7);
            var fin = siguiente.AddDays(-1);
            tramos.Add((inicio, fin < hasta.Date ? fin : hasta.Date));
            inicio = siguiente;
        }

        return tramos;
    }

    /// <summary>"todas las plantas", "planta Norte" o "plantas Norte, Sur y Este".</summary>
    internal static string TextoPlantas(IReadOnlyList<string> plantas) => plantas.Count switch
    {
        0 => "todas las plantas",
        1 => $"planta {plantas[0]}",
        _ => $"plantas {string.Join(", ", plantas.Take(plantas.Count - 1))} y {plantas[^1]}"
    };

    private static string? ValidarRango(DateTime desde, DateTime hasta)
    {
        if (desde > hasta)
            return "La fecha «Desde» no puede ser posterior a «Hasta»";
        if (hasta > desde.AddYears(1))
            return "El rango no puede superar un año";
        return null;
    }

    /// <summary>
    /// "desde 01/10/2026, hasta 24/10/2026, planta Planta 1" (o "plantas Norte y Sur", o "todas las plantas"),
    /// para el pie del PDF y la auditoría. El total general empieza con "total general, ".
    /// </summary>
    internal static string TextoFiltros(DashboardFiltroAplicadoDto filtros)
    {
        var texto = $"desde {filtros.Desde.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}, " +
                    $"hasta {filtros.Hasta.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
        return (filtros.Total ? "total general, " : string.Empty) + texto + ", " + TextoPlantas(filtros.Plantas);
    }

    /// <summary>
    /// «octubre de 2026» para un mes entero o el mes en curso hasta hoy; si no, el rango. Antes cualquier
    /// rango dentro de un mes (del 05/10 al 10/10) salía como el mes completo.
    /// </summary>
    internal static string TextoPeriodo(DateTime desde, DateTime hasta, DateTime hoy)
    {
        var cultura = CultureInfo.GetCultureInfo("es-CO");
        var mesEntero = desde.Day == 1 && desde.Year == hasta.Year && desde.Month == hasta.Month
                        && (hasta.AddDays(1).Day == 1 || hasta == hoy);
        if (mesEntero)
            return desde.ToString("MMMM 'de' yyyy", cultura);

        return $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";
    }

    private static bool EstaEnTramo(DateTime utc, DateTime desde, DateTime finExclusivo)
    {
        var local = HoraColombia.DesdeUtc(utc);
        return local >= desde && local < finExclusivo;
    }
}
