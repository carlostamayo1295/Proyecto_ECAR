using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.API.Services;
using ECAR.API.Exceptions;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class InspeccionesController : ControllerBase
{
    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IInspeccionService _inspeccionService;

    public InspeccionesController(ECARDbContext context, ICurrentUser currentUser, IInspeccionService inspeccionService)
    {
        _context = context;
        _currentUser = currentUser;
        _inspeccionService = inspeccionService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<InspeccionDto>>>> GetInspecciones([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? estado = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .AsQueryable();

        if (EsTecnicoSinPrivilegiosDeLecturaGlobal())
        {
            var idUsuario = _currentUser.IdUsuario;
            query = query.Where(i => i.IdUsuario == idUsuario);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(i =>
                i.Equipo.NombreEquipo.Contains(search) ||
                i.Usuario.Nombre.Contains(search) ||
                (i.Resultado != null && i.Resultado.Contains(search)));
        }

        // Filtro por estado (EnCurso / Cerrada) antes de paginar. Sin esto el cliente filtraba
        // sobre la página ya cargada y podía decir "no hay inspecciones en curso" habiéndolas
        // en otras páginas.
        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(i => i.Estado == estado);
        }

        var totalCount = await query.CountAsync();

        var inspecciones = await query
            .OrderByDescending(i => i.FechaInspeccion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InspeccionDto
            {
                IdInspeccion = i.IdInspeccion,
                IdEquipo = i.IdEquipo,
                NombreEquipo = i.Equipo.NombreEquipo,
                IdUsuario = i.IdUsuario,
                NombreUsuario = i.Usuario.Nombre,
                IdChecklist = i.IdChecklist,
                NombreChecklist = i.Checklist.Nombre,
                FechaInspeccion = i.FechaInspeccion,
                Estado = i.Estado,
                FechaCierre = i.FechaCierre,
                Resultado = i.Resultado,
                Observaciones = i.Observaciones,
                TieneFirma = i.FirmaDigital != null && i.FirmaDigital != "",
                TotalEvidencias = i.Evidencias.Count,
                TotalHallazgos = i.Hallazgos.Count
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<InspeccionDto>
        {
            Data = inspecciones,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<InspeccionDto>>.SuccessResponse(pagedResult));
    }

    // Punto 2: GET /api/inspecciones/mias?estado=
    [HttpGet("mias")]
    [Authorize(Roles = "Técnico")]
    public async Task<ActionResult<ApiResponse<List<InspeccionDto>>>> GetMisInspecciones([FromQuery] string? estado)
    {
        var usuarioId = _currentUser.IdUsuario;
        var result = await _inspeccionService.ObtenerMisInspeccionesAsync(usuarioId, estado);
        return Ok(ApiResponse<List<InspeccionDto>>.SuccessResponse(result));
    }

    // Punto 1: GET /api/inspecciones/{id}/respuestas
    [HttpGet("{id}/respuestas")]
    public async Task<ActionResult<ApiResponse<List<RespuestaInspeccionDto>>>> GetRespuestas(long id)
    {
        try
        {
            var usuarioId = _currentUser.IdUsuario;
            var esAdmin = _currentUser.IsInRole("Administrador");
            var esAuditor = _currentUser.IsInRole("Auditor");

            var respuestas = await _inspeccionService.ObtenerRespuestasAsync(id, usuarioId, esAdmin, esAuditor);
            return Ok(ApiResponse<List<RespuestaInspeccionDto>>.SuccessResponse(respuestas));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<List<RespuestaInspeccionDto>>.ErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> GetInspeccion(long id)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Include(i => i.Evidencias)
            .Include(i => i.Hallazgos)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeLeer(inspeccion))
        {
            return Forbid();
        }

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion)));
    }

    [HttpPost("iniciar")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<InspeccionEjecucionDto>>> IniciarInspeccion(
        IniciarInspeccionDto iniciarDto)
    {
        var usuario = await _context.Usuarios.FindAsync(_currentUser.IdUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "El usuario autenticado no existe o no está habilitado en ECAR"));
        }

        var equipoExiste = await _context.Equipos.AnyAsync(e =>
            e.IdEquipo == iniciarDto.IdEquipo && e.Activo);
        if (!equipoExiste)
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "El equipo indicado no existe o no está activo"));
        }

        var checklistExiste = await _context.Checklists.AnyAsync(c =>
            c.IdChecklist == iniciarDto.IdChecklist && c.Activo);
        if (!checklistExiste)
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "El checklist indicado no existe o no está activo"));
        }

        var existente = await _context.Inspecciones
            .Where(i => i.IdEquipo == iniciarDto.IdEquipo
                && i.IdUsuario == usuario.IdUsuario
                && i.Estado == InspeccionEstados.EnCurso)
            .Select(i => i.IdInspeccion)
            .FirstOrDefaultAsync();

        if (existente != 0)
        {
            var ejecucionExistente = await CargarEjecucionAsync(existente);
            return Conflict(ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
                MapToEjecucionDto(ejecucionExistente!),
                "Ya existe una inspección en curso para este equipo; puede continuarla"));
        }

        var inspeccion = new Inspeccion
        {
            IdEquipo = iniciarDto.IdEquipo,
            IdChecklist = iniciarDto.IdChecklist,
            IdUsuario = usuario.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };

        _context.Inspecciones.Add(inspeccion);
        await _context.SaveChangesAsync();

        var ejecucion = await CargarEjecucionAsync(inspeccion.IdInspeccion);
        return CreatedAtAction(
            nameof(GetEjecucion),
            new { id = inspeccion.IdInspeccion },
            ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
                MapToEjecucionDto(ejecucion!),
                "Inspección iniciada exitosamente"));
    }

    [HttpGet("{id}/ejecucion")]
    public async Task<ActionResult<ApiResponse<InspeccionEjecucionDto>>> GetEjecucion(long id)
    {
        var inspeccion = await CargarEjecucionAsync(id);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "Inspección no encontrada"));
        }

        if (!PuedeLeer(inspeccion))
        {
            return Forbid();
        }

        return Ok(ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
            MapToEjecucionDto(inspeccion)));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> CreateInspeccion(CreateInspeccionDto createDto)
    {
        var equipo = await _context.Equipos.FindAsync(createDto.IdEquipo);
        if (equipo == null)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("El equipo indicado no existe"));
        }

        var usuario = await _context.Usuarios.FindAsync(_currentUser.IdUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("El usuario autenticado no existe o no está habilitado en ECAR"));
        }

        var checklist = await _context.Checklists.FirstOrDefaultAsync(c =>
            c.IdChecklist == createDto.IdChecklist && c.Activo);
        if (checklist == null)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse(
                "El checklist indicado no existe o no está activo"));
        }

        var inspeccion = new Inspeccion
        {
            IdEquipo = createDto.IdEquipo,
            IdUsuario = usuario.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = createDto.FechaInspeccion,
            Observaciones = createDto.Observaciones,
            Estado = InspeccionEstados.EnCurso
        };

        _context.Inspecciones.Add(inspeccion);
        await _context.SaveChangesAsync();

        await _context.Entry(inspeccion).Reference(i => i.Equipo).LoadAsync();
        await _context.Entry(inspeccion).Reference(i => i.Usuario).LoadAsync();
        await _context.Entry(inspeccion).Reference(i => i.Checklist).LoadAsync();

        return CreatedAtAction(nameof(GetInspeccion), new { id = inspeccion.IdInspeccion },
            ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion), "Inspección registrada exitosamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> UpdateInspeccion(long id, UpdateInspeccionDto updateDto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Include(i => i.Evidencias)
            .Include(i => i.Hallazgos)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeModificar(inspeccion))
        {
            return Forbid();
        }

        // Regla 6 del SRS: una inspección cerrada es evidencia histórica y no admite cambios.
        if (EstaCerrada(inspeccion))
        {
            return Conflict(ApiResponse<InspeccionDto>.ErrorResponse(MensajeInspeccionCerrada));
        }

        if (updateDto.Resultado != null || updateDto.FirmaDigital != null)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse(
                "El resultado y la firma solo se establecen mediante el cierre firmado de la inspección"));
        }

        if (updateDto.Observaciones != null)
            inspeccion.Observaciones = updateDto.Observaciones;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion), "Inspección actualizada exitosamente"));
    }

    // Punto 6, 3, 4, 5: Guardar respuestas usando el servicio y devolviendo 409 Conflict si está cerrada
    [HttpPut("{id}/respuestas")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<InspeccionEjecucionDto>>> GuardarRespuestas(
        long id,
        [FromBody] GuardarRespuestasDto dto)
    {
        try
        {
            var usuarioId = _currentUser.IdUsuario;
            var esAdmin = _currentUser.IsInRole("Administrador");

            await _inspeccionService.GuardarRespuestasAsync(id, dto, usuarioId, esAdmin);

            var ejecucionActualizada = await CargarEjecucionAsync(id);
            return Ok(ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
                MapToEjecucionDto(ejecucionActualizada!),
                "Respuestas guardadas exitosamente"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InspeccionCerradaException ex) // Retorna 409 Conflict
        {
            return Conflict(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex) // Retorna 400 BadRequest para validaciones
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteInspeccion(long id)
    {
        var inspeccion = await _context.Inspecciones.FindAsync(id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeModificar(inspeccion))
        {
            return Forbid();
        }

        // Regla 6 del SRS: una inspección cerrada tampoco se borra.
        if (EstaCerrada(inspeccion))
        {
            return Conflict(ApiResponse<bool>.ErrorResponse(MensajeInspeccionCerrada));
        }

        _context.Inspecciones.Remove(inspeccion);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Inspección eliminada exitosamente"));
    }

    /// <summary>
    /// Firma y cierra la inspección: son la misma acción, no existe cerrada sin firma.
    /// Valida obligatorias (regla 3 del SRS), novedad→observación (regla 4), y la firma PNG;
    /// después calcula Resultado y FirmaHash y deja la inspección inmutable (regla 6).
    /// </summary>
    [HttpPost("{id}/firmar")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> FirmarInspeccion(
        long id, FirmarInspeccionDto firmarDto)
    {
        var inspeccion = await CargarCierreAsync(id, seguimiento: true);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionResultadoDto>.ErrorResponse("Inspección no encontrada"));
        }

        // La firma es personal: solo el inspector que ejecutó la inspección puede cerrarla,
        // aunque quien llame sea Administrador.
        if (inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        if (EstaCerrada(inspeccion))
        {
            return Conflict(ApiResponse<InspeccionResultadoDto>.ErrorResponse(MensajeInspeccionCerrada));
        }

        var faltantes = ValidarContenidoParaCierre(inspeccion);
        if (faltantes.Count > 0)
        {
            return BadRequest(ApiResponse<InspeccionResultadoDto>.ErrorResponse(
                "La inspección no puede cerrarse: faltan datos obligatorios", faltantes));
        }

        if (!FirmaInspeccion.TryValidarPng(firmarDto.FirmaPngBase64, out var firmaPng, out var errorFirma))
        {
            return BadRequest(ApiResponse<InspeccionResultadoDto>.ErrorResponse(errorFirma));
        }

        var fechaCierre = DateTime.UtcNow;
        inspeccion.Estado = InspeccionEstados.Cerrada;
        inspeccion.FechaCierre = fechaCierre;
        inspeccion.Resultado = TieneNovedades(inspeccion)
            ? InspeccionResultados.ConNovedad
            : InspeccionResultados.Conforme;

        if (!string.IsNullOrWhiteSpace(firmarDto.Observaciones))
        {
            inspeccion.Observaciones = firmarDto.Observaciones;
        }

        inspeccion.FirmaDigital = firmaPng;
        inspeccion.FirmaHash = FirmaInspeccion.CalcularHash(
            inspeccion, inspeccion.Respuestas, inspeccion.Evidencias, fechaCierre, firmaPng);

        // Un único SaveChanges: estado, resultado, firma y hash se guardan en la misma
        // transacción, de modo que no puede quedar una inspección cerrada a medias.
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<InspeccionResultadoDto>.SuccessResponse(
            MapToResultadoDto(inspeccion),
            "Inspección firmada y cerrada exitosamente"));
    }

    /// <summary>Vista de solo lectura de una inspección ya cerrada: resumen, respuestas, evidencias, firma y hash.</summary>
    [HttpGet("{id}/resultado")]
    public async Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> GetResultado(long id)
    {
        var inspeccion = await CargarCierreAsync(id, seguimiento: false);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionResultadoDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeLeer(inspeccion))
        {
            return Forbid();
        }

        if (!EstaCerrada(inspeccion))
        {
            return Conflict(ApiResponse<InspeccionResultadoDto>.ErrorResponse(
                "La inspección aún está en curso; consúltela en /api/inspecciones/{id}/ejecucion"));
        }

        return Ok(ApiResponse<InspeccionResultadoDto>.SuccessResponse(MapToResultadoDto(inspeccion)));
    }

    private const string MensajeInspeccionCerrada = InmutabilidadInspeccion.MensajeCerrada;

    private static bool EstaCerrada(Inspeccion inspeccion) =>
        inspeccion.Estado == InspeccionEstados.Cerrada;

    /// <summary>
    /// Comprueba las reglas 3 y 4 del SRS y devuelve todos los incumplimientos de una vez,
    /// para que la pantalla de firma pueda listarle al técnico qué le falta sin ir de uno en uno.
    /// </summary>
    private static List<string> ValidarContenidoParaCierre(Inspeccion inspeccion)
    {
        var respuestasPorPregunta = inspeccion.Respuestas
            .ToDictionary(respuesta => respuesta.IdPregunta);
        var faltantes = new List<string>();

        foreach (var pregunta in inspeccion.Checklist.Preguntas
            .OrderBy(pregunta => pregunta.Orden)
            .ThenBy(pregunta => pregunta.IdPregunta))
        {
            respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta);
            var sinResponder = respuesta == null || string.IsNullOrWhiteSpace(respuesta.Respuesta);

            if (pregunta.Obligatoria && sinResponder)
            {
                faltantes.Add($"La pregunta obligatoria «{pregunta.Pregunta}» no tiene respuesta");
                continue;
            }

            if (!sinResponder
                && EsNovedad(pregunta.TipoRespuesta, respuesta!.Respuesta)
                && string.IsNullOrWhiteSpace(respuesta.Observacion))
            {
                faltantes.Add($"La novedad de «{pregunta.Pregunta}» requiere una observación");
            }
        }

        return faltantes;
    }

    private static bool TieneNovedades(Inspeccion inspeccion)
    {
        var tipoPorPregunta = inspeccion.Checklist.Preguntas
            .ToDictionary(pregunta => pregunta.IdPregunta, pregunta => pregunta.TipoRespuesta);

        return inspeccion.Respuestas.Any(respuesta =>
            tipoPorPregunta.TryGetValue(respuesta.IdPregunta, out var tipo)
            && EsNovedad(tipo, respuesta.Respuesta));
    }

    /// <summary>Una respuesta Sí/No marcada como "No" es una novedad (regla 4 del SRS).</summary>
    private static bool EsNovedad(string? tipoRespuesta, string? respuesta) =>
        tipoRespuesta == TiposRespuesta.SiNo
        && string.Equals(respuesta, "No", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Carga todo lo que interviene en el cierre. Con <paramref name="seguimiento"/> en true
    /// para firmar (hay que escribir) y en false para la consulta del resultado.
    /// </summary>
    private Task<Inspeccion?> CargarCierreAsync(long id, bool seguimiento)
    {
        var query = _context.Inspecciones.AsQueryable();
        if (!seguimiento)
        {
            query = query.AsNoTracking();
        }

        return query
            .Include(i => i.Equipo)
                .ThenInclude(e => e.Ubicacion)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
                .ThenInclude(c => c.Preguntas)
            .Include(i => i.Respuestas)
            .Include(i => i.Evidencias)
                .ThenInclude(e => e.UsuarioCargaDetalle)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);
    }

    private static InspeccionResultadoDto MapToResultadoDto(Inspeccion inspeccion)
    {
        var respuestasPorPregunta = inspeccion.Respuestas
            .ToDictionary(respuesta => respuesta.IdPregunta);

        var preguntas = inspeccion.Checklist.Preguntas
            .OrderBy(pregunta => pregunta.Orden)
            .ThenBy(pregunta => pregunta.IdPregunta)
            .Select(pregunta =>
            {
                respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta);
                return new PreguntaEjecucionDto
                {
                    IdPregunta = pregunta.IdPregunta,
                    Pregunta = pregunta.Pregunta,
                    TipoRespuesta = pregunta.TipoRespuesta,
                    Obligatoria = pregunta.Obligatoria,
                    Orden = pregunta.Orden,
                    IdRespuesta = respuesta?.IdRespuesta,
                    Respuesta = respuesta?.Respuesta,
                    Observacion = respuesta?.Observacion
                };
            })
            .ToList();

        return new InspeccionResultadoDto
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Estado = inspeccion.Estado,
            Resultado = inspeccion.Resultado ?? string.Empty,
            FechaInspeccion = inspeccion.FechaInspeccion,
            FechaCierre = inspeccion.FechaCierre,
            Observaciones = inspeccion.Observaciones,
            IdEquipo = inspeccion.IdEquipo,
            CodigoInterno = inspeccion.Equipo.CodigoInterno,
            NombreEquipo = inspeccion.Equipo.NombreEquipo,
            UbicacionNombre = inspeccion.Equipo.Ubicacion == null
                ? null
                : $"{inspeccion.Equipo.Ubicacion.Planta} - {inspeccion.Equipo.Ubicacion.Area}",
            NombreChecklist = inspeccion.Checklist.Nombre,
            VersionChecklist = inspeccion.Checklist.Version,
            NombreUsuario = inspeccion.Usuario.Nombre,
            Preguntas = preguntas,
            TotalNovedades = preguntas.Count(pregunta => pregunta.EsNovedad),
            Evidencias = inspeccion.Evidencias
                .OrderBy(evidencia => evidencia.FechaCarga)
                .Select(evidencia => new EvidenciaDto
                {
                    IdEvidencia = evidencia.IdEvidencia,
                    IdInspeccion = evidencia.IdInspeccion,
                    NombreEquipo = inspeccion.Equipo.NombreEquipo,
                    Archivo = evidencia.Archivo,
                    NombreOriginal = evidencia.NombreOriginal,
                    TipoContenido = evidencia.TipoContenido,
                    TamanoBytes = evidencia.TamanoBytes,
                    FechaCarga = evidencia.FechaCarga,
                    IdUsuarioCarga = evidencia.IdUsuarioCarga,
                    UsuarioCarga = evidencia.UsuarioCargaDetalle.Nombre
                })
                .ToList(),
            TieneFirma = !string.IsNullOrEmpty(inspeccion.FirmaDigital),
            FirmaPngBase64 = inspeccion.FirmaDigital,
            FirmaHash = inspeccion.FirmaHash
        };
    }

    private Task<Inspeccion?> CargarEjecucionAsync(long id) => _context.Inspecciones
        .AsNoTracking()
        .Include(i => i.Equipo)
            .ThenInclude(e => e.Ubicacion)
        .Include(i => i.Usuario)
        .Include(i => i.Checklist)
            .ThenInclude(c => c.Preguntas)
        .Include(i => i.Respuestas)
        .Include(i => i.Evidencias)
            .ThenInclude(e => e.UsuarioCargaDetalle)
        .FirstOrDefaultAsync(i => i.IdInspeccion == id);

    private bool EsTecnicoSinPrivilegiosDeLecturaGlobal() =>
        _currentUser.IsInRole("Técnico")
        && !_currentUser.IsInRole("Administrador")
        && !_currentUser.IsInRole("Auditor");

    private bool PuedeLeer(Inspeccion inspeccion) =>
        _currentUser.IsInRole("Administrador")
        || _currentUser.IsInRole("Auditor")
        || (_currentUser.IsInRole("Técnico") && inspeccion.IdUsuario == _currentUser.IdUsuario);

    private bool PuedeModificar(Inspeccion inspeccion) =>
        _currentUser.IsInRole("Administrador")
        || (_currentUser.IsInRole("Técnico") && inspeccion.IdUsuario == _currentUser.IdUsuario);

    private static InspeccionEjecucionDto MapToEjecucionDto(Inspeccion inspeccion)
    {
        var respuestasPorPregunta = inspeccion.Respuestas
            .ToDictionary(respuesta => respuesta.IdPregunta);

        var preguntasChecklist = inspeccion.Checklist.Preguntas.ToList();
        var obligatorias = preguntasChecklist.Where(pregunta => pregunta.Obligatoria).ToList();
        var obligatoriasRespondidas = obligatorias.Count(pregunta =>
            respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta)
            && !string.IsNullOrWhiteSpace(respuesta.Respuesta));
        var novedades = preguntasChecklist.Count(pregunta =>
            pregunta.TipoRespuesta == TiposRespuesta.SiNo
            && respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta)
            && string.Equals(respuesta.Respuesta, "No", StringComparison.OrdinalIgnoreCase));

        return new InspeccionEjecucionDto
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdEquipo = inspeccion.IdEquipo,
            CodigoInternoEquipo = inspeccion.Equipo.CodigoInterno,
            NombreEquipo = inspeccion.Equipo.NombreEquipo,
            UbicacionNombre = inspeccion.Equipo.Ubicacion == null
                ? null
                : $"{inspeccion.Equipo.Ubicacion.Planta} - {inspeccion.Equipo.Ubicacion.Area}",
            Criticidad = inspeccion.Equipo.Criticidad,
            TotalObligatorias = obligatorias.Count,
            ObligatoriasRespondidas = obligatoriasRespondidas,
            TotalNovedades = novedades,
            IdChecklist = inspeccion.IdChecklist,
            NombreChecklist = inspeccion.Checklist.Nombre,
            VersionChecklist = inspeccion.Checklist.Version,
            IdUsuario = inspeccion.IdUsuario,
            NombreUsuario = inspeccion.Usuario.Nombre,
            FechaInspeccion = inspeccion.FechaInspeccion,
            Estado = inspeccion.Estado,
            Preguntas = inspeccion.Checklist.Preguntas
                .OrderBy(pregunta => pregunta.Orden)
                .ThenBy(pregunta => pregunta.IdPregunta)
                .Select(pregunta =>
                {
                    respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta);
                    return new PreguntaEjecucionDto
                    {
                        IdPregunta = pregunta.IdPregunta,
                        Pregunta = pregunta.Pregunta,
                        TipoRespuesta = pregunta.TipoRespuesta,
                        Obligatoria = pregunta.Obligatoria,
                        Orden = pregunta.Orden,
                        IdRespuesta = respuesta?.IdRespuesta,
                        Respuesta = respuesta?.Respuesta,
                        Observacion = respuesta?.Observacion
                    };
                })
                .ToList(),
            Evidencias = inspeccion.Evidencias
                .OrderBy(evidencia => evidencia.FechaCarga)
                .Select(evidencia => new EvidenciaDto
                {
                    IdEvidencia = evidencia.IdEvidencia,
                    IdInspeccion = evidencia.IdInspeccion,
                    NombreEquipo = inspeccion.Equipo.NombreEquipo,
                    Archivo = evidencia.Archivo,
                    NombreOriginal = evidencia.NombreOriginal,
                    TipoContenido = evidencia.TipoContenido,
                    TamanoBytes = evidencia.TamanoBytes,
                    FechaCarga = evidencia.FechaCarga,
                    IdUsuarioCarga = evidencia.IdUsuarioCarga,
                    UsuarioCarga = evidencia.UsuarioCargaDetalle.Nombre
                })
                .ToList()
        };
    }

    private static InspeccionDto MapToDto(Inspeccion i)
    {
        return new InspeccionDto
        {
            IdInspeccion = i.IdInspeccion,
            IdEquipo = i.IdEquipo,
            NombreEquipo = i.Equipo?.NombreEquipo,
            IdUsuario = i.IdUsuario,
            NombreUsuario = i.Usuario?.Nombre,
            IdChecklist = i.IdChecklist,
            NombreChecklist = i.Checklist?.Nombre,
            FechaInspeccion = i.FechaInspeccion,
            Estado = i.Estado,
            FechaCierre = i.FechaCierre,
            Resultado = i.Resultado,
            Observaciones = i.Observaciones,
            TieneFirma = !string.IsNullOrEmpty(i.FirmaDigital),
            TotalEvidencias = i.Evidencias?.Count ?? 0,
            TotalHallazgos = i.Hallazgos?.Count ?? 0
        };
    }
}
