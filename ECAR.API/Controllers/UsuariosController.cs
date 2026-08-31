using BCrypt.Net;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var query = _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u => 
                u.Nombre.Contains(search) || 
                u.Correo.Contains(search) ||
                (u.UsuarioAD != null && u.UsuarioAD.Contains(search)));
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
        // Validar si el correo ya existe
        var existingUsuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == createDto.Correo);

        if (existingUsuario != null)
        {
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse("El correo ya está registrado"));
        }

        // Hashear el password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(createDto.Password);

        var usuario = new Usuario
        {
            Nombre = createDto.Nombre,
            Correo = createDto.Correo,
            UsuarioAD = createDto.UsuarioAD,
            PasswordHash = passwordHash,
            Activo = true
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        // Asignar roles si se proporcionaron
        if (createDto.RoleIds != null && createDto.RoleIds.Any())
        {
            foreach (var roleId in createDto.RoleIds)
            {
                var rol = await _context.Roles.FindAsync(roleId);
                if (rol != null)
                {
                    _context.UsuarioRoles.Add(new UsuarioRol
                    {
                        IdUsuario = usuario.IdUsuario,
                        IdRol = roleId
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        // Recargar usuario con roles
        await _context.Entry(usuario).Collection(u => u.UsuarioRoles).LoadAsync();

        var usuarioDto = new UsuarioDto
        {
            IdUsuario = usuario.IdUsuario,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            UsuarioAD = usuario.UsuarioAD,
            Activo = usuario.Activo,
            Roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
        };

        return CreatedAtAction(nameof(GetUsuario), new { id = usuario.IdUsuario }, 
            ApiResponse<UsuarioDto>.SuccessResponse(usuarioDto, "Usuario creado exitosamente"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UsuarioDto>>> UpdateUsuario(long id, UpdateUsuarioDto updateDto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);

        if (usuario == null)
        {
            return NotFound(ApiResponse<UsuarioDto>.ErrorResponse("Usuario no encontrado"));
        }

        if (!string.IsNullOrEmpty(updateDto.Nombre))
            usuario.Nombre = updateDto.Nombre;

        if (!string.IsNullOrEmpty(updateDto.Correo))
            usuario.Correo = updateDto.Correo;

        if (updateDto.UsuarioAD != null)
            usuario.UsuarioAD = updateDto.UsuarioAD;

        if (updateDto.Activo.HasValue)
            usuario.Activo = updateDto.Activo.Value;

        // Update password if provided
        if (!string.IsNullOrEmpty(updateDto.Password))
        {
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateDto.Password);
        }

        await _context.SaveChangesAsync();

        // Actualizar roles si se proporcionaron
        if (updateDto.RoleIds != null)
        {
            // Eliminar roles existentes
            var existingRoles = _context.UsuarioRoles.Where(ur => ur.IdUsuario == id);
            _context.UsuarioRoles.RemoveRange(existingRoles);

            // Agregar nuevos roles
            foreach (var roleId in updateDto.RoleIds)
            {
                var rol = await _context.Roles.FindAsync(roleId);
                if (rol != null)
                {
                    _context.UsuarioRoles.Add(new UsuarioRol
                    {
                        IdUsuario = id,
                        IdRol = roleId
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        // Recargar usuario con roles
        await _context.Entry(usuario).Collection(u => u.UsuarioRoles).LoadAsync();

        var usuarioDto = new UsuarioDto
        {
            IdUsuario = usuario.IdUsuario,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            UsuarioAD = usuario.UsuarioAD,
            Activo = usuario.Activo,
            Roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
        };

        return Ok(ApiResponse<UsuarioDto>.SuccessResponse(usuarioDto, "Usuario actualizado exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteUsuario(long id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);

        if (usuario == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Usuario no encontrado"));
        }

        // Soft delete - mark as inactive instead of removing
        usuario.Activo = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Usuario desactivado exitosamente"));
    }
}