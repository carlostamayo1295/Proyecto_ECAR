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
public class CategoriasEquipoController : ControllerBase
{
    private readonly ECARDbContext _context;

    public CategoriasEquipoController(ECARDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<CategoriaEquipoDto>>>> GetCategorias(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse<PagedResultDto<CategoriaEquipoDto>>.ErrorResponse(
                "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100"));

        var query = _context.CategoriasEquipo.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Nombre.Contains(term) ||
                (c.Descripcion != null && c.Descripcion.Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var data = await query.OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoriaEquipoDto
            {
                IdCategoria = c.IdCategoria,
                Nombre = c.Nombre,
                Descripcion = c.Descripcion
            })
            .ToListAsync();

        return Ok(ApiResponse<PagedResultDto<CategoriaEquipoDto>>.SuccessResponse(new()
        {
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<CategoriaEquipoDto>>> GetCategoria(long id)
    {
        var categoria = await _context.CategoriasEquipo.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCategoria == id);
        return categoria == null
            ? NotFound(ApiResponse<CategoriaEquipoDto>.ErrorResponse("Categoría no encontrada"))
            : Ok(ApiResponse<CategoriaEquipoDto>.SuccessResponse(MapToDto(categoria)));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<CategoriaEquipoDto>>> CreateCategoria(CreateCategoriaEquipoDto dto)
    {
        var nombre = dto.Nombre.Trim();
        if (nombre.Length == 0)
            return BadRequest(ApiResponse<CategoriaEquipoDto>.ErrorResponse("El nombre de la categoría es requerido"));
        if (await _context.CategoriasEquipo.AnyAsync(c => c.Nombre == nombre))
            return BadRequest(ApiResponse<CategoriaEquipoDto>.ErrorResponse("Ya existe una categoría con ese nombre"));

        var categoria = new CategoriaEquipo
        {
            Nombre = nombre,
            Descripcion = NormalizeOptional(dto.Descripcion)
        };
        _context.CategoriasEquipo.Add(categoria);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategoria), new { id = categoria.IdCategoria },
            ApiResponse<CategoriaEquipoDto>.SuccessResponse(MapToDto(categoria), "Categoría creada exitosamente"));
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<CategoriaEquipoDto>>> UpdateCategoria(long id, UpdateCategoriaEquipoDto dto)
    {
        var categoria = await _context.CategoriasEquipo.FindAsync(id);
        if (categoria == null)
            return NotFound(ApiResponse<CategoriaEquipoDto>.ErrorResponse("Categoría no encontrada"));

        var nombre = dto.Nombre.Trim();
        if (nombre.Length == 0)
            return BadRequest(ApiResponse<CategoriaEquipoDto>.ErrorResponse("El nombre de la categoría es requerido"));
        if (await _context.CategoriasEquipo.AnyAsync(c => c.Nombre == nombre && c.IdCategoria != id))
            return BadRequest(ApiResponse<CategoriaEquipoDto>.ErrorResponse("Ya existe una categoría con ese nombre"));

        categoria.Nombre = nombre;
        categoria.Descripcion = NormalizeOptional(dto.Descripcion);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<CategoriaEquipoDto>.SuccessResponse(MapToDto(categoria), "Categoría actualizada exitosamente"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCategoria(long id)
    {
        var categoria = await _context.CategoriasEquipo.FindAsync(id);
        if (categoria == null)
            return NotFound(ApiResponse<bool>.ErrorResponse("Categoría no encontrada"));

        if (await _context.Equipos.AnyAsync(e => e.IdCategoria == id))
            return Conflict(ApiResponse<bool>.ErrorResponse(
                "La categoría no se puede eliminar porque está asignada a uno o más equipos"));

        _context.CategoriasEquipo.Remove(categoria);
        await _context.SaveChangesAsync();
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Categoría eliminada exitosamente"));
    }

    private static CategoriaEquipoDto MapToDto(CategoriaEquipo categoria) => new()
    {
        IdCategoria = categoria.IdCategoria,
        Nombre = categoria.Nombre,
        Descripcion = categoria.Descripcion
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
