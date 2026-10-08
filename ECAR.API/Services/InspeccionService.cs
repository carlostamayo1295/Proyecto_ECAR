using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.API.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services;

public class InspeccionService : IInspeccionService
{
    private readonly ECARDbContext _context;

    public InspeccionService(ECARDbContext context)
    {
        _context = context;
    }

    public async Task GuardarRespuestasAsync(long inspeccionId, GuardarRespuestasDto dto, long usuarioId, bool esAdmin)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Checklist)
                .ThenInclude(c => c.Preguntas)
            .Include(i => i.Respuestas)
            .FirstOrDefaultAsync(i => i.IdInspeccion == inspeccionId);

        if (inspeccion == null)
        {
            throw new KeyNotFoundException($"No se encontró la inspección con ID {inspeccionId}");
        }

        // Solo técnico asignado o Admin
        if (!esAdmin && inspeccion.IdUsuario != usuarioId)
        {
            throw new UnauthorizedAccessException("No tienes permiso para modificar esta inspección.");
        }

        // 409 Conflict si la inspección no está 'En curso'
        if (inspeccion.Estado != InspeccionEstados.EnCurso)
        {
            throw new InspeccionCerradaException("La inspección está cerrada y no permite modificaciones.");
        }

        var preguntasValidas = inspeccion.Checklist.Preguntas.ToDictionary(p => p.IdPregunta);

        foreach (var item in dto.Respuestas)
        {
            if (!preguntasValidas.TryGetValue(item.IdPregunta, out var pregunta))
            {
                throw new ArgumentException($"La pregunta con ID {item.IdPregunta} no pertenece al checklist de esta inspección.");
            }

            if (pregunta.Obligatoria && string.IsNullOrWhiteSpace(item.Respuesta))
            {
                throw new ArgumentException($"La pregunta '{pregunta.Pregunta}' es obligatoria y no puede estar vacía.");
            }

            if (pregunta.TipoRespuesta == TiposRespuesta.SiNo && !string.IsNullOrWhiteSpace(item.Respuesta))
            {
                var val = item.Respuesta.Trim();
                if (!string.Equals(val, "Si", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(val, "No", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Respuesta inválida para la pregunta Si/No '{pregunta.Pregunta}'. Debe ser 'Si' o 'No'.");
                }
            }

            var respuestaExistente = inspeccion.Respuestas.FirstOrDefault(r => r.IdPregunta == item.IdPregunta);

            if (respuestaExistente != null)
            {
                respuestaExistente.Respuesta = item.Respuesta;
                respuestaExistente.Observacion = item.Observacion;
            }
            else
            {
                inspeccion.Respuestas.Add(new RespuestaInspeccion
                {
                    IdInspeccion = inspeccionId,
                    IdPregunta = item.IdPregunta,
                    Respuesta = item.Respuesta,
                    Observacion = item.Observacion
                });
            }

            // Generación automática de hallazgo ante respuesta negativa ("No")
            bool esNegativa = !string.IsNullOrWhiteSpace(item.Respuesta) && 
                              string.Equals(item.Respuesta.Trim(), "No", StringComparison.OrdinalIgnoreCase);

            if (esNegativa)
            {
                var hallazgoExistente = await _context.Hallazgos
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(h => h.IdInspeccion == inspeccionId && h.IdPregunta == item.IdPregunta && !h.IsDeleted);

                if (hallazgoExistente == null)
                {
                    var nuevoHallazgo = new Hallazgo
                    {
                        IdInspeccion = inspeccionId,
                        IdPregunta = item.IdPregunta,
                        Descripcion = $"Hallazgo automático por respuesta negativa en: {pregunta.Pregunta}",
                        Criticidad = "Media",
                        Estado = "Abierto",
                        FechaRegistro = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    _context.Hallazgos.Add(nuevoHallazgo);
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<RespuestaInspeccionDto>> ObtenerRespuestasAsync(long inspeccionId, long usuarioId, bool esAdmin, bool esAuditor)
    {
        var inspeccion = await _context.Inspecciones
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.IdInspeccion == inspeccionId);

        if (inspeccion == null)
        {
            throw new KeyNotFoundException($"No se encontró la inspección con ID {inspeccionId}");
        }

        if (!esAdmin && !esAuditor && inspeccion.IdUsuario != usuarioId)
        {
            throw new UnauthorizedAccessException("No tienes permiso para consultar las respuestas de esta inspección.");
        }

        return await _context.RespuestasInspeccion
            .AsNoTracking()
            .Where(r => r.IdInspeccion == inspeccionId)
            .Select(r => new RespuestaInspeccionDto
            {
                IdRespuesta = r.IdRespuesta,
                IdInspeccion = r.IdInspeccion,
                IdPregunta = r.IdPregunta,
                Respuesta = r.Respuesta,
                Observacion = r.Observacion
            })
            .ToListAsync();
    }

    public async Task<List<InspeccionDto>> ObtenerMisInspeccionesAsync(long usuarioId, string? estado)
    {
        var query = _context.Inspecciones
            .AsNoTracking()
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Where(i => i.IdUsuario == usuarioId);

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(i => i.Estado == estado);
        }

        return await query
            .OrderByDescending(i => i.FechaInspeccion)
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
                TieneFirma = !string.IsNullOrEmpty(i.FirmaDigital),
                TotalEvidencias = i.Evidencias.Count,
                TotalHallazgos = i.Hallazgos.Count
            })
            .ToListAsync();
    }

    public async Task<PagedResultDto<RespuestaInspeccionDto>> ObtenerRespuestasPaginadasAsync(
        int pageNumber, int pageSize, string? search, long? idInspeccion, long usuarioId, bool esAdmin, bool esAuditor)
    {
        int size = Math.Clamp(pageSize, 1, 100);
        int page = pageNumber < 1 ? 1 : pageNumber;

        var query = _context.RespuestasInspeccion
            .AsNoTracking()
            .Include(r => r.Inspeccion)
            .AsQueryable();

        if (!esAdmin && !esAuditor)
        {
            query = query.Where(r => r.Inspeccion.IdUsuario == usuarioId);
        }

        if (idInspeccion.HasValue)
        {
            query = query.Where(r => r.IdInspeccion == idInspeccion.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                (r.Respuesta != null && r.Respuesta.Contains(search)) ||
                (r.Observacion != null && r.Observacion.Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.IdRespuesta)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new RespuestaInspeccionDto
            {
                IdRespuesta = r.IdRespuesta,
                IdInspeccion = r.IdInspeccion,
                IdPregunta = r.IdPregunta,
                Respuesta = r.Respuesta,
                Observacion = r.Observacion
            })
            .ToListAsync();

        return new PagedResultDto<RespuestaInspeccionDto>
        {
            Data = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = size
        };
    }
}