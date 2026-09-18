using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Cryptography;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class EquiposController : ControllerBase
{
    private readonly ECARDbContext _context;
    private readonly IConfiguration _configuration;

    public EquiposController(ECARDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EquipoDto>>>> GetEquipos(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? criticidad = null,
        [FromQuery] long? idCategoria = null,
        [FromQuery] long? idUbicacion = null,
        [FromQuery] string? planta = null,
        [FromQuery] string? area = null,
        [FromQuery] bool? activo = null)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(ApiResponse<PagedResultDto<EquipoDto>>.ErrorResponse(
                "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100"));
        }

        var query = _context.Equipos
            .Include(e => e.Categoria)
            .Include(e => e.Ubicacion)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.CodigoInterno.Contains(term) ||
                e.ActivoFijo.Contains(term) ||
                e.NombreEquipo.Contains(term) ||
                (e.Marca != null && e.Marca.Contains(term)) ||
                (e.Modelo != null && e.Modelo.Contains(term)) ||
                (e.SerialFabricante != null && e.SerialFabricante.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(criticidad))
        {
            var value = criticidad.Trim();
            query = query.Where(e => e.Criticidad == value);
        }

        if (idCategoria.HasValue)
            query = query.Where(e => e.IdCategoria == idCategoria.Value);
        if (idUbicacion.HasValue)
            query = query.Where(e => e.IdUbicacion == idUbicacion.Value);
        if (!string.IsNullOrWhiteSpace(planta))
        {
            var value = planta.Trim();
            query = query.Where(e => e.Ubicacion != null && e.Ubicacion.Planta == value);
        }
        if (!string.IsNullOrWhiteSpace(area))
        {
            var value = area.Trim();
            query = query.Where(e => e.Ubicacion != null && e.Ubicacion.Area == value);
        }
        if (activo.HasValue)
            query = query.Where(e => e.Activo == activo.Value);

        var totalCount = await query.CountAsync();

        var equipos = (await query
            .OrderBy(e => e.CodigoInterno)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync())
            .Select(MapToDto)
            .ToList();

        var pagedResult = new PagedResultDto<EquipoDto>
        {
            Data = equipos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<EquipoDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EquipoDto>>> GetEquipo(long id)
    {
        var equipo = await _context.Equipos
            .Include(e => e.Categoria)
            .Include(e => e.Ubicacion)
            .FirstOrDefaultAsync(e => e.IdEquipo == id);

        if (equipo == null)
        {
            return NotFound(ApiResponse<EquipoDto>.ErrorResponse("Equipo no encontrado"));
        }

        return Ok(ApiResponse<EquipoDto>.SuccessResponse(MapToDto(equipo)));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<EquipoDto>>> CreateEquipo(CreateEquipoDto createDto)
    {
        var codigoInterno = createDto.CodigoInterno.Trim();
        var activoFijo = createDto.ActivoFijo.Trim();
        var nombreEquipo = createDto.NombreEquipo.Trim();
        if (codigoInterno.Length == 0 || activoFijo.Length == 0 || nombreEquipo.Length == 0)
            return BadRequest(ApiResponse<EquipoDto>.ErrorResponse(
                "El código interno, el activo fijo y el nombre del equipo son requeridos"));

        if (await _context.Equipos.AnyAsync(e => e.CodigoInterno == codigoInterno))
        {
            return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El código interno ya está registrado"));
        }

        if (await _context.Equipos.AnyAsync(e => e.ActivoFijo == activoFijo))
        {
            return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El activo fijo ya está registrado"));
        }

        if (createDto.IdCategoria.HasValue &&
            !await _context.CategoriasEquipo.AnyAsync(c => c.IdCategoria == createDto.IdCategoria.Value))
        {
            return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("La categoría seleccionada no existe"));
        }

        if (createDto.IdUbicacion.HasValue &&
            !await _context.Ubicaciones.AnyAsync(u => u.IdUbicacion == createDto.IdUbicacion.Value))
        {
            return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("La ubicación seleccionada no existe"));
        }

        var equipo = new Equipo
        {
            CodigoInterno = codigoInterno,
            ActivoFijo = activoFijo,
            SerialFabricante = NormalizeOptional(createDto.SerialFabricante),
            NombreEquipo = nombreEquipo,
            Marca = NormalizeOptional(createDto.Marca),
            Modelo = NormalizeOptional(createDto.Modelo),
            Fabricante = NormalizeOptional(createDto.Fabricante),
            Criticidad = NormalizeOptional(createDto.Criticidad),
            IdCategoria = createDto.IdCategoria,
            IdUbicacion = createDto.IdUbicacion,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Equipos.Add(equipo);
        await _context.SaveChangesAsync();

        await _context.Entry(equipo).Reference(e => e.Categoria).LoadAsync();
        await _context.Entry(equipo).Reference(e => e.Ubicacion).LoadAsync();

        return CreatedAtAction(nameof(GetEquipo), new { id = equipo.IdEquipo },
            ApiResponse<EquipoDto>.SuccessResponse(MapToDto(equipo), "Equipo creado exitosamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<EquipoDto>>> UpdateEquipo(long id, UpdateEquipoDto updateDto)
    {
        var equipo = await _context.Equipos.FindAsync(id);

        if (equipo == null)
        {
            return NotFound(ApiResponse<EquipoDto>.ErrorResponse("Equipo no encontrado"));
        }

        if (updateDto.CodigoInterno != null)
        {
            var codigoInterno = updateDto.CodigoInterno.Trim();
            if (codigoInterno.Length == 0)
                return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El código interno no puede estar vacío"));
            if (await _context.Equipos.AnyAsync(e => e.CodigoInterno == codigoInterno && e.IdEquipo != id))
            {
                return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El código interno ya está registrado"));
            }
            equipo.CodigoInterno = codigoInterno;
        }

        if (updateDto.ActivoFijo != null)
        {
            var activoFijo = updateDto.ActivoFijo.Trim();
            if (activoFijo.Length == 0)
                return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El activo fijo no puede estar vacío"));
            if (await _context.Equipos.AnyAsync(e => e.ActivoFijo == activoFijo && e.IdEquipo != id))
            {
                return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El activo fijo ya está registrado"));
            }
            equipo.ActivoFijo = activoFijo;
        }

        if (updateDto.NombreEquipo != null)
        {
            var nombreEquipo = updateDto.NombreEquipo.Trim();
            if (nombreEquipo.Length == 0)
                return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El nombre del equipo no puede estar vacío"));
            equipo.NombreEquipo = nombreEquipo;
        }

        if (updateDto.SerialFabricante != null)
            equipo.SerialFabricante = NormalizeOptional(updateDto.SerialFabricante);

        if (updateDto.Marca != null)
            equipo.Marca = NormalizeOptional(updateDto.Marca);

        if (updateDto.Modelo != null)
            equipo.Modelo = NormalizeOptional(updateDto.Modelo);

        if (updateDto.Fabricante != null)
            equipo.Fabricante = NormalizeOptional(updateDto.Fabricante);

        if (updateDto.Criticidad != null)
            equipo.Criticidad = NormalizeOptional(updateDto.Criticidad);

        if (updateDto.IdCategoria.HasValue)
        {
            if (updateDto.IdCategoria.Value == 0)
            {
                equipo.IdCategoria = null;
            }
            else
            {
                if (!await _context.CategoriasEquipo.AnyAsync(c => c.IdCategoria == updateDto.IdCategoria.Value))
                {
                    return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("La categoría seleccionada no existe"));
                }
                equipo.IdCategoria = updateDto.IdCategoria.Value;
            }
        }

        if (updateDto.IdUbicacion.HasValue)
        {
            if (updateDto.IdUbicacion.Value == 0)
            {
                equipo.IdUbicacion = null;
            }
            else
            {
                if (!await _context.Ubicaciones.AnyAsync(u => u.IdUbicacion == updateDto.IdUbicacion.Value))
                {
                    return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("La ubicación seleccionada no existe"));
                }
                equipo.IdUbicacion = updateDto.IdUbicacion.Value;
            }
        }

        if (updateDto.Activo.HasValue)
            equipo.Activo = updateDto.Activo.Value;

        await _context.SaveChangesAsync();

        await _context.Entry(equipo).Reference(e => e.Categoria).LoadAsync();
        await _context.Entry(equipo).Reference(e => e.Ubicacion).LoadAsync();

        return Ok(ApiResponse<EquipoDto>.SuccessResponse(MapToDto(equipo), "Equipo actualizado exitosamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEquipo(long id)
    {
        var equipo = await _context.Equipos.FindAsync(id);

        if (equipo == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Equipo no encontrado"));
        }

        // Se conserva el registro para el historial de inspecciones y se marca como inactivo.
        equipo.Activo = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Equipo desactivado exitosamente"));
    }

    [HttpGet("categorias")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> GetCategorias()
    {
        var categorias = await _context.CategoriasEquipo
            .OrderBy(c => c.Nombre)
            .Select(c => new LookupDto { Id = c.IdCategoria, Nombre = c.Nombre })
            .ToListAsync();

        return Ok(ApiResponse<List<LookupDto>>.SuccessResponse(categorias));
    }

    [HttpGet("ubicaciones")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> GetUbicaciones()
    {
        var ubicaciones = await _context.Ubicaciones
            .OrderBy(u => u.Planta).ThenBy(u => u.Area)
            .Select(u => new LookupDto { Id = u.IdUbicacion, Nombre = u.Planta + " - " + u.Area })
            .ToListAsync();

        return Ok(ApiResponse<List<LookupDto>>.SuccessResponse(ubicaciones));
    }

    // ---- Código QR ----
    // El QR codifica una URL pública del cliente ({Cliente:BaseUrl}/equipos/qr/{token}).
    // El token es opaco y no expone el id del equipo; se guarda en Equipos.QRCode.

    [HttpPost("{id:long}/qr")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<EquipoQrDto>>> GenerateQr(long id)
    {
        var equipo = await _context.Equipos.FindAsync(id);
        if (equipo == null)
        {
            return NotFound(ApiResponse<EquipoQrDto>.ErrorResponse("Equipo no encontrado"));
        }

        // Idempotente: si ya tiene token se devuelve el existente para no invalidar etiquetas impresas
        var esNuevo = string.IsNullOrEmpty(equipo.QRCode);
        if (esNuevo)
        {
            equipo.QRCode = GenerarToken();
            await _context.SaveChangesAsync();
        }

        return Ok(ApiResponse<EquipoQrDto>.SuccessResponse(MapToQrDto(equipo, esNuevo),
            esNuevo ? "Código QR generado exitosamente" : "El equipo ya tenía un código QR"));
    }

    [HttpPut("{id:long}/qr/regenerar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<EquipoQrDto>>> RegenerateQr(long id)
    {
        var equipo = await _context.Equipos.FindAsync(id);
        if (equipo == null)
        {
            return NotFound(ApiResponse<EquipoQrDto>.ErrorResponse("Equipo no encontrado"));
        }

        equipo.QRCode = GenerarToken();
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<EquipoQrDto>.SuccessResponse(MapToQrDto(equipo, esNuevo: true),
            "Código QR regenerado. Las etiquetas anteriores ya no son válidas"));
    }

    // Imagen PNG generada en el servidor (sin servicios externos). Requiere sesión.
    [HttpGet("{id:long}/qr.png")]
    public async Task<IActionResult> GetQrImage(long id, [FromQuery] int pixeles = 10)
    {
        var equipo = await _context.Equipos.FindAsync(id);
        if (equipo == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Equipo no encontrado"));
        }

        if (string.IsNullOrEmpty(equipo.QRCode))
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("El equipo no tiene código QR generado"));
        }

        pixeles = Math.Clamp(pixeles, 4, 40);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(BuildUrlConsulta(equipo.QRCode), QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(pixeles);

        return File(png, "image/png", $"QR-{equipo.CodigoInterno}.png");
    }

    // Consulta pública: la abre quien escanea la etiqueta, sin sesión. Solo expone la ficha
    // básica y los checklists activos; nunca datos de usuarios ni de inspecciones.
    [HttpGet("qr/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ConsultaQrDto>>> GetByQr(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
        {
            return NotFound(ApiResponse<ConsultaQrDto>.ErrorResponse("Código QR no válido"));
        }

        var equipo = await _context.Equipos
            .Include(e => e.Categoria)
            .Include(e => e.Ubicacion)
            .FirstOrDefaultAsync(e => e.QRCode == token && e.Activo);

        if (equipo == null)
        {
            return NotFound(ApiResponse<ConsultaQrDto>.ErrorResponse("Este código QR no corresponde a ningún equipo registrado"));
        }

        // El MVP no asocia checklists a equipos ni categorías: aplican todas las versiones activas
        var checklists = await _context.Checklists
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new ChecklistDto
            {
                IdChecklist = c.IdChecklist,
                Nombre = c.Nombre,
                Version = c.Version,
                Activo = c.Activo,
                FechaCreacion = c.FechaCreacion
            })
            .ToListAsync();

        var dto = new ConsultaQrDto
        {
            Equipo = MapToDto(equipo),
            ChecklistsActivos = checklists
        };
        // El token no se devuelve en la respuesta pública
        dto.Equipo.QRCode = null;

        return Ok(ApiResponse<ConsultaQrDto>.SuccessResponse(dto));
    }

    private static string GenerarToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private string BuildUrlConsulta(string token)
    {
        var baseUrl = (_configuration["Cliente:BaseUrl"] ?? "https://localhost:7267").TrimEnd('/');
        return $"{baseUrl}/equipos/qr/{token}";
    }

    private EquipoQrDto MapToQrDto(Equipo e, bool esNuevo) => new()
    {
        IdEquipo = e.IdEquipo,
        Token = e.QRCode ?? string.Empty,
        UrlConsulta = BuildUrlConsulta(e.QRCode ?? string.Empty),
        EsNuevo = esNuevo
    };

    private static EquipoDto MapToDto(Equipo e) => new()
    {
        IdEquipo = e.IdEquipo,
        CodigoInterno = e.CodigoInterno,
        ActivoFijo = e.ActivoFijo,
        SerialFabricante = e.SerialFabricante,
        NombreEquipo = e.NombreEquipo,
        Marca = e.Marca,
        Modelo = e.Modelo,
        Fabricante = e.Fabricante,
        Criticidad = e.Criticidad,
        IdCategoria = e.IdCategoria,
        CategoriaNombre = e.Categoria != null ? e.Categoria.Nombre : null,
        IdUbicacion = e.IdUbicacion,
        UbicacionNombre = e.Ubicacion != null ? e.Ubicacion.Planta + " - " + e.Ubicacion.Area : null,
        QRCode = e.QRCode,
        Activo = e.Activo,
        FechaCreacion = e.FechaCreacion
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
