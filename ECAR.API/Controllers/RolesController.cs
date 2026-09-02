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
[Authorize(Roles = "Administrador")]
public class RolesController : ControllerBase
{
    private static readonly HashSet<string> SystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Administrador", "Técnico", "Auditor"
    };

    private readonly ECARDbContext _context;

    public RolesController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<RolDto>>>> GetRoles([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(ApiResponse<PagedResultDto<RolDto>>.ErrorResponse(
                "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100"));
        }

        var query = _context.Roles.AsQueryable();

        // Aplicar el filtro de búsqueda
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(r => r.Nombre.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var roles = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RolDto
            {
                IdRol = r.IdRol,
                Nombre = r.Nombre
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<RolDto>
        {
            Data = roles,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<RolDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<RolDto>>> GetRol(long id)
    {
        var rol = await _context.Roles.FindAsync(id);

        if (rol == null)
        {
            return NotFound(ApiResponse<RolDto>.ErrorResponse("Rol no encontrado"));
        }

        var rolDto = new RolDto
        {
            IdRol = rol.IdRol,
            Nombre = rol.Nombre
        };

        return Ok(ApiResponse<RolDto>.SuccessResponse(rolDto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RolDto>>> CreateRol(CreateRolDto createDto)
    {
        var nombre = createDto.Nombre.Trim();
        if (nombre.Length == 0)
            return BadRequest(ApiResponse<RolDto>.ErrorResponse("El nombre del rol es requerido"));
        if (await _context.Roles.AnyAsync(r => r.Nombre == nombre))
        {
            return BadRequest(ApiResponse<RolDto>.ErrorResponse("Ya existe un rol con ese nombre"));
        }

        var rol = new Rol
        {
            Nombre = nombre
        };

        _context.Roles.Add(rol);
        await _context.SaveChangesAsync();

        var rolDto = new RolDto
        {
            IdRol = rol.IdRol,
            Nombre = rol.Nombre
        };

        return CreatedAtAction(nameof(GetRol), new { id = rol.IdRol },
            ApiResponse<RolDto>.SuccessResponse(rolDto, "Rol creado exitosamente"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<RolDto>>> UpdateRol(long id, UpdateRolDto updateDto)
    {
        var rol = await _context.Roles.FindAsync(id);

        if (rol == null)
        {
            return NotFound(ApiResponse<RolDto>.ErrorResponse("Rol no encontrado"));
        }

        if (SystemRoles.Contains(rol.Nombre))
        {
            return BadRequest(ApiResponse<RolDto>.ErrorResponse(
                "Los roles base de ECAR no se pueden renombrar porque forman parte de las reglas de autorización"));
        }

        var nombre = updateDto.Nombre.Trim();
        if (nombre.Length == 0)
            return BadRequest(ApiResponse<RolDto>.ErrorResponse("El nombre del rol es requerido"));
        if (await _context.Roles.AnyAsync(r => r.Nombre == nombre && r.IdRol != id))
        {
            return BadRequest(ApiResponse<RolDto>.ErrorResponse("Ya existe un rol con ese nombre"));
        }

        rol.Nombre = nombre;
        await _context.SaveChangesAsync();

        var rolDto = new RolDto
        {
            IdRol = rol.IdRol,
            Nombre = rol.Nombre
        };

        return Ok(ApiResponse<RolDto>.SuccessResponse(rolDto, "Rol actualizado exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteRol(long id)
    {
        var rol = await _context.Roles.FindAsync(id);

        if (rol == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Rol no encontrado"));
        }

        if (SystemRoles.Contains(rol.Nombre))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                "Los roles base de ECAR no se pueden eliminar"));
        }

        if (await _context.UsuarioRoles.AnyAsync(ur => ur.IdRol == id))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                "El rol no puede eliminarse porque está asignado a uno o más usuarios"));
        }

        _context.Roles.Remove(rol);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Rol eliminado exitosamente"));
    }
}
