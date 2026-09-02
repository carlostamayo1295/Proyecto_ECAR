using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InspeccionesController : ControllerBase
{
    private readonly ECARDbContext _context;

    public InspeccionesController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<InspeccionDto>>>> GetInspecciones([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        var query = _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(i =>
                i.Equipo.NombreEquipo.Contains(search) ||
                i.Usuario.Nombre.Contains(search) ||
                (i.Resultado != null && i.Resultado.Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var inspecciones = await query
            .OrderByDescending(i => i.FechaInspeccion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InspeccionDto
            {
                IdInspeccion = i.IdInspeccion,
                IdEquipo = i.IdEquipo,
                NombreEquipo = i.Equipo.NombreEquipo,
                IdUsuario = i.IdUsuario,
                NombreUsuario = i.Usuario.Nombre,
                FechaInspeccion = i.FechaInspeccion,
                Resultado = i.Resultado,
                Observaciones = i.Observaciones,
                TieneFirma = i.FirmaDigital != null && i.FirmaDigital != "",
                TotalEvidencias = i.Evidencias.Count,
                TotalHallazgos = i.Hallazgos.Count
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<InspeccionDto>
        {
            Data = inspecciones,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<InspeccionDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> GetInspeccion(long id)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Evidencias)
            .Include(i => i.Hallazgos)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> CreateInspeccion(CreateInspeccionDto createDto)
    {
        var equipo = await _context.Equipos.FindAsync(createDto.IdEquipo);
        if (equipo == null)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("El equipo indicado no existe"));
        }

        var usuario = await _context.Usuarios.FindAsync(createDto.IdUsuario);
        if (usuario == null)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("El usuario indicado no existe"));
        }

        // Regla de negocio: si existe novedad, la observación es obligatoria
        if (!string.IsNullOrWhiteSpace(createDto.Resultado)
            && createDto.Resultado.Contains("novedad", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(createDto.Observaciones))
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("Si la inspección tiene novedad, las observaciones son obligatorias"));
        }

        var inspeccion = new Inspeccion
        {
            IdEquipo = createDto.IdEquipo,
            IdUsuario = createDto.IdUsuario,
            FechaInspeccion = createDto.FechaInspeccion,
            Resultado = createDto.Resultado,
            Observaciones = createDto.Observaciones,
            FirmaDigital = createDto.FirmaDigital
        };

        _context.Inspecciones.Add(inspeccion);
        await _context.SaveChangesAsync();

        await _context.Entry(inspeccion).Reference(i => i.Equipo).LoadAsync();
        await _context.Entry(inspeccion).Reference(i => i.Usuario).LoadAsync();

        return CreatedAtAction(nameof(GetInspeccion), new { id = inspeccion.IdInspeccion },
            ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion), "Inspección registrada exitosamente"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> UpdateInspeccion(long id, UpdateInspeccionDto updateDto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Evidencias)
            .Include(i => i.Hallazgos)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (updateDto.Resultado != null)
            inspeccion.Resultado = updateDto.Resultado;

        if (updateDto.Observaciones != null)
            inspeccion.Observaciones = updateDto.Observaciones;

        if (updateDto.FirmaDigital != null)
            inspeccion.FirmaDigital = updateDto.FirmaDigital;

        var resultado = inspeccion.Resultado;
        if (!string.IsNullOrWhiteSpace(resultado)
            && resultado.Contains("novedad", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(inspeccion.Observaciones))
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("Si la inspección tiene novedad, las observaciones son obligatorias"));
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion), "Inspección actualizada exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteInspeccion(long id)
    {
        var inspeccion = await _context.Inspecciones.FindAsync(id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Inspección no encontrada"));
        }

        _context.Inspecciones.Remove(inspeccion);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Inspección eliminada exitosamente"));
    }

    private static InspeccionDto MapToDto(Inspeccion i)
    {
        return new InspeccionDto
        {
            IdInspeccion = i.IdInspeccion,
            IdEquipo = i.IdEquipo,
            NombreEquipo = i.Equipo?.NombreEquipo,
            IdUsuario = i.IdUsuario,
            NombreUsuario = i.Usuario?.Nombre,
            FechaInspeccion = i.FechaInspeccion,
            Resultado = i.Resultado,
            Observaciones = i.Observaciones,
            TieneFirma = !string.IsNullOrEmpty(i.FirmaDigital),
            TotalEvidencias = i.Evidencias?.Count ?? 0,
            TotalHallazgos = i.Hallazgos?.Count ?? 0
        };
    }
}
