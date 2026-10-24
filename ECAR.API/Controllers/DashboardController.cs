using System.Globalization;
using ECAR.API.Services;
using ECAR.API.Services.Documentos;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
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
/// La pantalla (GET) y el PDF (B-03) usan los mismos filtros y el mismo cálculo, así que no pueden discrepar.
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
    private readonly ICurrentUser _currentUser;

    public DashboardController(ECARDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard([FromQuery] DashboardFiltroDto? filtro = null)
    {
        filtro ??= new DashboardFiltroDto();
        var error = Validar(filtro);
        if (error != null)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(error));
        }

        return Ok(ApiResponse<DashboardDto>.SuccessResponse(await CalcularAsync(filtro)));
    }

    /// <summary>El tablero en A4 con los filtros de la pantalla; cada PDF queda en la auditoría.</summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> GetDashboardPdf([FromQuery] DashboardFiltroDto? filtro = null)
    {
        filtro ??= new DashboardFiltroDto();
        var error = Validar(filtro);
        if (error != null)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(error));
        }

        var tablero = await CalcularAsync(filtro);
        var textoFiltros = TextoFiltros(filtro);
        var bytes = TableroPdf.Generar(tablero, textoFiltros, _currentUser.Nombre, tablero.GeneradoEn);

        await _context.RegistrarEventoAsync(AuditoriaAcciones.Exportacion, _currentUser.IdUsuario,
            new { Reporte = ReporteTablero, Formato = ReporteFormatos.Pdf, Filtros = textoFiltros });

        return File(bytes, ExportadorTabla.TipoPdf,
            $"ECAR_{ReporteTablero}_{HoraColombia.Texto(tablero.GeneradoEn, "yyyyMMdd")}.pdf");
    }

    private async Task<DashboardDto> CalcularAsync(DashboardFiltroDto filtro)
    {
        var ahora = DateTime.UtcNow;
        var hoy = HoraColombia.DesdeUtc(ahora).Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var (inicioMesUtc, _) = HoraColombia.RangoUtc(inicioMes, null);

        // Sin filtro de equipo las consultas son las de siempre; con él, inspecciones y novedades
        // se acotan por los equipos que cumplen (activos o no: una inspección no se borra con su equipo).
        var equiposFiltrados = FiltrarEquipos(_context.Equipos.AsNoTracking(), filtro);

        var equipos = await (equiposFiltrados ?? _context.Equipos.AsNoTracking())
            .Where(e => e.Activo)
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

        var inspeccionesDeEquipos = _context.Inspecciones.AsNoTracking();
        var novedades = _context.Novedades.AsNoTracking();
        if (equiposFiltrados != null)
        {
            var idsEquipo = equiposFiltrados.Select(e => e.IdEquipo);
            inspeccionesDeEquipos = inspeccionesDeEquipos.Where(i => idsEquipo.Contains(i.IdEquipo));
            novedades = novedades.Where(n => idsEquipo.Contains(n.Inspeccion.IdEquipo));
        }

        var inspecciones = inspeccionesDeEquipos;
        var (inicio, fin) = HoraColombia.RangoUtc(filtro.Desde, filtro.Hasta);
        if (inicio.HasValue)
        {
            inspecciones = inspecciones.Where(i => i.FechaInspeccion >= inicio.Value);
            novedades = novedades.Where(n => n.FechaRegistro >= inicio.Value);
        }

        if (fin.HasValue)
        {
            inspecciones = inspecciones.Where(i => i.FechaInspeccion < fin.Value);
            novedades = novedades.Where(n => n.FechaRegistro < fin.Value);
        }

        var estadosInspeccion = await inspecciones
            .GroupBy(i => i.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);
        var firmadasMes = await inspeccionesDeEquipos
            .CountAsync(i => (i.Estado == InspeccionEstados.Cerrada || i.Estado == InspeccionEstados.NovedadesPendientes)
                             && i.FechaInspeccion >= inicioMesUtc!.Value);

        var estadosNovedad = await novedades
            .GroupBy(n => n.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);
        var conPendientes = estadosInspeccion.GetValueOrDefault(InspeccionEstados.NovedadesPendientes);

        return new DashboardDto
        {
            TotalEquipos = equipos.Count,
            EquiposPorCriticidad = criticidades,
            EquiposPorArea = areas,
            InspeccionesRealizadas = estadosInspeccion.GetValueOrDefault(InspeccionEstados.Cerrada) + conPendientes,
            InspeccionesRealizadasMes = firmadasMes,
            InspeccionesEnCurso = estadosInspeccion.GetValueOrDefault(InspeccionEstados.EnCurso),
            InspeccionesConNovedadesPendientes = conPendientes,
            NovedadesPendientes = estadosNovedad.GetValueOrDefault(NovedadEstados.Pendiente),
            NovedadesResueltas = estadosNovedad.GetValueOrDefault(NovedadEstados.Resuelta),
            Periodo = inicioMes.ToString("MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-CO")),
            GeneradoEn = ahora
        };
    }

    /// <summary>Los equipos de la planta, el área y la criticidad pedidas; null si no se filtra por equipo.</summary>
    private static IQueryable<Equipo>? FiltrarEquipos(IQueryable<Equipo> equipos, DashboardFiltroDto filtro)
    {
        var planta = filtro.Planta?.Trim();
        var area = filtro.Area?.Trim();
        var criticidad = filtro.Criticidad?.Trim();
        if (string.IsNullOrEmpty(planta) && string.IsNullOrEmpty(area) && string.IsNullOrEmpty(criticidad))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(planta))
            equipos = equipos.Where(e => e.Ubicacion != null && e.Ubicacion.Planta == planta);
        if (!string.IsNullOrEmpty(area))
            equipos = equipos.Where(e => e.Ubicacion != null && e.Ubicacion.Area == area);
        if (!string.IsNullOrEmpty(criticidad))
            equipos = equipos.Where(e => e.Criticidad == criticidad);
        return equipos;
    }

    private static string? Validar(DashboardFiltroDto filtro)
    {
        if (filtro.Desde.HasValue && filtro.Hasta.HasValue && filtro.Desde.Value.Date > filtro.Hasta.Value.Date)
            return "La fecha «Desde» no puede ser posterior a «Hasta»";
        if (!Criticidades.EsValida(filtro.Criticidad))
            return Criticidades.Mensaje;
        return null;
    }

    /// <summary>"desde 01/10/2026, planta Planta 1, criticidad Alta" o "sin filtros", para el pie y la auditoría.</summary>
    internal static string TextoFiltros(DashboardFiltroDto f)
    {
        var partes = new List<string>();
        if (f.Desde.HasValue) partes.Add($"desde {f.Desde:dd/MM/yyyy}");
        if (f.Hasta.HasValue) partes.Add($"hasta {f.Hasta:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(f.Planta)) partes.Add($"planta {f.Planta.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.Area)) partes.Add($"área {f.Area.Trim()}");
        if (!string.IsNullOrWhiteSpace(f.Criticidad)) partes.Add($"criticidad {f.Criticidad.Trim()}");
        return partes.Count == 0 ? "sin filtros" : string.Join(", ", partes);
    }
}
