using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class UbicacionesController : ControllerBase
{
    private readonly ECARDbContext _context;

    public UbicacionesController(ECARDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<UbicacionDto>>>> GetUbicaciones(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse<PagedResultDto<UbicacionDto>>.ErrorResponse(
                "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100"));

        var query = _context.Ubicaciones.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.Planta.Contains(term) || u.Area.Contains(term) ||
                (u.Descripcion != null && u.Descripcion.Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var data = await query.OrderBy(u => u.Planta).ThenBy(u => u.Area)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UbicacionDto
            {
                IdUbicacion = u.IdUbicacion,
                Planta = u.Planta,
                Area = u.Area,
                Descripcion = u.Descripcion
            })
            .ToListAsync();

        return Ok(ApiResponse<PagedResultDto<UbicacionDto>>.SuccessResponse(new()
        {
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<UbicacionDto>>> GetUbicacion(long id)
    {
        var ubicacion = await _context.Ubicaciones.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUbicacion == id);
        return ubicacion == null
            ? NotFound(ApiResponse<UbicacionDto>.ErrorResponse("Ubicación no encontrada"))
            : Ok(ApiResponse<UbicacionDto>.SuccessResponse(MapToDto(ubicacion)));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<UbicacionDto>>> CreateUbicacion(CreateUbicacionDto dto)
    {
        var planta = dto.Planta.Trim();
        var area = dto.Area.Trim();
        if (planta.Length == 0 || area.Length == 0)
            return BadRequest(ApiResponse<UbicacionDto>.ErrorResponse("La planta y el área son requeridas"));
        if (await _context.Ubicaciones.AnyAsync(u => u.Planta == planta && u.Area == area))
            return BadRequest(ApiResponse<UbicacionDto>.ErrorResponse("Ya existe esa combinación de planta y área"));

        var ubicacion = new Ubicacion
        {
            Planta = planta,
            Area = area,
            Descripcion = NormalizeOptional(dto.Descripcion)
        };
        _context.Ubicaciones.Add(ubicacion);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUbicacion), new { id = ubicacion.IdUbicacion },
            ApiResponse<UbicacionDto>.SuccessResponse(MapToDto(ubicacion), "Ubicación creada exitosamente"));
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<UbicacionDto>>> UpdateUbicacion(long id, UpdateUbicacionDto dto)
    {
        var ubicacion = await _context.Ubicaciones.FindAsync(id);
        if (ubicacion == null)
            return NotFound(ApiResponse<UbicacionDto>.ErrorResponse("Ubicación no encontrada"));

        var planta = dto.Planta.Trim();
        var area = dto.Area.Trim();
        if (planta.Length == 0 || area.Length == 0)
            return BadRequest(ApiResponse<UbicacionDto>.ErrorResponse("La planta y el área son requeridas"));
        if (await _context.Ubicaciones.AnyAsync(u =>
                u.Planta == planta && u.Area == area && u.IdUbicacion != id))
            return BadRequest(ApiResponse<UbicacionDto>.ErrorResponse("Ya existe esa combinación de planta y área"));

        ubicacion.Planta = planta;
        ubicacion.Area = area;
        ubicacion.Descripcion = NormalizeOptional(dto.Descripcion);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<UbicacionDto>.SuccessResponse(MapToDto(ubicacion), "Ubicación actualizada exitosamente"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteUbicacion(long id)
    {
        var ubicacion = await _context.Ubicaciones.FindAsync(id);
        if (ubicacion == null)
            return NotFound(ApiResponse<bool>.ErrorResponse("Ubicación no encontrada"));

        if (await _context.Equipos.AnyAsync(e => e.IdUbicacion == id))
            return Conflict(ApiResponse<bool>.ErrorResponse(
                "La ubicación no se puede eliminar porque está asignada a uno o más equipos"));

        _context.Ubicaciones.Remove(ubicacion);
        await _context.SaveChangesAsync();
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Ubicación eliminada exitosamente"));
    }

    private static UbicacionDto MapToDto(Ubicacion ubicacion) => new()
    {
        IdUbicacion = ubicacion.IdUbicacion,
        Planta = ubicacion.Planta,
        Area = ubicacion.Area,
        Descripcion = ubicacion.Descripcion
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
