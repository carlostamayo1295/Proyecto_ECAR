using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EvidenciasController : ControllerBase
{
    private readonly ECARDbContext _context;

    public EvidenciasController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EvidenciaDto>>>> GetEvidencias([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] long? idInspeccion = null)
    {
        var query = _context.Evidencias
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .AsQueryable();

        if (idInspeccion.HasValue)
        {
            query = query.Where(e => e.IdInspeccion == idInspeccion.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(e =>
                e.Inspeccion.Equipo.NombreEquipo.Contains(search) ||
                e.UsuarioCarga.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var evidencias = await query
            .OrderByDescending(e => e.FechaCarga)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EvidenciaDto
            {
                IdEvidencia = e.IdEvidencia,
                IdInspeccion = e.IdInspeccion,
                NombreEquipo = e.Inspeccion.Equipo.NombreEquipo,
                Archivo = e.Archivo,
                FechaCarga = e.FechaCarga,
                UsuarioCarga = e.UsuarioCarga
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<EvidenciaDto>
        {
            Data = evidencias,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<EvidenciaDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EvidenciaDto>>> GetEvidencia(long id)
    {
        var evidencia = await _context.Evidencias
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(e => e.IdEvidencia == id);

        if (evidencia == null)
        {
            return NotFound(ApiResponse<EvidenciaDto>.ErrorResponse("Evidencia no encontrada"));
        }

        var evidenciaDto = new EvidenciaDto
        {
            IdEvidencia = evidencia.IdEvidencia,
            IdInspeccion = evidencia.IdInspeccion,
            NombreEquipo = evidencia.Inspeccion?.Equipo?.NombreEquipo,
            Archivo = evidencia.Archivo,
            FechaCarga = evidencia.FechaCarga,
            UsuarioCarga = evidencia.UsuarioCarga
        };

        return Ok(ApiResponse<EvidenciaDto>.SuccessResponse(evidenciaDto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EvidenciaDto>>> CreateEvidencia(CreateEvidenciaDto createDto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .FirstOrDefaultAsync(i => i.IdInspeccion == createDto.IdInspeccion);

        if (inspeccion == null)
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("La inspección indicada no existe"));
        }

        var evidencia = new Evidencia
        {
            IdInspeccion = createDto.IdInspeccion,
            Archivo = createDto.Archivo,
            UsuarioCarga = createDto.UsuarioCarga,
            FechaCarga = DateTime.UtcNow
        };

        _context.Evidencias.Add(evidencia);
        await _context.SaveChangesAsync();

        var evidenciaDto = new EvidenciaDto
        {
            IdEvidencia = evidencia.IdEvidencia,
            IdInspeccion = evidencia.IdInspeccion,
            NombreEquipo = inspeccion.Equipo?.NombreEquipo,
            Archivo = evidencia.Archivo,
            FechaCarga = evidencia.FechaCarga,
            UsuarioCarga = evidencia.UsuarioCarga
        };

        return CreatedAtAction(nameof(GetEvidencia), new { id = evidencia.IdEvidencia },
            ApiResponse<EvidenciaDto>.SuccessResponse(evidenciaDto, "Evidencia cargada exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEvidencia(long id)
    {
        var evidencia = await _context.Evidencias.FindAsync(id);

        if (evidencia == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Evidencia no encontrada"));
        }

        _context.Evidencias.Remove(evidencia);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Evidencia eliminada exitosamente"));
    }
}
