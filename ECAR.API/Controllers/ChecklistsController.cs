using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
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
public class ChecklistsController : ControllerBase
{
    private readonly ECARDbContext _context;

    public ChecklistsController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<ChecklistDto>>>> GetChecklists([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        var query = _context.Checklists
            .Include(c => c.Preguntas)
            .AsQueryable();

        // Aplicar el filtro de búsqueda
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => c.Nombre.Contains(search) || c.Version.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var checklists = await query
            .OrderByDescending(c => c.FechaCreacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ChecklistDto
            {
                IdChecklist = c.IdChecklist,
                Nombre = c.Nombre,
                Version = c.Version,
                Activo = c.Activo,
                FechaCreacion = c.FechaCreacion,
                Preguntas = c.Preguntas.Select(p => new PreguntaChecklistDto
                {
                    IdPregunta = p.IdPregunta,
                    IdChecklist = p.IdChecklist,
                    Pregunta = p.Pregunta,
                    TipoRespuesta = p.TipoRespuesta,
                    Obligatoria = p.Obligatoria,
                    Orden = p.Orden
                }).OrderBy(p => p.Orden).ToList()
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<ChecklistDto>
        {
            Data = checklists,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<ChecklistDto>>.SuccessResponse(pagedResult));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ChecklistDto>>> GetChecklist(long id)
    {
        var checklist = await _context.Checklists
            .Include(c => c.Preguntas)
            .FirstOrDefaultAsync(c => c.IdChecklist == id);

        if (checklist == null)
        {
            return NotFound(ApiResponse<ChecklistDto>.ErrorResponse("Checklist no encontrado"));
        }

        var checklistDto = MapToDto(checklist);

        return Ok(ApiResponse<ChecklistDto>.SuccessResponse(checklistDto));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<ChecklistDto>>> CreateChecklist(CreateChecklistDto createDto)
    {
        var tipoInvalido = GetInvalidTipoRespuesta(createDto.Preguntas);
        if (tipoInvalido != null)
        {
            return BadRequest(ApiResponse<ChecklistDto>.ErrorResponse(tipoInvalido));
        }

        var existente = await _context.Checklists
            .FirstOrDefaultAsync(c => c.Nombre == createDto.Nombre && c.Version == createDto.Version);

        if (existente != null)
        {
            return BadRequest(ApiResponse<ChecklistDto>.ErrorResponse("Ya existe un checklist con ese nombre y versión"));
        }

        var checklist = new Checklist
        {
            Nombre = createDto.Nombre,
            Version = createDto.Version,
            Activo = true
        };

        _context.Checklists.Add(checklist);
        await _context.SaveChangesAsync();

        if (createDto.Preguntas != null && createDto.Preguntas.Any())
        {
            AgregarPreguntas(checklist.IdChecklist, createDto.Preguntas);
            await _context.SaveChangesAsync();
        }

        await _context.Entry(checklist).Collection(c => c.Preguntas).LoadAsync();

        var checklistDto = MapToDto(checklist);

        return CreatedAtAction(nameof(GetChecklist), new { id = checklist.IdChecklist },
            ApiResponse<ChecklistDto>.SuccessResponse(checklistDto, "Checklist creado exitosamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<ChecklistDto>>> UpdateChecklist(long id, UpdateChecklistDto updateDto)
    {
        var checklist = await _context.Checklists
            .Include(c => c.Preguntas)
            .FirstOrDefaultAsync(c => c.IdChecklist == id);

        if (checklist == null)
        {
            return NotFound(ApiResponse<ChecklistDto>.ErrorResponse("Checklist no encontrado"));
        }

        // Regla SRS #6: un checklist ya respondido en inspecciones es evidencia histórica.
        // Sus preguntas no se reemplazan; se crea una versión nueva.
        if (updateDto.Preguntas != null && await TieneRespuestasAsync(id))
        {
            return Conflict(ApiResponse<ChecklistDto>.ErrorResponse(
                "Este checklist ya se usó en inspecciones. Para modificar sus preguntas cree una versión nueva."));
        }

        var tipoInvalido = GetInvalidTipoRespuesta(updateDto.Preguntas);
        if (tipoInvalido != null)
        {
            return BadRequest(ApiResponse<ChecklistDto>.ErrorResponse(tipoInvalido));
        }

        if (!string.IsNullOrEmpty(updateDto.Nombre))
            checklist.Nombre = updateDto.Nombre;

        if (!string.IsNullOrEmpty(updateDto.Version))
            checklist.Version = updateDto.Version;

        if (updateDto.Activo.HasValue)
            checklist.Activo = updateDto.Activo.Value;

        await _context.SaveChangesAsync();

        // Reemplazar preguntas si se proporcionaron
        if (updateDto.Preguntas != null)
        {
            var existentes = _context.PreguntasChecklist.Where(p => p.IdChecklist == id);
            _context.PreguntasChecklist.RemoveRange(existentes);

            AgregarPreguntas(id, updateDto.Preguntas);
            await _context.SaveChangesAsync();
        }

        await _context.Entry(checklist).Collection(c => c.Preguntas).LoadAsync();

        var checklistDto = MapToDto(checklist);

        return Ok(ApiResponse<ChecklistDto>.SuccessResponse(checklistDto, "Checklist actualizado exitosamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteChecklist(long id)
    {
        var checklist = await _context.Checklists.FindAsync(id);

        if (checklist == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Checklist no encontrado"));
        }

        // Borrado lógico: se marca como inactivo en lugar de eliminarlo
        checklist.Activo = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Checklist desactivado exitosamente"));
    }

    // Versionamiento: todas las versiones de un checklist comparten Nombre; solo una está activa
    [HttpGet("{id}/versiones")]
    public async Task<ActionResult<ApiResponse<List<ChecklistVersionDto>>>> GetVersiones(long id)
    {
        var checklist = await _context.Checklists.FindAsync(id);
        if (checklist == null)
        {
            return NotFound(ApiResponse<List<ChecklistVersionDto>>.ErrorResponse("Checklist no encontrado"));
        }

        var versiones = await _context.Checklists
            .Where(c => c.Nombre == checklist.Nombre)
            .OrderByDescending(c => c.Activo)
            .ThenByDescending(c => c.FechaCreacion)
            .Select(c => new ChecklistVersionDto
            {
                IdChecklist = c.IdChecklist,
                Nombre = c.Nombre,
                Version = c.Version,
                Activo = c.Activo,
                FechaCreacion = c.FechaCreacion,
                TotalPreguntas = c.Preguntas.Count,
                TieneRespuestas = c.Preguntas.Any(p => p.Respuestas.Any())
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ChecklistVersionDto>>.SuccessResponse(versiones));
    }

    // Crea una versión nueva copiando las preguntas de la actual y deja la anterior como histórica
    [HttpPost("{id}/nueva-version")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<ChecklistDto>>> CreateVersion(long id, CreateChecklistVersionDto createDto)
    {
        var origen = await _context.Checklists
            .Include(c => c.Preguntas)
            .FirstOrDefaultAsync(c => c.IdChecklist == id);

        if (origen == null)
        {
            return NotFound(ApiResponse<ChecklistDto>.ErrorResponse("Checklist no encontrado"));
        }

        var version = createDto.Version.Trim();
        if (string.Equals(origen.Version, version, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<ChecklistDto>.ErrorResponse("La nueva versión debe ser diferente de la actual"));
        }

        var duplicada = await _context.Checklists
            .AnyAsync(c => c.Nombre == origen.Nombre && c.Version == version);
        if (duplicada)
        {
            return Conflict(ApiResponse<ChecklistDto>.ErrorResponse($"Ya existe la versión '{version}' del checklist '{origen.Nombre}'"));
        }

        // Solo puede haber una versión activa por nombre: las demás pasan a históricas
        var activas = await _context.Checklists
            .Where(c => c.Nombre == origen.Nombre && c.Activo)
            .ToListAsync();
        foreach (var activa in activas)
        {
            activa.Activo = false;
        }

        var nueva = new Checklist
        {
            Nombre = origen.Nombre,
            Version = version,
            Activo = true
        };

        _context.Checklists.Add(nueva);
        await _context.SaveChangesAsync();

        foreach (var pregunta in origen.Preguntas.OrderBy(p => p.Orden))
        {
            _context.PreguntasChecklist.Add(new PreguntaChecklist
            {
                IdChecklist = nueva.IdChecklist,
                Pregunta = pregunta.Pregunta,
                TipoRespuesta = pregunta.TipoRespuesta,
                Obligatoria = pregunta.Obligatoria,
                Orden = pregunta.Orden
            });
        }
        await _context.SaveChangesAsync();

        await _context.Entry(nueva).Collection(c => c.Preguntas).LoadAsync();

        return CreatedAtAction(nameof(GetChecklist), new { id = nueva.IdChecklist },
            ApiResponse<ChecklistDto>.SuccessResponse(MapToDto(nueva), $"Versión '{version}' creada exitosamente"));
    }

    private Task<bool> TieneRespuestasAsync(long idChecklist) =>
        _context.RespuestasInspeccion.AnyAsync(r => r.Pregunta.IdChecklist == idChecklist);

    // El orden lo fija el servidor según la posición en que llegan las preguntas
    private void AgregarPreguntas(long idChecklist, IEnumerable<CreatePreguntaChecklistDto> preguntas)
    {
        var orden = 1;
        foreach (var pregunta in preguntas)
        {
            _context.PreguntasChecklist.Add(new PreguntaChecklist
            {
                IdChecklist = idChecklist,
                Pregunta = pregunta.Pregunta,
                TipoRespuesta = pregunta.TipoRespuesta,
                Obligatoria = pregunta.Obligatoria,
                Orden = orden++
            });
        }
    }

    /// <summary>Devuelve un mensaje de error cuando una pregunta usa un tipo de respuesta fuera del catálogo.</summary>
    private static string? GetInvalidTipoRespuesta(IEnumerable<CreatePreguntaChecklistDto>? preguntas)
    {
        var invalida = preguntas?.FirstOrDefault(p => !TiposRespuesta.EsValido(p.TipoRespuesta));

        return invalida == null
            ? null
            : $"El tipo de respuesta '{invalida.TipoRespuesta}' no es válido. Valores permitidos: {TiposRespuesta.ValoresPermitidos}";
    }

    private static ChecklistDto MapToDto(Checklist checklist)
    {
        return new ChecklistDto
        {
            IdChecklist = checklist.IdChecklist,
            Nombre = checklist.Nombre,
            Version = checklist.Version,
            Activo = checklist.Activo,
            FechaCreacion = checklist.FechaCreacion,
            Preguntas = checklist.Preguntas.OrderBy(p => p.Orden).Select(p => new PreguntaChecklistDto
            {
                IdPregunta = p.IdPregunta,
                IdChecklist = p.IdChecklist,
                Pregunta = p.Pregunta,
                TipoRespuesta = p.TipoRespuesta,
                Obligatoria = p.Obligatoria,
                Orden = p.Orden
            }).ToList()
        };
    }
}
