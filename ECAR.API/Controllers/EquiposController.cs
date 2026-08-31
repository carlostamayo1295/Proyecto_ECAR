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
public class EquiposController : ControllerBase
{
    private readonly ECARDbContext _context;

    public EquiposController(ECARDbContext context)
    {
        _context = context;
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
            QRCode = NormalizeOptional(createDto.QRCode),
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

        if (updateDto.QRCode != null)
            equipo.QRCode = NormalizeOptional(updateDto.QRCode);

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

        // Keep the record for inspection history and mark it as inactive.
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
