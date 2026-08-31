using BCrypt.Net;
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
public class UsuariosController : ControllerBase
{
    private readonly ECARDbContext _context;

    public UsuariosController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<UsuarioDto>>>> GetUsuarios([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(ApiResponse<PagedResultDto<UsuarioDto>>.ErrorResponse(
                "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100"));
        }

        var query = _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.Nombre.Contains(term) ||
                u.Correo.Contains(term) ||
                (u.UsuarioAD != null && u.UsuarioAD.Contains(term)));
        }

        var totalCount = await query.CountAsync();

        var usuarios = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UsuarioDto
            {
                IdUsuario = u.IdUsuario,
                Nombre = u.Nombre,
                Correo = u.Correo,
                UsuarioAD = u.UsuarioAD,
                Activo = u.Activo,
                Roles = u.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<UsuarioDto>
        {
            Data = usuarios,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<UsuarioDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UsuarioDto>>> GetUsuario(long id)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);

        if (usuario == null)
        {
            return NotFound(ApiResponse<UsuarioDto>.ErrorResponse("Usuario no encontrado"));
        }

        var usuarioDto = new UsuarioDto
        {
            IdUsuario = usuario.IdUsuario,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            UsuarioAD = usuario.UsuarioAD,
            Activo = usuario.Activo,
            Roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
        };

        return Ok(ApiResponse<UsuarioDto>.SuccessResponse(usuarioDto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UsuarioDto>>> CreateUsuario(CreateUsuarioDto createDto)
    {
        createDto.Nombre = createDto.Nombre.Trim();
        createDto.Correo = createDto.Correo.Trim();
        createDto.UsuarioAD = string.IsNullOrWhiteSpace(createDto.UsuarioAD) ? null : createDto.UsuarioAD.Trim();
        if (createDto.Nombre.Length == 0 || createDto.Correo.Length == 0)
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("El nombre y el correo son requeridos"));
        if (string.IsNullOrWhiteSpace(createDto.Password) && createDto.UsuarioAD == null)
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse(
                "Debe indicar una contraseña local o un usuario de Active Directory"));

        // Email and AD usernames must be unique.
        var existingUsuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == createDto.Correo);

        if (existingUsuario != null)
        {
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("El correo ya está registrado"));
        }

        if (createDto.UsuarioAD != null &&
            await _context.Usuarios.AnyAsync(u => u.UsuarioAD == createDto.UsuarioAD))
        {
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("El usuario de Active Directory ya está registrado"));
        }

        var roleIds = (createDto.RoleIds ?? []).Distinct().ToList();
        if (roleIds.Count > 0)
        {
            var existingRoleIds = await _context.Roles
                .Where(r => roleIds.Contains(r.IdRol))
                .Select(r => r.IdRol)
                .ToListAsync();

            if (existingRoleIds.Count != roleIds.Count)
            {
                return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("Uno o más roles seleccionados no existen"));
            }
        }

        // AD-only users do not need a local password hash.
        var passwordHash = string.IsNullOrWhiteSpace(createDto.Password)
            ? string.Empty
            : BCrypt.Net.BCrypt.HashPassword(createDto.Password);

        var usuario = new Usuario
        {
            Nombre = createDto.Nombre,
            Correo = createDto.Correo,
            UsuarioAD = createDto.UsuarioAD,
            PasswordHash = passwordHash,
            Activo = true
        };

        foreach (var roleId in roleIds)
        {
            usuario.UsuarioRoles.Add(new UsuarioRol { IdRol = roleId });
        }

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        // Reload the user to return the role names in the response.
        var usuarioCreado = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .SingleAsync(u => u.IdUsuario == usuario.IdUsuario);

        var usuarioDto = new UsuarioDto
        {
            IdUsuario = usuarioCreado.IdUsuario,
            Nombre = usuarioCreado.Nombre,
            Correo = usuarioCreado.Correo,
            UsuarioAD = usuarioCreado.UsuarioAD,
            Activo = usuarioCreado.Activo,
            Roles = usuarioCreado.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
        };

        return CreatedAtAction(nameof(GetUsuario), new { id = usuario.IdUsuario },
            ApiResponse<UsuarioDto>.SuccessResponse(usuarioDto, "Usuario creado exitosamente"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UsuarioDto>>> UpdateUsuario(long id, UpdateUsuarioDto updateDto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);

        if (usuario == null)
        {
            return NotFound(ApiResponse<UsuarioDto>.ErrorResponse("Usuario no encontrado"));
        }

        List<long>? roleIds = null;
        if (updateDto.RoleIds != null)
        {
            roleIds = updateDto.RoleIds.Distinct().ToList();
            var validRoleIds = await _context.Roles
                .Where(r => roleIds.Contains(r.IdRol))
                .Select(r => r.IdRol)
                .ToListAsync();

            if (validRoleIds.Count != roleIds.Count)
            {
                return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("Uno o más roles seleccionados no existen"));
            }
        }

        var isCurrentlyActiveAdministrator = usuario.Activo &&
            usuario.UsuarioRoles.Any(ur => ur.Rol.Nombre == "Administrador");
        var willRemainActive = updateDto.Activo ?? usuario.Activo;
        var willRemainAdministrator = roleIds == null
            ? usuario.UsuarioRoles.Any(ur => ur.Rol.Nombre == "Administrador")
            : await _context.Roles.AnyAsync(r => roleIds.Contains(r.IdRol) && r.Nombre == "Administrador");

        if (isCurrentlyActiveAdministrator && (!willRemainActive || !willRemainAdministrator) &&
            !await HasAnotherActiveAdministrator(id))
        {
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse(
                "No se puede desactivar o retirar el rol al último administrador activo"));
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Nombre))
            usuario.Nombre = updateDto.Nombre.Trim();

        if (!string.IsNullOrWhiteSpace(updateDto.Correo))
        {
            var correo = updateDto.Correo.Trim();
            if (await _context.Usuarios.AnyAsync(u => u.Correo == correo && u.IdUsuario != id))
            {
                return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("El correo ya está registrado"));
            }
            usuario.Correo = correo;
        }

        if (updateDto.UsuarioAD != null)
        {
            var usuarioAd = string.IsNullOrWhiteSpace(updateDto.UsuarioAD) ? null : updateDto.UsuarioAD.Trim();
            if (usuarioAd != null &&
                await _context.Usuarios.AnyAsync(u => u.UsuarioAD == usuarioAd && u.IdUsuario != id))
            {
                return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("El usuario de Active Directory ya está registrado"));
            }
            usuario.UsuarioAD = usuarioAd;
        }

        if (updateDto.Activo.HasValue)
            usuario.Activo = updateDto.Activo.Value;

        // Update password if provided
        if (!string.IsNullOrEmpty(updateDto.Password))
        {
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateDto.Password);
        }

        // Replace role assignments only when the request includes RoleIds.
        if (roleIds != null)
        {
            // Remove the previous assignments.
            _context.UsuarioRoles.RemoveRange(usuario.UsuarioRoles);

            // Add the requested assignments.
            foreach (var roleId in roleIds)
            {
                _context.UsuarioRoles.Add(new UsuarioRol
                {
                    IdUsuario = id,
                    IdRol = roleId
                });
            }
        }

        // Save once so an invalid request cannot leave partial changes.
        await _context.SaveChangesAsync();

        // Use a fresh query so the response contains the saved assignments.
        var usuarioActualizado = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .SingleAsync(u => u.IdUsuario == id);

        var usuarioDto = new UsuarioDto
        {
            IdUsuario = usuarioActualizado.IdUsuario,
            Nombre = usuarioActualizado.Nombre,
            Correo = usuarioActualizado.Correo,
            UsuarioAD = usuarioActualizado.UsuarioAD,
            Activo = usuarioActualizado.Activo,
            Roles = usuarioActualizado.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
        };

        return Ok(ApiResponse<UsuarioDto>.SuccessResponse(usuarioDto, "Usuario actualizado exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteUsuario(long id)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);

        if (usuario == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Usuario no encontrado"));
        }

        if (usuario.Activo && usuario.UsuarioRoles.Any(ur => ur.Rol.Nombre == "Administrador") &&
            !await HasAnotherActiveAdministrator(id))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                "No se puede desactivar al último administrador activo"));
        }

        // Soft delete - mark as inactive instead of removing
        usuario.Activo = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Usuario desactivado exitosamente"));
    }

    private Task<bool> HasAnotherActiveAdministrator(long excludedUserId) =>
        _context.UsuarioRoles.AnyAsync(ur =>
            ur.IdUsuario != excludedUserId &&
            ur.Usuario.Activo &&
            ur.Rol.Nombre == "Administrador");
}
