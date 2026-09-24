using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.API.Services;
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

    public InspeccionesController(ECARDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<InspeccionDto>>>> GetInspecciones([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
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

        // Regla de negocio: si existe novedad, la observación es obligatoria
        if (!string.IsNullOrWhiteSpace(createDto.Resultado)
            && createDto.Resultado.Contains("novedad", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(createDto.Observaciones))
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("Si la inspección tiene novedad, las observaciones son obligatorias"));
        }

        var inspeccion = new Inspeccion
        {
            IdEquipo = createDto.IdEquipo,
            IdUsuario = usuario.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = createDto.FechaInspeccion,
            Resultado = createDto.Resultado,
            Observaciones = createDto.Observaciones,
            FirmaDigital = createDto.FirmaDigital
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

        if (updateDto.Resultado != null)
            inspeccion.Resultado = updateDto.Resultado;

        if (updateDto.Observaciones != null)
            inspeccion.Observaciones = updateDto.Observaciones;

        if (updateDto.FirmaDigital != null)
            inspeccion.FirmaDigital = updateDto.FirmaDigital;

        var resultado = inspeccion.Resultado;
        if (!string.IsNullOrWhiteSpace(resultado)
            && resultado.Contains("novedad", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(inspeccion.Observaciones))
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("Si la inspección tiene novedad, las observaciones son obligatorias"));
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion), "Inspección actualizada exitosamente"));
    }

    /// <summary>
    /// Guarda o actualiza (Upsert) por lote las respuestas de una inspección en curso.
    /// Consumido por Blazor (PasoPreguntas.razor)
    /// </summary>
    [HttpPut("{id}/respuestas")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<InspeccionEjecucionDto>>> GuardarRespuestas(
        long id,
        [FromBody] GuardarRespuestasDto dto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Respuestas)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionEjecucionDto>.ErrorResponse("Inspección no encontrada"));
        }

        // 1. Validación de permisos: Solo el técnico asignado o Admin
        if (!PuedeModificar(inspeccion))
        {
            return Forbid();
        }

        // 2. Validación 21 CFR Part 11: Inmutabilidad si la inspección no está en curso
        if (inspeccion.Estado != InspeccionEstados.EnCurso)
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "Solo se pueden guardar respuestas en inspecciones que estén 'En curso'"));
        }

        // 3. Lógica de UPSERT por lote (Actualizar si existe, insertar si no)
        foreach (var item in dto.Respuestas)
        {
            var respuestaExistente = inspeccion.Respuestas
                .FirstOrDefault(r => r.IdPregunta == item.IdPregunta);

            if (respuestaExistente != null)
            {
                // UPDATE
                respuestaExistente.Respuesta = item.Respuesta;
                respuestaExistente.Observacion = item.Observacion;
            }
            else
            {
                // INSERT
                inspeccion.Respuestas.Add(new RespuestaInspeccion
                {
                    IdInspeccion = id,
                    IdPregunta = item.IdPregunta,
                    Respuesta = item.Respuesta,
                    Observacion = item.Observacion
                });
            }
        }

        await _context.SaveChangesAsync();

        // 4. Retorna el DTO de ejecución actualizado para que Blazor recalcule contadores en tiempo real
        var ejecucionActualizada = await CargarEjecucionAsync(id);
        return Ok(ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
            MapToEjecucionDto(ejecucionActualizada!),
            "Respuestas guardadas exitosamente"));
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

        _context.Inspecciones.Remove(inspeccion);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Inspección eliminada exitosamente"));
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

        // Contadores que la pantalla de ejecución usa para el stepper (reglas 3 y 4 del SRS).
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