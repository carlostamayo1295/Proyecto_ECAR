using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.API.Services;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class EvidenciasController : ControllerBase
{
    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IEvidenciaStorage _storage;
    private readonly IConfiguration _configuration;

    public EvidenciasController(
        ECARDbContext context,
        ICurrentUser currentUser,
        IEvidenciaStorage storage,
        IConfiguration configuration)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
        _configuration = configuration;
    }

    // GET: api/evidencias O api/inspecciones/{idInspeccion}/evidencias
    [HttpGet]
    [HttpGet("/api/inspecciones/{idInspeccion:long}/evidencias")]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EvidenciaDto>>>> GetEvidencias(
        [FromRoute] long? idInspeccion,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var query = _context.Evidencias
            .Include(e => e.UsuarioCargaDetalle)
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .AsQueryable();

        if (idInspeccion.HasValue && idInspeccion.Value > 0)
        {
            query = query.Where(e => e.IdInspeccion == idInspeccion.Value);
        }

        // Filtro si es Técnico
        if (User.IsInRole("Técnico"))
        {
            query = query.Where(e => e.IdUsuarioCarga == _currentUser.IdUsuario || e.Inspeccion.IdUsuario == _currentUser.IdUsuario);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(e =>
                e.Inspeccion.Equipo.NombreEquipo.Contains(search) ||
                e.UsuarioCargaDetalle.Nombre.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var evidencias = await query
            .OrderByDescending(e => e.FechaCarga)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EvidenciaDto
            {
                IdEvidencia = e.IdEvidencia,
                IdInspeccion = e.IdInspeccion,
                NombreEquipo = e.Inspeccion.Equipo.NombreEquipo,
                Archivo = e.Archivo,
                NombreOriginal = e.NombreOriginal,
                TipoContenido = e.TipoContenido,
                TamanoBytes = e.TamanoBytes,
                FechaCarga = e.FechaCarga,
                IdUsuarioCarga = e.IdUsuarioCarga,
                UsuarioCarga = e.UsuarioCargaDetalle.Nombre,
                EstadoInspeccion = e.Inspeccion.Estado ?? string.Empty
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<EvidenciaDto>
        {
            Data = evidencias,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<EvidenciaDto>>.SuccessResponse(pagedResult));
    }

    // GET: api/evidencias/{id}
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<EvidenciaDto>>> GetEvidencia(long id)
    {
        var evidencia = await _context.Evidencias
            .Include(e => e.UsuarioCargaDetalle)
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(e => e.IdEvidencia == id);

        if (evidencia == null)
        {
            return NotFound(ApiResponse<EvidenciaDto>.ErrorResponse("Evidencia no encontrada"));
        }

        if (User.IsInRole("Técnico") && evidencia.IdUsuarioCarga != _currentUser.IdUsuario && evidencia.Inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        var evidenciaDto = new EvidenciaDto
        {
            IdEvidencia = evidencia.IdEvidencia,
            IdInspeccion = evidencia.IdInspeccion,
            NombreEquipo = evidencia.Inspeccion?.Equipo?.NombreEquipo,
            Archivo = evidencia.Archivo,
            NombreOriginal = evidencia.NombreOriginal,
            TipoContenido = evidencia.TipoContenido,
            TamanoBytes = evidencia.TamanoBytes,
            FechaCarga = evidencia.FechaCarga,
            IdUsuarioCarga = evidencia.IdUsuarioCarga,
            UsuarioCarga = evidencia.UsuarioCargaDetalle?.Nombre ?? string.Empty,
            EstadoInspeccion = evidencia.Inspeccion?.Estado ?? string.Empty
        };

        return Ok(ApiResponse<EvidenciaDto>.SuccessResponse(evidenciaDto));
    }

    // POST: api/inspecciones/{idInspeccion}/evidencias
    [HttpPost("/api/inspecciones/{idInspeccion:long}/evidencias")]
    public async Task<ActionResult<ApiResponse<EvidenciaDto>>> CreateEvidencia(
        long idInspeccion,
        IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("No se envió ningún archivo."));
        }

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("Formato inválido. Solo se permiten JPG o PNG."));
        }

        var maxBytes = _configuration.GetValue<long?>("FileStorage:MaxSizeBytes") ?? (5 * 1024 * 1024);
        if (archivo.Length > maxBytes)
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse($"El archivo supera el límite permitido de {maxBytes / (1024 * 1024)} MB."));
        }

        using var memoryStream = new MemoryStream();
        await archivo.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        if (bytes.Length < 4)
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("El archivo está vacío o incompleto."));
        }

        bool esPdf = bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
        bool esJpg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        bool esPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

        if (esPdf || (!esJpg && !esPng))
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("El archivo no es una imagen válida o es un PDF disfrazado."));
        }

        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .FirstOrDefaultAsync(i => i.IdInspeccion == idInspeccion);

        if (inspeccion == null)
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("La inspección indicada no existe."));
        }

        // Regla 6 del SRS: sobre una inspección cerrada no se añaden evidencias.
        if (inspeccion.Estado != InspeccionEstados.EnCurso)
        {
            return Conflict(ApiResponse<EvidenciaDto>.ErrorResponse(InmutabilidadInspeccion.MensajeCerrada));
        }

        if (User.IsInRole("Técnico") && inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        memoryStream.Position = 0;
        var rutaRelativa = await _storage.GuardarAsync(memoryStream, extension, idInspeccion);
        var evidencia = new Evidencia
        {
            IdInspeccion = idInspeccion,
            Archivo = rutaRelativa,
            NombreOriginal = Path.GetFileName(archivo.FileName),
            TipoContenido = archivo.ContentType,
            TamanoBytes = archivo.Length,
            IdUsuarioCarga = _currentUser.IdUsuario,
            FechaCarga = DateTime.UtcNow
        };

        try
        {
            _context.Evidencias.Add(evidencia);
            await _context.SaveChangesAsync();
        }
        catch
        {
            await _storage.EliminarAsync(rutaRelativa);
            throw;
        }

        var evidenciaDto = new EvidenciaDto
        {
            IdEvidencia = evidencia.IdEvidencia,
            IdInspeccion = evidencia.IdInspeccion,
            NombreEquipo = inspeccion.Equipo?.NombreEquipo,
            Archivo = evidencia.Archivo,
            NombreOriginal = evidencia.NombreOriginal,
            TipoContenido = evidencia.TipoContenido,
            TamanoBytes = evidencia.TamanoBytes,
            FechaCarga = evidencia.FechaCarga,
            IdUsuarioCarga = evidencia.IdUsuarioCarga,
            UsuarioCarga = _currentUser.Nombre,
            EstadoInspeccion = inspeccion.Estado ?? string.Empty
        };

        return CreatedAtAction(nameof(GetEvidencia), new { id = evidencia.IdEvidencia },
            ApiResponse<EvidenciaDto>.SuccessResponse(evidenciaDto, "Evidencia cargada exitosamente"));
    }

    // DELETE: api/evidencias/{id}
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEvidencia(long id)
    {
        var evidencia = await _context.Evidencias
            .Include(e => e.Inspeccion)
            .FirstOrDefaultAsync(e => e.IdEvidencia == id);

        if (evidencia == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Evidencia no encontrada"));
        }

        // Regla 6 del SRS: tampoco se borran las evidencias de una inspección cerrada.
        if (await _context.ObtenerEstadoEscrituraAsync(evidencia.IdInspeccion) == EstadoEscritura.Cerrada)
        {
            return Conflict(ApiResponse<bool>.ErrorResponse(InmutabilidadInspeccion.MensajeCerrada));
        }

        if (!User.IsInRole("Administrador")
            && evidencia.IdUsuarioCarga != _currentUser.IdUsuario
            && evidencia.Inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        var rutaRelativa = evidencia.Archivo;

        _context.Evidencias.Remove(evidencia);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(rutaRelativa))
        {
            await _storage.EliminarAsync(rutaRelativa);
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Evidencia eliminada exitosamente"));
    }
}
