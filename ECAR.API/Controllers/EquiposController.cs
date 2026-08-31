using ECAR.Infrastructure.Data;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EquiposController : ControllerBase
{
    private readonly ECARDbContext _context;

    public EquiposController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EquipoDto>>>> GetEquipos([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        var query = _context.Equipos.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(e =>
                e.NombreEquipo.Contains(search) ||
                e.CodigoInterno.Contains(search) ||
                e.ActivoFijo.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var equipos = await query
            .OrderBy(e => e.NombreEquipo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EquipoDto
            {
                IdEquipo = e.IdEquipo,
                CodigoInterno = e.CodigoInterno,
                ActivoFijo = e.ActivoFijo,
                NombreEquipo = e.NombreEquipo,
                Marca = e.Marca,
                Modelo = e.Modelo,
                Criticidad = e.Criticidad,
                Activo = e.Activo
            })
            .ToListAsync();

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
        var equipo = await _context.Equipos.FindAsync(id);

        if (equipo == null)
        {
            return NotFound(ApiResponse<EquipoDto>.ErrorResponse("Equipo no encontrado"));
        }

        var equipoDto = new EquipoDto
        {
            IdEquipo = equipo.IdEquipo,
            CodigoInterno = equipo.CodigoInterno,
            ActivoFijo = equipo.ActivoFijo,
            NombreEquipo = equipo.NombreEquipo,
            Marca = equipo.Marca,
            Modelo = equipo.Modelo,
            Criticidad = equipo.Criticidad,
            Activo = equipo.Activo
        };

        return Ok(ApiResponse<EquipoDto>.SuccessResponse(equipoDto));
    }
}
