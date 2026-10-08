using ECAR.Infrastructure.Data;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Auditor")]
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

    // ECAR-212: Consultar el historial completo de un registro específico por tabla y ID
    [HttpGet("historial/{tabla}/{registroId}")]
    public async Task<ActionResult<ApiResponse<List<AuditoriaDto>>>> GetHistorialRegistro(string tabla, long registroId)
    {
        var historial = await _context.Auditoria
            .Where(a => a.Tabla.ToLower() == tabla.ToLower() && a.RegistroId == registroId)
            .OrderBy(a => a.FechaHora)
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

        return Ok(ApiResponse<List<AuditoriaDto>>.SuccessResponse(historial));
    }

    // ECAR-212: Verificar la integridad de la cadena de auditoría (detecta anomalías o saltos de secuencia)
    [HttpGet("verificar-integridad")]
    public async Task<ActionResult<ApiResponse<object>>> VerificarIntegridad()
    {
        var registros = await _context.Auditoria
            .OrderBy(a => a.IdAuditoria)
            .ToListAsync();

        bool integridadValida = true;
        string mensaje = "La cadena de auditoría es íntegra y consistente.";
        long? idAnterior = null;
        DateTime? fechaAnterior = null;

        foreach (var reg in registros)
        {
            if (idAnterior.HasValue && reg.IdAuditoria != idAnterior.Value + 1)
            {
                integridadValida = false;
                mensaje = $"Discrepancia detectada en el ID de auditoría: salto encontrado después de {idAnterior.Value}.";
                break;
            }

            if (fechaAnterior.HasValue && reg.FechaHora < fechaAnterior.Value)
            {
                integridadValida = false;
                mensaje = $"Inconsistencia cronológica detectada en el registro ID {reg.IdAuditoria}.";
                break;
            }

            idAnterior = reg.IdAuditoria;
            fechaAnterior = reg.FechaHora;
        }

        var resultado = new
        {
            IntegridadValida = integridadValida,
            TotalRegistrosVerificados = registros.Count,
            Mensaje = mensaje
        };

        return Ok(ApiResponse<object>.SuccessResponse(resultado, "Verificación de integridad ejecutada"));
    }
}