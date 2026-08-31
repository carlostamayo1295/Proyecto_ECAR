using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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

        // Apply search filter
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
                    Obligatoria = p.Obligatoria
                }).ToList()
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
    public async Task<ActionResult<ApiResponse<ChecklistDto>>> CreateChecklist(CreateChecklistDto createDto)
    {
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
            foreach (var pregunta in createDto.Preguntas)
            {
                _context.PreguntasChecklist.Add(new PreguntaChecklist
                {
                    IdChecklist = checklist.IdChecklist,
                    Pregunta = pregunta.Pregunta,
                    TipoRespuesta = pregunta.TipoRespuesta,
                    Obligatoria = pregunta.Obligatoria
                });
            }
            await _context.SaveChangesAsync();
        }

        await _context.Entry(checklist).Collection(c => c.Preguntas).LoadAsync();

        var checklistDto = MapToDto(checklist);

        return CreatedAtAction(nameof(GetChecklist), new { id = checklist.IdChecklist },
            ApiResponse<ChecklistDto>.SuccessResponse(checklistDto, "Checklist creado exitosamente"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ChecklistDto>>> UpdateChecklist(long id, UpdateChecklistDto updateDto)
    {
        var checklist = await _context.Checklists
            .Include(c => c.Preguntas)
            .FirstOrDefaultAsync(c => c.IdChecklist == id);

        if (checklist == null)
        {
            return NotFound(ApiResponse<ChecklistDto>.ErrorResponse("Checklist no encontrado"));
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

            foreach (var pregunta in updateDto.Preguntas)
            {
                _context.PreguntasChecklist.Add(new PreguntaChecklist
                {
                    IdChecklist = id,
                    Pregunta = pregunta.Pregunta,
                    TipoRespuesta = pregunta.TipoRespuesta,
                    Obligatoria = pregunta.Obligatoria
                });
            }
            await _context.SaveChangesAsync();
        }

        await _context.Entry(checklist).Collection(c => c.Preguntas).LoadAsync();

        var checklistDto = MapToDto(checklist);

        return Ok(ApiResponse<ChecklistDto>.SuccessResponse(checklistDto, "Checklist actualizado exitosamente"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteChecklist(long id)
    {
        var checklist = await _context.Checklists.FindAsync(id);

        if (checklist == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Checklist no encontrado"));
        }

        // Soft delete - mark as inactive instead of removing
        checklist.Activo = false;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Checklist desactivado exitosamente"));
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
            Preguntas = checklist.Preguntas.Select(p => new PreguntaChecklistDto
            {
                IdPregunta = p.IdPregunta,
                IdChecklist = p.IdChecklist,
                Pregunta = p.Pregunta,
                TipoRespuesta = p.TipoRespuesta,
                Obligatoria = p.Obligatoria
            }).ToList()
        };
    }
}
