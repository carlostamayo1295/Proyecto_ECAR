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

    public CategoriasEquipoController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<CategoriaEquipoDto>>>> GetCategoriasEquipo([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        var query = _context.CategoriasEquipo.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => c.Nombre.Contains(search) || (c.Descripcion != null && c.Descripcion.Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var categorias = await query
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoriaEquipoDto
            {
                IdCategoria = c.IdCategoria,
                Nombre = c.Nombre,
                Descripcion = c.Descripcion
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<CategoriaEquipoDto>
        {
            Data = categorias,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<CategoriaEquipoDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<CategoriaEquipoDto>>> GetCategoriaEquipo(long id)
    {
        var categoria = await _context.CategoriasEquipo.FindAsync(id);

        if (categoria == null)
        {
            return NotFound(ApiResponse<CategoriaEquipoDto>.ErrorResponse("Categoría de equipo no encontrada"));
        }

        var categoriaDto = new CategoriaEquipoDto
        {
            IdCategoria = categoria.IdCategoria,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion
        };

        return Ok(ApiResponse<CategoriaEquipoDto>.SuccessResponse(categoriaDto));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<CategoriaEquipoDto>>> CreateCategoriaEquipo(CreateCategoriaEquipoDto createDto)
    {
        var existente = await _context.CategoriasEquipo.FirstOrDefaultAsync(c => c.Nombre == createDto.Nombre);
        if (existente != null)
        {
            return BadRequest(ApiResponse<CategoriaEquipoDto>.ErrorResponse("Ya existe una categoría con ese nombre"));
        }

        var categoria = new CategoriaEquipo
        {
            Nombre = createDto.Nombre,
            Descripcion = createDto.Descripcion
        };

        _context.CategoriasEquipo.Add(categoria);
        await _context.SaveChangesAsync();

        var categoriaDto = new CategoriaEquipoDto
        {
            IdCategoria = categoria.IdCategoria,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion
        };

        return CreatedAtAction(nameof(GetCategoriaEquipo), new { id = categoria.IdCategoria },
            ApiResponse<CategoriaEquipoDto>.SuccessResponse(categoriaDto, "Categoría de equipo creada exitosamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<CategoriaEquipoDto>>> UpdateCategoriaEquipo(long id, UpdateCategoriaEquipoDto updateDto)
    {
        var categoria = await _context.CategoriasEquipo.FindAsync(id);

        if (categoria == null)
        {
            return NotFound(ApiResponse<CategoriaEquipoDto>.ErrorResponse("Categoría de equipo no encontrada"));
        }

        categoria.Nombre = updateDto.Nombre;
        categoria.Descripcion = updateDto.Descripcion;
        await _context.SaveChangesAsync();

        var categoriaDto = new CategoriaEquipoDto
        {
            IdCategoria = categoria.IdCategoria,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion
        };

        return Ok(ApiResponse<CategoriaEquipoDto>.SuccessResponse(categoriaDto, "Categoría de equipo actualizada exitosamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCategoriaEquipo(long id)
    {
        var categoria = await _context.CategoriasEquipo.FindAsync(id);

        if (categoria == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Categoría de equipo no encontrada"));
        }

        var enUso = await _context.Equipos.AnyAsync(e => e.IdCategoria == id);
        if (enUso)
        {
            // Mismo criterio que Ubicaciones: 409 cuando el registro está en uso.
            return Conflict(ApiResponse<bool>.ErrorResponse("No se puede eliminar la categoría porque tiene equipos asociados"));
        }

        _context.CategoriasEquipo.Remove(categoria);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Categoría de equipo eliminada exitosamente"));
    }
}
