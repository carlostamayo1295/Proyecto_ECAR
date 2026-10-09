using ECAR.Infrastructure.Data;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class HallazgosController : ControllerBase
{
    private readonly ECARDbContext _context;

    public HallazgosController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<HallazgoDto>>>> GetHallazgos(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? estado = null,
        [FromQuery] string? criticidad = null,
        [FromQuery] long? idInspeccion = null)
    {
        var query = _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .Where(h => !h.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(estado))
        {
            query = query.Where(h => h.Estado == estado);
        }

        if (!string.IsNullOrEmpty(criticidad))
        {
            query = query.Where(h => h.Criticidad == criticidad);
        }

        if (idInspeccion.HasValue)
        {
            query = query.Where(h => h.IdInspeccion == idInspeccion.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(h => h.FechaRegistro)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new HallazgoDto
            {
                IdHallazgo = h.IdHallazgo,
                IdInspeccion = h.IdInspeccion,
                IdPregunta = h.IdPregunta,
                Descripcion = h.Descripcion,
                Criticidad = h.Criticidad,
                Estado = h.Estado,
                FechaRegistro = h.FechaRegistro
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<HallazgoDto>
        {
            Data = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<HallazgoDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<HallazgoDetalleDto>>> GetHallazgoDetalle(long id)
    {
        var hallazgo = await _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Usuario)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id && !h.IsDeleted);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDetalleDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        var dto = new HallazgoDetalleDto
        {
            IdHallazgo = hallazgo.IdHallazgo,
            IdInspeccion = hallazgo.IdInspeccion,
            IdPregunta = hallazgo.IdPregunta,
            Descripcion = hallazgo.Descripcion,
            Criticidad = hallazgo.Criticidad,
            Estado = hallazgo.Estado,
            FechaRegistro = hallazgo.FechaRegistro,
            NombreEquipo = hallazgo.Inspeccion?.Equipo?.NombreEquipo,
            NombreUsuario = hallazgo.Inspeccion?.Usuario?.Nombre
        };

        return Ok(ApiResponse<HallazgoDetalleDto>.SuccessResponse(dto));
    }

    [HttpPost("{id}/estado")]
    [Authorize(Roles = "Administrador,Auditor")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CambiarEstado(long id, [FromBody] ActualizarEstadoHallazgoDto dto)
    {
        var hallazgo = await _context.Hallazgos.FirstOrDefaultAsync(h => h.IdHallazgo == id && !h.IsDeleted);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.NuevoEstado))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("El nuevo estado es obligatorio."));
        }

        hallazgo.Estado = dto.NuevoEstado;
        await _context.SaveChangesAsync();

        var resultadoDto = new HallazgoDto
        {
            IdHallazgo = hallazgo.IdHallazgo,
            IdInspeccion = hallazgo.IdInspeccion,
            IdPregunta = hallazgo.IdPregunta,
            Descripcion = hallazgo.Descripcion,
            Criticidad = hallazgo.Criticidad,
            Estado = hallazgo.Estado,
            FechaRegistro = hallazgo.FechaRegistro
        };

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(resultadoDto, "Estado del hallazgo actualizado exitosamente"));
    }
}