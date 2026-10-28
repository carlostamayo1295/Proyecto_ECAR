using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.API.Configuration;
using ECAR.API.Services;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RolesSistema.Todos)]
public class EvidenciasController : ControllerBase
{
    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IEvidenciaStorage _storage;
    private readonly EvidenciasOptions _options;
    private readonly IMotivoCambio _motivoCambio;

    public EvidenciasController(
        ECARDbContext context,
        ICurrentUser currentUser,
        IEvidenciaStorage storage,
        IOptions<EvidenciasOptions> options,
        IMotivoCambio motivoCambio)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
        _options = options.Value;
        _motivoCambio = motivoCambio;
    }

    /// <summary>
    /// Las fotos de cualquier inspección, las haya subido quien las haya subido, las ven el
    /// Administrador, el Supervisor y el Estándar (28/10/2026; antes el Estándar veía solo las de sus
    /// inspecciones y las de las ajenas con novedades). El Operador, solo las de sus inspecciones.
    /// Verlas no es retirarlas (<see cref="RetirarEvidencia"/>).
    /// </summary>
    private bool VeTodas =>
        User.IsInRole(RolesSistema.Administrador) || User.IsInRole(RolesSistema.Supervisor) || User.IsInRole(RolesSistema.Estandar);

    /// <summary>
    /// Las fotos retiradas (§3.6) solo las ven el Administrador y el Supervisor, marcadas; para el
    /// Operador, el Estándar y en la ejecución desaparecen, aunque se conservan.
    /// </summary>
    private bool VeRetiradas => User.IsInRole(RolesSistema.Administrador) || User.IsInRole(RolesSistema.Supervisor);

    // GET: api/evidencias O api/inspecciones/{idInspeccion}/evidencias
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EvidenciaDto>>>> GetEvidencias(
        [FromQuery] long? idInspeccion,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Evidencias
            .Include(e => e.UsuarioCargaDetalle)
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .AsQueryable();

        if (!VeRetiradas)
        {
            query = query.Where(e => !e.Retirada);
        }

        if (idInspeccion.HasValue && idInspeccion.Value > 0)
        {
            query = query.Where(e => e.IdInspeccion == idInspeccion.Value);
        }

        // El Operador, las de sus inspecciones
        if (!VeTodas)
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
                IdUsuarioInspeccion = e.Inspeccion.IdUsuario,
                EstadoInspeccion = e.Inspeccion.Estado ?? string.Empty,
                Retirada = e.Retirada,
                FechaRetiro = e.FechaRetiro,
                MotivoRetiro = e.MotivoRetiro,
                UsuarioRetiro = e.UsuarioRetiro != null ? e.UsuarioRetiro.Nombre : null
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
    /// <summary>
    /// Evidencias de una inspección (endpoint 8 del contrato, PLAN_FASE3_TAREAS §3.2): una lista,
    /// no una página. Antes esta ruta compartía acción con el listado paginado y devolvía
    /// PagedResultDto, y el cliente, que deserializa List&lt;EvidenciaDto&gt;, recibía null.
    /// Un Operador ve las de sus inspecciones; el Estándar, como el Administrador y el Supervisor, las
    /// de cualquiera (<see cref="VeTodas"/>), como en GET {id}/archivo.
    /// </summary>
    [HttpGet("/api/inspecciones/{idInspeccion:long}/evidencias")]
    public async Task<ActionResult<ApiResponse<List<EvidenciaDto>>>> GetEvidenciasInspeccion(long idInspeccion)
    {
        var inspeccion = await _context.Inspecciones
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.IdInspeccion == idInspeccion);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<List<EvidenciaDto>>.ErrorResponse("Inspección no encontrada"));
        }

        if (!VeTodas && inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        var evidencias = await _context.Evidencias
            .Where(e => e.IdInspeccion == idInspeccion && !e.Retirada)
            .OrderBy(e => e.FechaCarga)
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
                IdUsuarioInspeccion = e.Inspeccion.IdUsuario,
                EstadoInspeccion = e.Inspeccion.Estado ?? string.Empty
            })
            .ToListAsync();

        return Ok(ApiResponse<List<EvidenciaDto>>.SuccessResponse(evidencias));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<EvidenciaDto>>> GetEvidencia(long id)
    {
        var evidencia = await _context.Evidencias
            .Include(e => e.UsuarioCargaDetalle)
            .Include(e => e.UsuarioRetiro)
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .FirstOrDefaultAsync(e => e.IdEvidencia == id);

        if (evidencia == null)
        {
            return NotFound(ApiResponse<EvidenciaDto>.ErrorResponse("Evidencia no encontrada"));
        }

        if (!VeTodas && evidencia.IdUsuarioCarga != _currentUser.IdUsuario
            && evidencia.Inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        if (evidencia.Retirada && !VeRetiradas)
        {
            return NotFound(ApiResponse<EvidenciaDto>.ErrorResponse("Evidencia no encontrada"));
        }

        return Ok(ApiResponse<EvidenciaDto>.SuccessResponse(MapToDto(evidencia)));
    }

    // GET: api/evidencias/{id}/archivo
    [HttpGet("{id:long}/archivo")]
    public async Task<IActionResult> GetArchivo(long id)
    {
        var evidencia = await _context.Evidencias
            .Include(e => e.Inspeccion)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdEvidencia == id);

        if (evidencia == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Evidencia no encontrada"));
        }

        if (!VeTodas && evidencia.Inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        if (evidencia.Retirada && !VeRetiradas)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Evidencia no encontrada"));
        }

        try
        {
            var stream = await _storage.AbrirAsync(evidencia.Archivo);
            return File(stream, evidencia.TipoContenido, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("El archivo de evidencia no existe en el almacenamiento"));
        }
    }

    // POST: api/inspecciones/{idInspeccion}/evidencias
    [HttpPost("/api/inspecciones/{idInspeccion:long}/evidencias")]
   [Authorize(Roles = RolesSistema.Inspeccionan)]
    public async Task<ActionResult<ApiResponse<EvidenciaDto>>> CreateEvidencia(
        long idInspeccion,
        [FromForm] IFormFile archivo)
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

        var maxBytes = Math.Max(1, _options.TamanoMaximoMB) * 1024L * 1024L;
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

        extension = esJpg ? ".jpg" : ".png";
        var tipoContenido = esJpg ? "image/jpeg" : "image/png";

        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .FirstOrDefaultAsync(i => i.IdInspeccion == idInspeccion);

        if (inspeccion == null)
        {
            return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("La inspección indicada no existe."));
        }

        // Regla 6 del SRS: sobre una inspección cerrada (o anulada) no se añaden evidencias.
        if (inspeccion.Estado != InspeccionEstados.EnCurso)
        {
            return Conflict(ApiResponse<EvidenciaDto>.ErrorResponse(
                InmutabilidadInspeccion.MensajeSiNoAdmiteCambios(inspeccion.Estado) ?? InmutabilidadInspeccion.MensajeCerrada));
        }

        if (!User.IsInRole(RolesSistema.Administrador) && inspeccion.IdUsuario != _currentUser.IdUsuario)
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
            TipoContenido = tipoContenido,
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
            IdUsuarioInspeccion = inspeccion.IdUsuario,
            EstadoInspeccion = inspeccion.Estado ?? string.Empty
        };

        return CreatedAtAction(nameof(GetEvidencia), new { id = evidencia.IdEvidencia },
            ApiResponse<EvidenciaDto>.SuccessResponse(evidenciaDto, "Evidencia cargada exitosamente"));
    }
    
    /// <summary>
    /// Endpoint 11 (PLAN_FASE4_TAREAS §3.6): una foto quitada antes de firmar se marca como
    /// retirada, con motivo, y el archivo se conserva. Solo en una inspección en curso, y solo el
    /// Administrador o el usuario que la subió o ejecuta la inspección. No hay DELETE (405).
    /// </summary>
    [HttpPost("{id:long}/retirar")]
    [Authorize(Roles = RolesSistema.Inspeccionan)]
    public async Task<ActionResult<ApiResponse<EvidenciaDto>>> RetirarEvidencia(long id, MotivoDto dto)
    {
        var evidencia = await _context.Evidencias
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .Include(e => e.UsuarioCargaDetalle)
            .FirstOrDefaultAsync(e => e.IdEvidencia == id);

        if (evidencia == null)
        {
            return NotFound(ApiResponse<EvidenciaDto>.ErrorResponse("Evidencia no encontrada"));
        }

        // Regla 6: las evidencias de una inspección firmada (o anulada) no se tocan.
        if (evidencia.Inspeccion.Estado != InspeccionEstados.EnCurso)
        {
            return Conflict(ApiResponse<EvidenciaDto>.ErrorResponse(
                evidencia.Inspeccion.Estado == InspeccionEstados.Anulada
                    ? InmutabilidadInspeccion.MensajeAnulada
                    : InmutabilidadInspeccion.MensajeCerrada));
        }

        if (!User.IsInRole(RolesSistema.Administrador)
            && evidencia.IdUsuarioCarga != _currentUser.IdUsuario
            && evidencia.Inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        if (evidencia.Retirada)
        {
            return Conflict(ApiResponse<EvidenciaDto>.ErrorResponse("La evidencia ya está retirada"));
        }

        _motivoCambio.Establecer(dto.Motivo);
        evidencia.Retirada = true;
        evidencia.FechaRetiro = DateTime.UtcNow;
        evidencia.IdUsuarioRetiro = _currentUser.IdUsuario;
        evidencia.MotivoRetiro = dto.Motivo.Trim();
        await _context.SaveChangesAsync();

        await _context.Entry(evidencia).Reference(e => e.UsuarioRetiro).LoadAsync();
        return Ok(ApiResponse<EvidenciaDto>.SuccessResponse(MapToDto(evidencia), "Evidencia retirada; el archivo se conserva"));
    }

    private static EvidenciaDto MapToDto(Evidencia evidencia) => new()
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
        IdUsuarioInspeccion = evidencia.Inspeccion?.IdUsuario ?? 0,
        EstadoInspeccion = evidencia.Inspeccion?.Estado ?? string.Empty,
        Retirada = evidencia.Retirada,
        FechaRetiro = evidencia.FechaRetiro,
        MotivoRetiro = evidencia.MotivoRetiro,
        UsuarioRetiro = evidencia.UsuarioRetiro?.Nombre
    };
}
