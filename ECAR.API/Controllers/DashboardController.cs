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

    /// <summary>Valor de "Reporte" en la auditoría de la exportación.</summary>
    public const string ReporteTablero = "tablero";

    private readonly ECARDbContext _context;

    public DashboardController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard(
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null,
        [FromQuery] string? planta = null)
    {
        var (dto, error) = await CalcularAsync(desde, hasta, planta);
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
        [FromQuery] string? planta = null)
    {
        var (tablero, error) = await CalcularAsync(desde, hasta, planta);
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
        string? planta)
    {
        var ahora = DateTime.UtcNow;
        var hoy = HoraColombia.DesdeUtc(ahora).Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var desdeAplicado = desde?.Date ?? inicioMes;
        var hastaAplicado = hasta?.Date ?? hoy;
        var error = ValidarRango(desdeAplicado, hastaAplicado);
        if (error != null)
            return (null, error);

        planta = string.IsNullOrWhiteSpace(planta) ? null : planta.Trim();
        var (inicioUtc, finUtc) = HoraColombia.RangoUtc(desdeAplicado, hastaAplicado);
        var inicio = inicioUtc!.Value;
        var fin = finUtc!.Value;

        var equiposQuery = _context.Equipos.AsNoTracking().Where(e => e.Activo);
        if (planta != null)
            equiposQuery = equiposQuery.Where(e => e.Ubicacion != null && e.Ubicacion.Planta == planta);

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
        if (planta != null)
            inspeccionesQuery = inspeccionesQuery.Where(i =>
                i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Planta == planta);

        var estadosInspeccion = await inspeccionesQuery
            .GroupBy(i => i.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);

        var firmadasTotalesQuery = _context.Inspecciones.AsNoTracking()
            .Where(i => i.Estado == InspeccionEstados.Cerrada ||
                        i.Estado == InspeccionEstados.NovedadesPendientes);
        if (planta != null)
            firmadasTotalesQuery = firmadasTotalesQuery.Where(i =>
                i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Planta == planta);
        var firmadasTotales = await firmadasTotalesQuery.CountAsync();

        var novedadesQuery = _context.Novedades.AsNoTracking();
        if (planta != null)
            novedadesQuery = novedadesQuery.Where(n =>
                n.Inspeccion.Equipo.Ubicacion != null && n.Inspeccion.Equipo.Ubicacion.Planta == planta);

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
                Planta = planta
            },
            TotalEquipos = equipos.Count,
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
            Periodo = TextoPeriodo(desdeAplicado, hastaAplicado),
            GeneradoEn = ahora
        };

        return (dto, null);
    }

    /// <summary>
    /// Tendencia de las últimas semanas, incluida la actual. Cada semana empieza el lunes a las
    /// 00:00 y termina el domingo en hora de Colombia; las fechas UTC usan un rango [inicio, fin).
    /// </summary>
    [HttpGet("tendencia")]
    public async Task<ActionResult<ApiResponse<DashboardTendenciaDto>>> GetTendencia(
        [FromQuery] int semanas = 8, [FromQuery] string? planta = null)
    {
        if (semanas is < 1 or > 52)
        {
            return BadRequest(ApiResponse<DashboardTendenciaDto>.ErrorResponse(
                "La cantidad de semanas debe estar entre 1 y 52"));
        }

        planta = string.IsNullOrWhiteSpace(planta) ? null : planta.Trim();
        var hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;
        var lunesActual = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        var desdeLocal = lunesActual.AddDays(-7 * (semanas - 1));
        var hastaLocal = lunesActual.AddDays(6);
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

        if (planta != null)
        {
            inspeccionesQuery = inspeccionesQuery.Where(i =>
                i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Planta == planta);
            novedadesQuery = novedadesQuery.Where(n =>
                n.Inspeccion.Equipo.Ubicacion != null && n.Inspeccion.Equipo.Ubicacion.Planta == planta);
        }

        var cierres = await inspeccionesQuery.Select(i => i.FechaCierre!.Value).ToListAsync();
        var novedades = await novedadesQuery
            .Select(n => new { n.FechaRegistro, n.FechaResolucion })
            .ToListAsync();

        var datos = Enumerable.Range(0, semanas)
            .Select(indice =>
            {
                var lunes = desdeLocal.AddDays(indice * 7);
                var siguienteLunes = lunes.AddDays(7);
                return new DashboardSemanaDto
                {
                    Desde = lunes,
                    Hasta = siguienteLunes.AddDays(-1),
                    InspeccionesFirmadas = cierres.Count(f => EstaEnSemana(f, lunes, siguienteLunes)),
                    NovedadesRegistradas = novedades.Count(n => EstaEnSemana(n.FechaRegistro, lunes, siguienteLunes)),
                    NovedadesResueltas = novedades.Count(n => n.FechaResolucion.HasValue &&
                        EstaEnSemana(n.FechaResolucion.Value, lunes, siguienteLunes))
                };
            })
            .ToList();

        return Ok(ApiResponse<DashboardTendenciaDto>.SuccessResponse(new DashboardTendenciaDto
        {
            Semanas = semanas,
            Desde = desdeLocal,
            Hasta = hastaLocal,
            Planta = planta,
            Datos = datos
        }));
    }

    private static string? ValidarRango(DateTime desde, DateTime hasta)
    {
        if (desde > hasta)
            return "La fecha «Desde» no puede ser posterior a «Hasta»";
        if (hasta > desde.AddYears(1))
            return "El rango no puede superar un año";
        return null;
    }

    /// <summary>"desde 01/10/2026, hasta 24/10/2026, planta Planta 1", para el pie del PDF y la auditoría.</summary>
    internal static string TextoFiltros(DashboardFiltroAplicadoDto filtros)
    {
        var texto = $"desde {filtros.Desde.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}, " +
                    $"hasta {filtros.Hasta.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
        return filtros.Planta == null ? texto + ", todas las plantas" : texto + $", planta {filtros.Planta}";
    }

    private static string TextoPeriodo(DateTime desde, DateTime hasta)
    {
        var cultura = CultureInfo.GetCultureInfo("es-CO");
        if (desde.Year == hasta.Year && desde.Month == hasta.Month)
            return desde.ToString("MMMM 'de' yyyy", cultura);

        return $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";
    }

    private static bool EstaEnSemana(DateTime utc, DateTime lunes, DateTime siguienteLunes)
    {
        var local = HoraColombia.DesdeUtc(utc);
        return local >= lunes && local < siguienteLunes;
    }
}
