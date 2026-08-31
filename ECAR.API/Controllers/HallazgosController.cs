using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HallazgosController : ControllerBase
{
    private readonly ECARDbContext _context;

    private const string EstadoAbierto = "Abierto";
    private const string EstadoCerrado = "Cerrado";

    public HallazgosController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<HallazgoDto>>>> GetHallazgos([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] long? idInspeccion = null, [FromQuery] string? estado = null)
    {
        var query = _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .AsQueryable();

        if (idInspeccion.HasValue)
        {
            query = query.Where(h => h.IdInspeccion == idInspeccion.Value);
        }

        if (!string.IsNullOrEmpty(estado))
        {
            query = query.Where(h => h.Estado == estado);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(h =>
                h.Descripcion.Contains(search) ||
                h.Inspeccion.Equipo.NombreEquipo.Contains(search) ||
                (h.Criticidad != null && h.Criticidad.Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var hallazgos = await query
            .OrderByDescending(h => h.FechaRegistro)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new HallazgoDto
            {
                IdHallazgo = h.IdHallazgo,
                IdInspeccion = h.IdInspeccion,
                NombreEquipo = h.Inspeccion.Equipo.NombreEquipo,
                Descripcion = h.Descripcion,
                Criticidad = h.Criticidad,
                Estado = h.Estado,
                FechaRegistro = h.FechaRegistro
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<HallazgoDto>
        {
            Data = hallazgos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<HallazgoDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> GetHallazgo(long id)
    {
        var hallazgo = await _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CreateHallazgo(CreateHallazgoDto createDto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .FirstOrDefaultAsync(i => i.IdInspeccion == createDto.IdInspeccion);

        if (inspeccion == null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La inspección indicada no existe"));
        }

        var hallazgo = new Hallazgo
        {
            IdInspeccion = createDto.IdInspeccion,
            Descripcion = createDto.Descripcion,
            Criticidad = createDto.Criticidad,
            Estado = EstadoAbierto,
            FechaRegistro = DateTime.UtcNow
        };

        _context.Hallazgos.Add(hallazgo);
        await _context.SaveChangesAsync();

        await _context.Entry(hallazgo).Reference(h => h.Inspeccion).LoadAsync();
        await _context.Entry(hallazgo.Inspeccion).Reference(i => i.Equipo).LoadAsync();

        return CreatedAtAction(nameof(GetHallazgo), new { id = hallazgo.IdHallazgo },
            ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo), "Hallazgo registrado exitosamente"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> UpdateHallazgo(long id, UpdateHallazgoDto updateDto)
    {
        var hallazgo = await _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Descripcion))
            hallazgo.Descripcion = updateDto.Descripcion;

        if (updateDto.Criticidad != null)
            hallazgo.Criticidad = updateDto.Criticidad;

        if (!string.IsNullOrWhiteSpace(updateDto.Estado))
        {
            if (updateDto.Estado != EstadoAbierto && updateDto.Estado != EstadoCerrado)
            {
                return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse($"El estado debe ser '{EstadoAbierto}' o '{EstadoCerrado}'"));
            }
            hallazgo.Estado = updateDto.Estado;
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo), "Hallazgo actualizado exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteHallazgo(long id)
    {
        var hallazgo = await _context.Hallazgos.FindAsync(id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Hallazgo no encontrado"));
        }

        _context.Hallazgos.Remove(hallazgo);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Hallazgo eliminado exitosamente"));
    }

    private static HallazgoDto MapToDto(Hallazgo h)
    {
        return new HallazgoDto
        {
            IdHallazgo = h.IdHallazgo,
            IdInspeccion = h.IdInspeccion,
            NombreEquipo = h.Inspeccion?.Equipo?.NombreEquipo,
            Descripcion = h.Descripcion,
            Criticidad = h.Criticidad,
            Estado = h.Estado,
            FechaRegistro = h.FechaRegistro
        };
    }
}
