using System.Globalization;
using ECAR.API.Services;
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
/// ven los tres roles. Una inspección con novedades pendientes ya está firmada: cuenta como realizada.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class DashboardController : ControllerBase
{
    private const string SinCriticidad = "Sin criticidad";
    private const string SinUbicacion = "Sin ubicación";

    private readonly ECARDbContext _context;

    public DashboardController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard()
    {
        var ahora = DateTime.UtcNow;
        var hoy = HoraColombia.DesdeUtc(ahora).Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioTrimestre = new DateTime(hoy.Year, (hoy.Month - 1) / 3 * 3 + 1, 1);
        var (inicioMesUtc, _) = HoraColombia.RangoUtc(inicioMes, null);
        var (inicioTrimestreUtc, _) = HoraColombia.RangoUtc(inicioTrimestre, null);

        var equipos = await _context.Equipos.AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => new
            {
                e.IdEquipo,
                e.Criticidad,
                e.FrecuenciaInspeccion,
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

        // Periodo vigente de cada equipo programado: el mes, o el trimestre si es trimestral (la misma
        // ventana que usa el reporte de cumplimiento).
        var programados = equipos.Where(e => e.FrecuenciaInspeccion != null).ToList();
        var idsProgramados = programados.Select(e => e.IdEquipo).ToList();
        var firmadasDelTrimestre = await _context.Inspecciones.AsNoTracking()
            .Where(i => (i.Estado == InspeccionEstados.Cerrada || i.Estado == InspeccionEstados.NovedadesPendientes)
                        && i.FechaInspeccion >= inicioTrimestreUtc!.Value
                        && idsProgramados.Contains(i.IdEquipo))
            .Select(i => new { i.IdEquipo, i.FechaInspeccion })
            .ToListAsync();
        var pendientes = programados.Count(e =>
        {
            var desde = e.FrecuenciaInspeccion == FrecuenciasInspeccion.Trimestral ? inicioTrimestreUtc!.Value : inicioMesUtc!.Value;
            return !firmadasDelTrimestre.Any(i => i.IdEquipo == e.IdEquipo && i.FechaInspeccion >= desde);
        });

        var estadosInspeccion = await _context.Inspecciones.AsNoTracking()
            .GroupBy(i => i.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);
        var firmadasMes = await _context.Inspecciones.AsNoTracking()
            .CountAsync(i => (i.Estado == InspeccionEstados.Cerrada || i.Estado == InspeccionEstados.NovedadesPendientes)
                             && i.FechaInspeccion >= inicioMesUtc!.Value);

        var estadosNovedad = await _context.Novedades.AsNoTracking()
            .GroupBy(n => n.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);
        var conPendientes = estadosInspeccion.GetValueOrDefault(InspeccionEstados.NovedadesPendientes);

        var dto = new DashboardDto
        {
            TotalEquipos = equipos.Count,
            EquiposPorCriticidad = criticidades,
            EquiposPorArea = areas,
            InspeccionesRealizadas = estadosInspeccion.GetValueOrDefault(InspeccionEstados.Cerrada) + conPendientes,
            InspeccionesRealizadasMes = firmadasMes,
            InspeccionesEnCurso = estadosInspeccion.GetValueOrDefault(InspeccionEstados.EnCurso),
            InspeccionesPendientes = pendientes,
            EquiposProgramados = programados.Count,
            InspeccionesConNovedadesPendientes = conPendientes,
            NovedadesPendientes = estadosNovedad.GetValueOrDefault(NovedadEstados.Pendiente),
            NovedadesResueltas = estadosNovedad.GetValueOrDefault(NovedadEstados.Resuelta),
            Periodo = inicioMes.ToString("MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-CO")),
            GeneradoEn = ahora
        };

        return Ok(ApiResponse<DashboardDto>.SuccessResponse(dto));
    }
}
