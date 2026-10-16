using BCrypt.Net;
using ECAR.API.Configuration;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public class UsuariosController : ControllerBase
{
    private readonly ECARDbContext _context;
    private readonly IMotivoCambio _motivoCambio;
    private readonly SeguridadOptions _seguridad;

    public UsuariosController(ECARDbContext context, IMotivoCambio motivoCambio, IOptions<SeguridadOptions> seguridad)
    {
        _context = context;
        _motivoCambio = motivoCambio;
        _seguridad = seguridad.Value;
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

        // Aplicar el filtro de búsqueda
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
                Roles = u.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList(),
                BloqueadoHasta = u.BloqueadoHasta,
                DebeCambiarPassword = u.DebeCambiarPassword,
                UltimoAcceso = u.UltimoAcceso
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

        return Ok(ApiResponse<UsuarioDto>.SuccessResponse(MapToDto(usuario)));
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

        // El correo y el usuario de Active Directory deben ser únicos.
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

        var tienePasswordLocal = !string.IsNullOrWhiteSpace(createDto.Password);
        if (tienePasswordLocal)
        {
            var errores = PoliticaPassword.Validar(createDto.Password, _seguridad);
            if (errores.Count > 0)
            {
                return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse(string.Join(". ", errores), errores));
            }
        }

        // Los usuarios que solo entran por Active Directory no necesitan hash de contraseña local.
        var passwordHash = tienePasswordLocal
            ? BCrypt.Net.BCrypt.HashPassword(createDto.Password)
            : string.Empty;

        var usuario = new Usuario
        {
            Nombre = createDto.Nombre,
            Correo = createDto.Correo,
            UsuarioAD = createDto.UsuarioAD,
            PasswordHash = passwordHash,
            Activo = true,
            // La contraseña inicial la conoce el Administrador: el usuario la cambia al entrar
            // (Parte 11 §11.200(a)(3)).
            DebeCambiarPassword = tienePasswordLocal,
            FechaCambioPassword = tienePasswordLocal ? DateTime.UtcNow : null
        };

        foreach (var roleId in roleIds)
        {
            usuario.UsuarioRoles.Add(new UsuarioRol { IdRol = roleId });
        }

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        // Se recarga el usuario para devolver los nombres de los roles en la respuesta.
        var usuarioCreado = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .SingleAsync(u => u.IdUsuario == usuario.IdUsuario);

        return CreatedAtAction(nameof(GetUsuario), new { id = usuario.IdUsuario },
            ApiResponse<UsuarioDto>.SuccessResponse(MapToDto(usuarioCreado), "Usuario creado exitosamente"));
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

        // Fase 4 (Parte 11 §11.200(a)(3)): el Administrador ya no fija contraseñas ajenas; solo las
        // restablece con una temporal que el usuario cambia al entrar.
        if (!string.IsNullOrEmpty(updateDto.Password))
        {
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse(
                "La contraseña no se edita aquí: use «Restablecer contraseña»"));
        }

        // Parte 11 §11.100(a): una firma no puede «pasar» a otra persona cambiando la identidad
        // de la cuenta, así que el correo y el usuario AD de quien ya firmó no se modifican.
        var cambiaCorreo = !string.IsNullOrWhiteSpace(updateDto.Correo) &&
            !string.Equals(updateDto.Correo.Trim(), usuario.Correo, StringComparison.OrdinalIgnoreCase);
        var cambiaUsuarioAd = updateDto.UsuarioAD != null &&
            !string.Equals(string.IsNullOrWhiteSpace(updateDto.UsuarioAD) ? null : updateDto.UsuarioAD.Trim(),
                usuario.UsuarioAD, StringComparison.OrdinalIgnoreCase);
        if ((cambiaCorreo || cambiaUsuarioAd) &&
            await _context.Inspecciones.AnyAsync(i => i.IdUsuario == id && i.FirmaDigital != null))
        {
            return BadRequest(ApiResponse<UsuarioDto>.ErrorResponse(
                "El correo y el usuario de Active Directory de quien ya firmó inspecciones no se pueden cambiar"));
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

        // Reemplazar las asignaciones de roles solo cuando la solicitud incluye RoleIds.
        if (roleIds != null)
        {
            // Eliminar las asignaciones anteriores.
            _context.UsuarioRoles.RemoveRange(usuario.UsuarioRoles);

            // Agregar las asignaciones solicitadas.
            foreach (var roleId in roleIds)
            {
                _context.UsuarioRoles.Add(new UsuarioRol
                {
                    IdUsuario = id,
                    IdRol = roleId
                });
            }
        }

        // Se guarda una sola vez para que una solicitud inválida no deje cambios parciales.
        await _context.SaveChangesAsync();

        // Se consulta de nuevo para que la respuesta contenga las asignaciones guardadas.
        var usuarioActualizado = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .SingleAsync(u => u.IdUsuario == id);

        return Ok(ApiResponse<UsuarioDto>.SuccessResponse(MapToDto(usuarioActualizado), "Usuario actualizado exitosamente"));
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

        // Borrado lógico: se marca como inactivo en lugar de eliminarlo
        usuario.Activo = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Usuario desactivado exitosamente"));
    }

    /// <summary>
    /// Reactiva un usuario desactivado. DELETE es el borrado lógico y siempre desactiva;
    /// hasta ahora el botón "Activar" del cliente llamaba también a DELETE, así que no hacía
    /// nada y avisaba de un éxito falso.
    /// </summary>
    [HttpPut("{id}/activar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<bool>>> ActivarUsuario(long id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);

        if (usuario == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Usuario no encontrado"));
        }

        usuario.Activo = true;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Usuario activado exitosamente"));
    }

    /// <summary>
    /// Endpoint 25 (Parte 11 §11.200(a)(3)). Genera una contraseña temporal que se muestra una sola
    /// vez al Administrador; el usuario debe cambiarla al entrar, así que el Administrador nunca
    /// conoce la definitiva. También desbloquea la cuenta.
    /// </summary>
    [HttpPost("{id:long}/restablecer-password")]
    public async Task<ActionResult<ApiResponse<PasswordTemporalDto>>> RestablecerPassword(long id, MotivoDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.HistorialPasswords)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);
        if (usuario == null)
        {
            return NotFound(ApiResponse<PasswordTemporalDto>.ErrorResponse("Usuario no encontrado"));
        }

        if (string.IsNullOrWhiteSpace(usuario.PasswordHash) && !string.IsNullOrWhiteSpace(usuario.UsuarioAD))
        {
            return BadRequest(ApiResponse<PasswordTemporalDto>.ErrorResponse(
                "El usuario entra con Active Directory: su contraseña se restablece en el directorio"));
        }

        var temporal = PoliticaPassword.GenerarTemporal();
        var ahora = DateTime.UtcNow;
        _motivoCambio.Establecer(dto.Motivo);
        if (!string.IsNullOrWhiteSpace(usuario.PasswordHash))
        {
            usuario.HistorialPasswords.Add(new HistorialPassword { PasswordHash = usuario.PasswordHash, Fecha = ahora });
        }

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporal);
        usuario.DebeCambiarPassword = true;
        usuario.FechaCambioPassword = ahora;
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        _context.RegistrarEvento(AuditoriaAcciones.RestablecerPassword, usuario.IdUsuario);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<PasswordTemporalDto>.SuccessResponse(
            new PasswordTemporalDto { PasswordTemporal = temporal, DebeCambiarAlEntrar = true },
            "Contraseña restablecida. Entréguela al usuario: solo se muestra esta vez"));
    }

    /// <summary>Endpoint 26: quita el bloqueo por intentos fallidos, con motivo.</summary>
    [HttpPost("{id:long}/desbloquear")]
    public async Task<ActionResult<ApiResponse<bool>>> DesbloquearUsuario(long id, MotivoDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Usuario no encontrado"));
        }

        if (usuario.BloqueadoHasta is null || usuario.BloqueadoHasta <= DateTime.UtcNow)
        {
            return Conflict(ApiResponse<bool>.ErrorResponse("La cuenta no está bloqueada"));
        }

        _motivoCambio.Establecer(dto.Motivo);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Cuenta desbloqueada"));
    }

    private static UsuarioDto MapToDto(Usuario usuario) => new()
    {
        IdUsuario = usuario.IdUsuario,
        Nombre = usuario.Nombre,
        Correo = usuario.Correo,
        UsuarioAD = usuario.UsuarioAD,
        Activo = usuario.Activo,
        Roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList(),
        BloqueadoHasta = usuario.BloqueadoHasta,
        DebeCambiarPassword = usuario.DebeCambiarPassword,
        UltimoAcceso = usuario.UltimoAcceso
    };

    private Task<bool> HasAnotherActiveAdministrator(long excludedUserId) =>
        _context.UsuarioRoles.AnyAsync(ur =>
            ur.IdUsuario != excludedUserId &&
            ur.Usuario.Activo &&
            ur.Rol.Nombre == "Administrador");
}
