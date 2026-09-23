using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
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

    public EvidenciasController(ECARDbContext context, ICurrentUser currentUser, IEvidenciaStorage storage) 
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<EvidenciaDto>>>> GetEvidencias([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] long? idInspeccion = null)
    {
        var query = _context.Evidencias
            .Include(e => e.UsuarioCargaDetalle)
            .Include(e => e.Inspeccion)
                .ThenInclude(i => i.Equipo)
            .AsQueryable();

        if (idInspeccion.HasValue)
        {
            query = query.Where(e => e.IdInspeccion == idInspeccion.Value);
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
                UsuarioCarga = e.UsuarioCargaDetalle.Nombre
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

    [HttpGet("{id}")]
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
            UsuarioCarga = evidencia.UsuarioCargaDetalle.Nombre
        };

        return Ok(ApiResponse<EvidenciaDto>.SuccessResponse(evidenciaDto));
    }

    [HttpPost]
public async Task<ActionResult<ApiResponse<EvidenciaDto>>> CreateEvidencia([FromForm] long idInspeccion, IFormFile archivo)
{
    // 1. Validaciones del ticket ECAR-202
    if (archivo == null || archivo.Length == 0)
        return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("No se envió ningún archivo."));

    var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
    if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
        return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("Formato inválido. Solo se permiten JPG o PNG."));

    if (archivo.Length > 5 * 1024 * 1024)
        return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("El archivo supera el límite de 5 MB."));
    
    using var memoryStream = new MemoryStream();
    await archivo.CopyToAsync(memoryStream);
    var bytes = memoryStream.ToArray();

    if (bytes.Length < 4)
        return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("El archivo está vacío o incompleto."));

    bool esPdf = bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
    bool esJpg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    bool esPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

    if (esPdf || (!esJpg && !esPng))
    {
        return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("El archivo no es una imagen válida o es un PDF disfrazado."));
    }

    // 2. Buscar inspección y validar estado
    var inspeccion = await _context.Inspecciones
        .Include(i => i.Equipo)
        .FirstOrDefaultAsync(i => i.IdInspeccion == idInspeccion);

    if (inspeccion == null)
        return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("La inspección indicada no existe"));

    // NOTA: Asegúrate de que la propiedad de estado se llame así en tu entidad Inspeccion
    // Si tu lógica requiere validar que esté "En curso", descomenta esta línea:
    // if (inspeccion.Estado != "En curso") return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("La inspección no está En curso."));
    
    // Validar que el usuario actual tenga permiso o esté asociado a la inspección/equipo
// (Puedes cambiar 'inspeccion.IdTecnico' por el campo exacto que relacione al usuario con la inspección en tu base de datos)
    var idUsuarioActual = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");

    if (inspeccion.IdUsuario != idUsuarioActual)
    {
        return BadRequest(ApiResponse<EvidenciaDto>.ErrorResponse("No estás autorizado para subir evidencias a esta inspección."));
    }

    // 3. Guardar el archivo en el disco duro (Usando el servicio que creamos)
    using var stream = archivo.OpenReadStream();
    var rutaRelativa = await _storage.GuardarAsync(stream, extension, idInspeccion);

    // 4. Guardar en Base de Datos
    var evidencia = new Evidencia
    {
        IdInspeccion = idInspeccion,
        Archivo = rutaRelativa, // Ruta generada por el disco
        NombreOriginal = Path.GetFileName(archivo.FileName),
        TipoContenido = archivo.ContentType, // Ahora guardamos el tipo real
        TamanoBytes = archivo.Length,        // Ahora guardamos el tamaño real en bytes
        IdUsuarioCarga = _currentUser.IdUsuario,
        FechaCarga = DateTime.UtcNow
    };

    _context.Evidencias.Add(evidencia);
    await _context.SaveChangesAsync();

    // 5. Retornar el DTO (como lo tenías)
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
        UsuarioCarga = _currentUser.Nombre
    };

    return CreatedAtAction(nameof(GetEvidencia), new { id = evidencia.IdEvidencia }, 
        ApiResponse<EvidenciaDto>.SuccessResponse(evidenciaDto, "Evidencia cargada exitosamente"));
}

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEvidencia(long id)
    {
        var evidencia = await _context.Evidencias.FindAsync(id);
    
        if (evidencia == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Evidencia no encontrada"));
        }

        // 1. Eliminar el archivo físico del disco
        if (!string.IsNullOrEmpty(evidencia.Archivo))
        {
            await _storage.EliminarAsync(evidencia.Archivo);
        }

        // 2. Eliminar el registro de la base de datos
        _context.Evidencias.Remove(evidencia);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(data: true, message: "Evidencia eliminada exitosamente"));
    }
}
