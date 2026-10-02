using ECAR.Infrastructure.Data;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services;

/// <summary>Situación de una inspección frente a una escritura (respuestas, evidencias, firma).</summary>
public enum EstadoEscritura
{
    /// <summary>La inspección no existe: responder 404 o 400 según el endpoint.</summary>
    NoExiste,

    /// <summary>La inspección admite escrituras.</summary>
    EnCurso,

    /// <summary>La inspección está cerrada: responder 409 (regla 6 del SRS).</summary>
    Cerrada
}

/// <summary>
/// Guarda de inmutabilidad compartida (regla 6 del SRS). Cualquier endpoint que escriba sobre
/// una inspección —respuestas (BE-1), evidencias (BE-2) o la firma (BE-3)— consulta aquí antes
/// de tocar nada, para que la regla viva en un solo sitio y no se repita en cada controlador.
/// </summary>
public static class InmutabilidadInspeccion
{
    /// <summary>Mensaje único de rechazo, para que el cliente reciba siempre el mismo texto.</summary>
    public const string MensajeCerrada = "La inspección está cerrada y no admite modificaciones";

    /// <summary>Lee solo el estado de la inspección, sin traer el resto de la fila.</summary>
    public static async Task<EstadoEscritura> ObtenerEstadoEscrituraAsync(
        this ECARDbContext context, long idInspeccion)
    {
        var estado = await context.Inspecciones
            .AsNoTracking()
            .Where(i => i.IdInspeccion == idInspeccion)
            .Select(i => i.Estado)
            .FirstOrDefaultAsync();

        if (estado == null)
        {
            return EstadoEscritura.NoExiste;
        }

        return estado == InspeccionEstados.Cerrada
            ? EstadoEscritura.Cerrada
            : EstadoEscritura.EnCurso;
    }
}
