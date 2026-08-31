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
        [FromQuery] string? criticidad = null)
    {
        var query = _context.Equipos
            .Include(e => e.Categoria)
            .Include(e => e.Ubicacion)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(e =>
                e.CodigoInterno.Contains(search) ||
                e.ActivoFijo.Contains(search) ||
                e.NombreEquipo.Contains(search) ||
                (e.Marca != null && e.Marca.Contains(search)) ||
                (e.Modelo != null && e.Modelo.Contains(search)) ||
                (e.SerialFabricante != null && e.SerialFabricante.Contains(search)));
        }

        if (!string.IsNullOrEmpty(criticidad))
        {
            query = query.Where(e => e.Criticidad == criticidad);
        }

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
    public async Task<ActionResult<ApiResponse<EquipoDto>>> CreateEquipo(CreateEquipoDto createDto)
    {
        if (await _context.Equipos.AnyAsync(e => e.CodigoInterno == createDto.CodigoInterno))
        {
            return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El código interno ya está registrado"));
        }

        if (await _context.Equipos.AnyAsync(e => e.ActivoFijo == createDto.ActivoFijo))
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
            CodigoInterno = createDto.CodigoInterno,
            ActivoFijo = createDto.ActivoFijo,
            SerialFabricante = createDto.SerialFabricante,
            NombreEquipo = createDto.NombreEquipo,
            Marca = createDto.Marca,
            Modelo = createDto.Modelo,
            Fabricante = createDto.Fabricante,
            Criticidad = createDto.Criticidad,
            IdCategoria = createDto.IdCategoria,
            IdUbicacion = createDto.IdUbicacion,
            QRCode = createDto.QRCode,
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
    public async Task<ActionResult<ApiResponse<EquipoDto>>> UpdateEquipo(long id, UpdateEquipoDto updateDto)
    {
        var equipo = await _context.Equipos.FindAsync(id);

        if (equipo == null)
        {
            return NotFound(ApiResponse<EquipoDto>.ErrorResponse("Equipo no encontrado"));
        }

        if (!string.IsNullOrEmpty(updateDto.CodigoInterno) && updateDto.CodigoInterno != equipo.CodigoInterno)
        {
            if (await _context.Equipos.AnyAsync(e => e.CodigoInterno == updateDto.CodigoInterno && e.IdEquipo != id))
            {
                return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El código interno ya está registrado"));
            }
            equipo.CodigoInterno = updateDto.CodigoInterno;
        }

        if (!string.IsNullOrEmpty(updateDto.ActivoFijo) && updateDto.ActivoFijo != equipo.ActivoFijo)
        {
            if (await _context.Equipos.AnyAsync(e => e.ActivoFijo == updateDto.ActivoFijo && e.IdEquipo != id))
            {
                return BadRequest(ApiResponse<EquipoDto>.ErrorResponse("El activo fijo ya está registrado"));
            }
            equipo.ActivoFijo = updateDto.ActivoFijo;
        }

        if (!string.IsNullOrEmpty(updateDto.NombreEquipo))
            equipo.NombreEquipo = updateDto.NombreEquipo;

        if (updateDto.SerialFabricante != null)
            equipo.SerialFabricante = updateDto.SerialFabricante;

        if (updateDto.Marca != null)
            equipo.Marca = updateDto.Marca;

        if (updateDto.Modelo != null)
            equipo.Modelo = updateDto.Modelo;

        if (updateDto.Fabricante != null)
            equipo.Fabricante = updateDto.Fabricante;

        if (updateDto.Criticidad != null)
            equipo.Criticidad = updateDto.Criticidad;

        if (updateDto.QRCode != null)
            equipo.QRCode = updateDto.QRCode;

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
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEquipo(long id)
    {
        var equipo = await _context.Equipos.FindAsync(id);

        if (equipo == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Equipo no encontrado"));
        }

        // Baja lógica: los equipos con historial de inspecciones no se eliminan físicamente.
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
}
