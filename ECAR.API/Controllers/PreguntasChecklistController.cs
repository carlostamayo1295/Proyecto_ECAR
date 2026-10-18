using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;

namespace ECAR.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador,Técnico,Auditor")]
    public class PreguntasChecklistController : ControllerBase
    {
        // Si 'ApplicationDbContext' sale remarcado, cámbialo por el nombre de tu DbContext (ej: AppDbContext o EcarDbContext)
        private readonly ECARDbContext _context;

        public PreguntasChecklistController(ECARDbContext context)
        {
            _context = context;
        }

        // 0. GET: api/PreguntasChecklist?page=1&pageSize=10&search=texto&idChecklist=5
        // Listado paginado de todas las preguntas, con búsqueda por texto y filtro opcional por checklist
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResultDto<PreguntaChecklistDto>>>> GetPreguntas(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] long? idChecklist = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.PreguntasChecklist.AsQueryable();

            if (idChecklist.HasValue)
            {
                query = query.Where(p => p.IdChecklist == idChecklist.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Pregunta.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var preguntas = await query
                .OrderBy(p => p.IdChecklist)
                .ThenBy(p => p.Orden)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PreguntaChecklistDto
                {
                    IdPregunta = p.IdPregunta,
                    IdChecklist = p.IdChecklist,
                    Pregunta = p.Pregunta,
                    TipoRespuesta = p.TipoRespuesta,
                    Obligatoria = p.Obligatoria,
                    Orden = p.Orden
                })
                .ToListAsync();

            var pagedResult = new PagedResultDto<PreguntaChecklistDto>
            {
                Data = preguntas,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(ApiResponse<PagedResultDto<PreguntaChecklistDto>>.SuccessResponse(pagedResult));
        }
        // 1. GET: api/PreguntasChecklist/checklist/{idChecklist}
        // Listar preguntas por IdChecklist ordenadas por posición (Requerimiento Jira #1)
        [HttpGet("checklist/{idChecklist}")]
        public async Task<ActionResult<ApiResponse<List<PreguntaChecklistDto>>>> GetPreguntasPorChecklist(long idChecklist)
        {
            var checklistExiste = await _context.Checklists.AnyAsync(c => c.IdChecklist == idChecklist);
            if (!checklistExiste)
            {
                return NotFound(ApiResponse<List<PreguntaChecklistDto>>.ErrorResponse("El checklist especificado no existe."));
            }

            var preguntas = await _context.PreguntasChecklist
                .AsNoTracking()
                .Where(p => p.IdChecklist == idChecklist)
                .OrderBy(p => p.Orden)
                .Select(p => new PreguntaChecklistDto
                {
                    IdPregunta = p.IdPregunta,
                    IdChecklist = p.IdChecklist,
                    Pregunta = p.Pregunta,
                    TipoRespuesta = p.TipoRespuesta,
                    Obligatoria = p.Obligatoria,
                    Orden = p.Orden
                })
                .ToListAsync();

            return Ok(ApiResponse<List<PreguntaChecklistDto>>.SuccessResponse(preguntas));
        }

        // 2. POST: api/PreguntasChecklist
        // Crear pregunta con validación de existencia, orden único y roles (Requerimientos Jira #2, #3, #4, #6)
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<ApiResponse<PreguntaChecklistDto>>> CrearPregunta([FromBody] CreatePreguntaChecklistDto dto)
        {
            var checklistExiste = await _context.Checklists.AnyAsync(c => c.IdChecklist == dto.IdChecklist);
            if (!checklistExiste)
            {
                return NotFound(ApiResponse<PreguntaChecklistDto>.ErrorResponse("El checklist al que intentas asociar la pregunta no existe."));
            }

            if (ValidarContenido(dto.Pregunta, dto.TipoRespuesta) is { } error)
            {
                return BadRequest(ApiResponse<PreguntaChecklistDto>.ErrorResponse(error));
            }

            if (await ChecklistUsadoAsync(dto.IdChecklist))
            {
                return Conflict(ApiResponse<PreguntaChecklistDto>.ErrorResponse(MensajeChecklistUsado));
            }

            // El orden lo asigna el servidor: siempre al final del checklist
            var ultimoOrden = await _context.PreguntasChecklist
                .Where(p => p.IdChecklist == dto.IdChecklist)
                .Select(p => (int?)p.Orden)
                .MaxAsync();

            var nuevaPregunta = new PreguntaChecklist
            {
                IdChecklist = dto.IdChecklist,
                Pregunta = dto.Pregunta.Trim(),
                TipoRespuesta = dto.TipoRespuesta.Trim(),
                Obligatoria = dto.Obligatoria,
                Orden = (ultimoOrden ?? 0) + 1
            };

            _context.PreguntasChecklist.Add(nuevaPregunta);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<PreguntaChecklistDto>.SuccessResponse(MapToDto(nuevaPregunta), "Pregunta creada exitosamente"));
        }

        // 3. PUT: api/PreguntasChecklist/{id}
        // Editar pregunta bloqueando modificación si ya fue usada en inspecciones (Requerimientos Jira #2, #4, #5, #6)
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<ApiResponse<PreguntaChecklistDto>>> EditarPregunta(long id, [FromBody] UpdatePreguntaChecklistDto dto)
        {
            var preguntaExistente = await _context.PreguntasChecklist.FindAsync(id);
            if (preguntaExistente == null)
            {
                return NotFound(ApiResponse<PreguntaChecklistDto>.ErrorResponse("La pregunta especificada no existe."));
            }

            var yaUsadaEnInspeccion = await _context.RespuestasInspeccion
                .AnyAsync(r => r.IdPregunta == id);

            if (yaUsadaEnInspeccion)
            {
                return Conflict(ApiResponse<PreguntaChecklistDto>.ErrorResponse("No se puede editar la pregunta porque ya fue respondida en una inspección histórica."));
            }

            if (ValidarContenido(dto.Pregunta, dto.TipoRespuesta) is { } error)
            {
                return BadRequest(ApiResponse<PreguntaChecklistDto>.ErrorResponse(error));
            }

            // El orden y el checklist no se editan desde aquí: se conservan los que ya tiene la pregunta
            preguntaExistente.Pregunta = dto.Pregunta.Trim();
            preguntaExistente.TipoRespuesta = dto.TipoRespuesta.Trim();
            preguntaExistente.Obligatoria = dto.Obligatoria;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<PreguntaChecklistDto>.SuccessResponse(MapToDto(preguntaExistente), "Pregunta actualizada exitosamente"));
        }

        // 4. DELETE: api/PreguntasChecklist/{id}
        // Eliminar pregunta bloqueando evidencia histórica (Requerimientos Jira #5, #6)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<ActionResult<ApiResponse<bool>>> EliminarPregunta(long id)
        {
            var pregunta = await _context.PreguntasChecklist.FindAsync(id);
            if (pregunta == null)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse("La pregunta especificada no existe."));
            }

            var yaUsadaEnInspeccion = await _context.RespuestasInspeccion
                .AnyAsync(r => r.IdPregunta == id);

            if (yaUsadaEnInspeccion)
            {
                return Conflict(ApiResponse<bool>.ErrorResponse("No se puede eliminar la pregunta porque forma parte de la evidencia histórica de una inspección."));
            }

            _context.PreguntasChecklist.Remove(pregunta);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Pregunta eliminada exitosamente"));
        }

        private const string MensajeChecklistUsado =
            "Este checklist ya se usó en inspecciones. Para cambiar sus preguntas cree una versión nueva.";

        private static string? ValidarContenido(string? pregunta, string? tipoRespuesta)
        {
            if (string.IsNullOrWhiteSpace(pregunta) || string.IsNullOrWhiteSpace(tipoRespuesta))
            {
                return "El texto de la pregunta y el tipo de respuesta son obligatorios.";
            }

            return TiposRespuesta.EsValido(tipoRespuesta.Trim())
                ? null
                : $"El tipo de respuesta '{tipoRespuesta}' no es válido. Valores permitidos: {TiposRespuesta.ValoresPermitidos}";
        }

        private Task<bool> ChecklistUsadoAsync(long idChecklist) =>
            _context.RespuestasInspeccion.AnyAsync(r => r.Pregunta.IdChecklist == idChecklist);

        private static PreguntaChecklistDto MapToDto(PreguntaChecklist p) => new()
        {
            IdPregunta = p.IdPregunta,
            IdChecklist = p.IdChecklist,
            Pregunta = p.Pregunta,
            TipoRespuesta = p.TipoRespuesta,
            Obligatoria = p.Obligatoria,
            Orden = p.Orden
        };
    }
}
