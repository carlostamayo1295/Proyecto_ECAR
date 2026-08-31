using ECAR.Infrastructure.Data;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditoriaController : ControllerBase
{
    private readonly ECARDbContext _context;

    public AuditoriaController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<AuditoriaDto>>>> GetAuditoria([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        var query = _context.Auditoria.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a =>
                a.Tabla.Contains(search) ||
                a.Accion.Contains(search) ||
                a.Usuario.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var registros = await query
            .OrderByDescending(a => a.FechaHora)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditoriaDto
            {
                IdAuditoria = a.IdAuditoria,
                Tabla = a.Tabla,
                RegistroId = a.RegistroId,
                Accion = a.Accion,
                ValorAnterior = a.ValorAnterior,
                ValorNuevo = a.ValorNuevo,
                Usuario = a.Usuario,
                FechaHora = a.FechaHora
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<AuditoriaDto>
        {
            Data = registros,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<AuditoriaDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<AuditoriaDto>>> GetAuditoriaRegistro(long id)
    {
        var registro = await _context.Auditoria.FindAsync(id);

        if (registro == null)
        {
            return NotFound(ApiResponse<AuditoriaDto>.ErrorResponse("Registro de auditoría no encontrado"));
        }

        var registroDto = new AuditoriaDto
        {
            IdAuditoria = registro.IdAuditoria,
            Tabla = registro.Tabla,
            RegistroId = registro.RegistroId,
            Accion = registro.Accion,
            ValorAnterior = registro.ValorAnterior,
            ValorNuevo = registro.ValorNuevo,
            Usuario = registro.Usuario,
            FechaHora = registro.FechaHora
        };

        return Ok(ApiResponse<AuditoriaDto>.SuccessResponse(registroDto));
    }
}
