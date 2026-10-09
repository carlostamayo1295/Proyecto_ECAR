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
public class HallazgosController : ControllerBase
{
    private readonly ECARDbContext _context;

    private const string EstadoAbierto = "Abierto";
    private const string EstadoEnProceso = "EnProceso";
    private const string EstadoCerrado = "Cerrado";
    private const string EstadoAnulado = "Anulado";

    public HallazgosController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<HallazgoDto>>>> GetHallazgos(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? search = null, 
        [FromQuery] long? idInspeccion = null, 
        [FromQuery] string? estado = null,
        [FromQuery] string? criticidad = null)
    {
        // Máximo 100 por página, como en el resto de listados: sin límite, pageSize=100000 devolvía la tabla entera.
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .AsQueryable();

        if (idInspeccion.HasValue)
        {
            query = query.Where(h => h.IdInspeccion == idInspeccion.Value);
        }

        if (!string.IsNullOrEmpty(estado))
        {
            query = query.Where(h => h.Estado == estado);
        }

        if (!string.IsNullOrEmpty(criticidad))
        {
            query = query.Where(h => h.Criticidad == criticidad);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(h =>
                h.Descripcion.Contains(search) ||
                h.Inspeccion.Equipo.NombreEquipo.Contains(search) ||
                (h.Criticidad != null && h.Criticidad.Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var hallazgos = await query
            .OrderByDescending(h => h.FechaRegistro)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new HallazgoDto
            {
                IdHallazgo = h.IdHallazgo,
                IdInspeccion = h.IdInspeccion,
                IdPregunta = h.IdPregunta,
                NombreEquipo = h.Inspeccion.Equipo.NombreEquipo,
                Descripcion = h.Descripcion,
                Criticidad = h.Criticidad,
                Estado = h.Estado,
                ResponsableId = h.ResponsableId,
                FechaCompromiso = h.FechaCompromiso,
                AccionCorrectiva = h.AccionCorrectiva,
                MotivoAnulacion = h.MotivoAnulacion,
                FechaRegistro = h.FechaRegistro
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<HallazgoDto>
        {
            Data = hallazgos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<HallazgoDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> GetHallazgo(long id)
    {
        var hallazgo = await _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo)));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CreateHallazgo(CreateHallazgoDto createDto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .FirstOrDefaultAsync(i => i.IdInspeccion == createDto.IdInspeccion);

        if (inspeccion == null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La inspección indicada no existe"));
        }

        var hallazgo = new Hallazgo
        {
            IdInspeccion = createDto.IdInspeccion,
            IdPregunta = createDto.IdPregunta,
            Descripcion = createDto.Descripcion,
            Criticidad = createDto.Criticidad,
            Estado = EstadoAbierto,
            ResponsableId = createDto.ResponsableId,
            FechaCompromiso = createDto.FechaCompromiso,
            FechaRegistro = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Hallazgos.Add(hallazgo);
        await _context.SaveChangesAsync();

        await _context.Entry(hallazgo).Reference(h => h.Inspeccion).LoadAsync();
        if (hallazgo.Inspeccion != null)
        {
            await _context.Entry(hallazgo.Inspeccion).Reference(i => i.Equipo).LoadAsync();
        }

        return CreatedAtAction(nameof(GetHallazgo), new { id = hallazgo.IdHallazgo },
            ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo), "Hallazgo registrado exitosamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> UpdateHallazgo(long id, UpdateHallazgoDto updateDto)
    {
        var hallazgo = await _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Descripcion))
            hallazgo.Descripcion = updateDto.Descripcion;

        if (updateDto.Criticidad != null)
            hallazgo.Criticidad = updateDto.Criticidad;

        if (!string.IsNullOrWhiteSpace(updateDto.Estado))
        {
            if (updateDto.Estado != EstadoAbierto && 
                updateDto.Estado != EstadoEnProceso && 
                updateDto.Estado != EstadoCerrado && 
                updateDto.Estado != EstadoAnulado)
            {
                return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse($"Estado inválido. Permitidos: {EstadoAbierto}, {EstadoEnProceso}, {EstadoCerrado}, {EstadoAnulado}"));
            }
            hallazgo.Estado = updateDto.Estado;
        }

        if (updateDto.ResponsableId != null)
            hallazgo.ResponsableId = updateDto.ResponsableId;

        if (updateDto.FechaCompromiso.HasValue)
            hallazgo.FechaCompromiso = updateDto.FechaCompromiso;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo), "Hallazgo actualizado exitosamente"));
    }

    // Transacción específica: Cerrar (Exige Acción Correctiva -> Valida 409 y 400)
    [HttpPatch("{id}/cerrar")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CerrarHallazgo(long id, [FromBody] CerrarHallazgoDto dto)
    {
        var hallazgo = await _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));

        if (hallazgo.Estado == EstadoCerrado || hallazgo.Estado == EstadoAnulado)
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse("El hallazgo ya se encuentra en un estado final y no puede cerrarse.")); // 409

        if (string.IsNullOrWhiteSpace(dto.AccionCorrectiva))
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La acción correctiva es obligatoria para cerrar el hallazgo.")); // 400

        hallazgo.Estado = EstadoCerrado;
        hallazgo.AccionCorrectiva = dto.AccionCorrectiva;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo), "Hallazgo cerrado exitosamente"));
    }

    // Transacción específica: Anular (Exige Motivo -> Valida 409 y 400)
    [HttpPatch("{id}/anular")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> AnularHallazgo(long id, [FromBody] AnularHallazgoDto dto)
    {
        var hallazgo = await _context.Hallazgos
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));

        if (hallazgo.Estado == EstadoCerrado || hallazgo.Estado == EstadoAnulado)
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse("El hallazgo ya está cerrado o anulado y no se puede modificar.")); // 409

        if (string.IsNullOrWhiteSpace(dto.MotivoAnulacion))
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("El motivo de anulación es obligatorio.")); // 400

        hallazgo.Estado = EstadoAnulado;
        hallazgo.MotivoAnulacion = dto.MotivoAnulacion;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(hallazgo), "Hallazgo anulado exitosamente"));
    }

    // Borrado Lógico estricto (Nunca se eliminan físicamente de la base de datos)
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteHallazgo(long id)
    {
        var hallazgo = await _context.Hallazgos.IgnoreQueryFilters().FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Hallazgo no encontrado"));
        }

        hallazgo.IsDeleted = true;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Hallazgo enviado a papelera (borrado lógico) exitosamente"));
    }

    private static HallazgoDto MapToDto(Hallazgo h)
    {
        return new HallazgoDto
        {
            IdHallazgo = h.IdHallazgo,
            IdInspeccion = h.IdInspeccion,
            IdPregunta = h.IdPregunta,
            NombreEquipo = h.Inspeccion?.Equipo?.NombreEquipo,
            Descripcion = h.Descripcion,
            Criticidad = h.Criticidad,
            Estado = h.Estado,
            ResponsableId = h.ResponsableId,
            FechaCompromiso = h.FechaCompromiso,
            AccionCorrectiva = h.AccionCorrectiva,
            MotivoAnulacion = h.MotivoAnulacion,
            FechaRegistro = h.FechaRegistro
        };
    }
}