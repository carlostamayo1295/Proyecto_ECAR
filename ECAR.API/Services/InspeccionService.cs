using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services;

public class InspeccionService : IInspeccionService
{
    private readonly ECARDbContext _context; // Cambia ApplicationDbContext por el nombre exacto de tu DbContext

    public InspeccionService(ECARDbContext context)
    {
        _context = context;
    }

    public async Task GuardarRespuestasAsync(long inspeccionId, GuardarRespuestasDto dto, long usuarioId)
    {
        // 1. Obtener la inspección cargando sus respuestas
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Respuestas)
            .FirstOrDefaultAsync(i => i.IdInspeccion == inspeccionId);

        if (inspeccion == null)
        {
            throw new KeyNotFoundException($"No se encontró la inspección con ID {inspeccionId}");
        }

        // 2. Validación de propiedad: solo el usuario asignado a la inspección puede modificarla
        if (inspeccion.IdUsuario != usuarioId)
        {
            throw new UnauthorizedAccessException("No tienes permiso para modificar esta inspección.");
        }

        // 3. Validación 21 CFR Part 11 / Negocio: Solo si está en curso
        if (inspeccion.Estado != InspeccionEstados.EnCurso)
        {
            throw new InvalidOperationException("Solo se pueden registrar respuestas en inspecciones que estén 'En curso'.");
        }

        // 4. Lógica de Upsert (Actualiza si existe, inserta si no)
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
                    IdInspeccion = inspeccionId,
                    IdPregunta = item.IdPregunta,
                    Respuesta = item.Respuesta,
                    Observacion = item.Observacion
                });
            }
        }

        // 5. Guardar en SQL Server
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<object>> ObtenerMisInspeccionesAsync(long usuarioId)
    {
        return await _context.Inspecciones
            .Where(i => i.IdUsuario == usuarioId)
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> ObtenerRespuestasAsync(long inspeccionId)
    {
        return await _context.RespuestasInspeccion
            .Where(r => r.IdInspeccion == inspeccionId)
            .ToListAsync();
    }
}