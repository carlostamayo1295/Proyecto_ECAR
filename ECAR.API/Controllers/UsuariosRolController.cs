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
public class UsuariosRolController : ControllerBase
{
    private readonly ECARDbContext _context;

    public UsuariosRolController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<UsuarioRolDto>>>> GetUsuariosRol(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse<PagedResultDto<UsuarioRolDto>>.ErrorResponse(
                "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100"));

        var query = _context.UsuarioRoles
            .Include(ur => ur.Usuario)
            .Include(ur => ur.Rol)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(ur =>
                ur.Usuario.Nombre.Contains(search) ||
                ur.Usuario.Correo.Contains(search) ||
                ur.Rol.Nombre.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var asignaciones = await query
            .OrderBy(ur => ur.Usuario.Nombre)
            .ThenBy(ur => ur.Rol.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ur => new UsuarioRolDto
            {
                Id = ur.Id,
                IdUsuario = ur.IdUsuario,
                UsuarioNombre = ur.Usuario.Nombre,
                UsuarioCorreo = ur.Usuario.Correo,
                IdRol = ur.IdRol,
                RolNombre = ur.Rol.Nombre
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<UsuarioRolDto>
        {
            Data = asignaciones,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<UsuarioRolDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UsuarioRolDto>>> GetUsuarioRol(long id)
    {
        var asignacion = await _context.UsuarioRoles
            .Include(ur => ur.Usuario)
            .Include(ur => ur.Rol)
            .FirstOrDefaultAsync(ur => ur.Id == id);

        if (asignacion == null)
        {
            return NotFound(ApiResponse<UsuarioRolDto>.ErrorResponse("Asignación no encontrada"));
        }

        return Ok(ApiResponse<UsuarioRolDto>.SuccessResponse(MapToDto(asignacion)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UsuarioRolDto>>> CreateUsuarioRol(CreateUsuarioRolDto createDto)
    {
        if (!await _context.Usuarios.AnyAsync(u => u.IdUsuario == createDto.IdUsuario))
        {
            return BadRequest(ApiResponse<UsuarioRolDto>.ErrorResponse("El usuario seleccionado no existe"));
        }

        if (!await _context.Roles.AnyAsync(r => r.IdRol == createDto.IdRol))
        {
            return BadRequest(ApiResponse<UsuarioRolDto>.ErrorResponse("El rol seleccionado no existe"));
        }

        if (await _context.UsuarioRoles.AnyAsync(ur => ur.IdUsuario == createDto.IdUsuario && ur.IdRol == createDto.IdRol))
        {
            return BadRequest(ApiResponse<UsuarioRolDto>.ErrorResponse("El usuario ya tiene asignado ese rol"));
        }

        var asignacion = new UsuarioRol
        {
            IdUsuario = createDto.IdUsuario,
            IdRol = createDto.IdRol
        };

        _context.UsuarioRoles.Add(asignacion);
        await _context.SaveChangesAsync();

        await _context.Entry(asignacion).Reference(ur => ur.Usuario).LoadAsync();
        await _context.Entry(asignacion).Reference(ur => ur.Rol).LoadAsync();

        return CreatedAtAction(nameof(GetUsuarioRol), new { id = asignacion.Id },
            ApiResponse<UsuarioRolDto>.SuccessResponse(MapToDto(asignacion), "Rol asignado exitosamente"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UsuarioRolDto>>> UpdateUsuarioRol(long id, UpdateUsuarioRolDto updateDto)
    {
        var asignacion = await _context.UsuarioRoles
            .Include(ur => ur.Usuario)
            .Include(ur => ur.Rol)
            .FirstOrDefaultAsync(ur => ur.Id == id);

        if (asignacion == null)
        {
            return NotFound(ApiResponse<UsuarioRolDto>.ErrorResponse("Asignación no encontrada"));
        }

        if (asignacion.Usuario.Activo && asignacion.Rol.Nombre == "Administrador" &&
            (asignacion.IdUsuario != updateDto.IdUsuario || asignacion.IdRol != updateDto.IdRol) &&
            !await HasAnotherActiveAdministrator(asignacion.IdUsuario))
        {
            return BadRequest(ApiResponse<UsuarioRolDto>.ErrorResponse(
                "No se puede retirar la asignación del último administrador activo"));
        }

        if (!await _context.Usuarios.AnyAsync(u => u.IdUsuario == updateDto.IdUsuario))
        {
            return BadRequest(ApiResponse<UsuarioRolDto>.ErrorResponse("El usuario seleccionado no existe"));
        }

        if (!await _context.Roles.AnyAsync(r => r.IdRol == updateDto.IdRol))
        {
            return BadRequest(ApiResponse<UsuarioRolDto>.ErrorResponse("El rol seleccionado no existe"));
        }

        if (await _context.UsuarioRoles.AnyAsync(ur =>
                ur.IdUsuario == updateDto.IdUsuario && ur.IdRol == updateDto.IdRol && ur.Id != id))
        {
            return BadRequest(ApiResponse<UsuarioRolDto>.ErrorResponse("El usuario ya tiene asignado ese rol"));
        }

        asignacion.IdUsuario = updateDto.IdUsuario;
        asignacion.IdRol = updateDto.IdRol;
        await _context.SaveChangesAsync();

        var asignacionActualizada = await _context.UsuarioRoles
            .AsNoTracking()
            .Include(ur => ur.Usuario)
            .Include(ur => ur.Rol)
            .SingleAsync(ur => ur.Id == id);

        return Ok(ApiResponse<UsuarioRolDto>.SuccessResponse(
            MapToDto(asignacionActualizada),
            "Asignación actualizada exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteUsuarioRol(long id)
    {
        var asignacion = await _context.UsuarioRoles
            .Include(ur => ur.Usuario)
            .Include(ur => ur.Rol)
            .FirstOrDefaultAsync(ur => ur.Id == id);

        if (asignacion == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Asignación no encontrada"));
        }

        if (asignacion.Usuario.Activo && asignacion.Rol.Nombre == "Administrador" &&
            !await HasAnotherActiveAdministrator(asignacion.IdUsuario))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                "No se puede retirar la asignación del último administrador activo"));
        }

        _context.UsuarioRoles.Remove(asignacion);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Asignación eliminada exitosamente"));
    }

    [HttpGet("usuarios")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> GetUsuariosLookup()
    {
        var usuarios = await _context.Usuarios
            .OrderBy(u => u.Nombre)
            .Select(u => new LookupDto { Id = u.IdUsuario, Nombre = u.Nombre + " (" + u.Correo + ")" })
            .ToListAsync();

        return Ok(ApiResponse<List<LookupDto>>.SuccessResponse(usuarios));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> GetRolesLookup()
    {
        var roles = await _context.Roles
            .OrderBy(r => r.Nombre)
            .Select(r => new LookupDto { Id = r.IdRol, Nombre = r.Nombre })
            .ToListAsync();

        return Ok(ApiResponse<List<LookupDto>>.SuccessResponse(roles));
    }

    private static UsuarioRolDto MapToDto(UsuarioRol ur) => new()
    {
        Id = ur.Id,
        IdUsuario = ur.IdUsuario,
        UsuarioNombre = ur.Usuario?.Nombre ?? string.Empty,
        UsuarioCorreo = ur.Usuario?.Correo ?? string.Empty,
        IdRol = ur.IdRol,
        RolNombre = ur.Rol?.Nombre ?? string.Empty
    };

    private Task<bool> HasAnotherActiveAdministrator(long excludedUserId) =>
        _context.UsuarioRoles.AnyAsync(ur =>
            ur.IdUsuario != excludedUserId &&
            ur.Usuario.Activo &&
            ur.Rol.Nombre == "Administrador");
}
