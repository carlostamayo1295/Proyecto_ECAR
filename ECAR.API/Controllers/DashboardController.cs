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
/// ven todos los roles. Una inspección con novedades pendientes ya está firmada: cuenta como realizada.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RolesSistema.Todos)]
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
        var (inicioMesUtc, _) = HoraColombia.RangoUtc(inicioMes, null);

        var equipos = await _context.Equipos.AsNoTracking()
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
            InspeccionesConNovedadesPendientes = conPendientes,
            NovedadesPendientes = estadosNovedad.GetValueOrDefault(NovedadEstados.Pendiente),
            NovedadesResueltas = estadosNovedad.GetValueOrDefault(NovedadEstados.Resuelta),
            Periodo = inicioMes.ToString("MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-CO")),
            GeneradoEn = ahora
        };

        return Ok(ApiResponse<DashboardDto>.SuccessResponse(dto));
    }
}
