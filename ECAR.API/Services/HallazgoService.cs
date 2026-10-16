using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;

namespace ECAR.API.Services;

/// <summary>
/// Hallazgos que nacen de una inspección (PLAN_FASE4_TAREAS §3.5, SRS regla 4): al firmar una
/// inspección con novedad se crea un hallazgo por cada respuesta "No", en la misma transacción.
/// </summary>
public interface IHallazgoService
{
    /// <summary>
    /// Agrega al contexto, sin guardar, un hallazgo Abierto de origen Novedad por cada respuesta
    /// Sí/No marcada como "No". Lo llama FirmarInspeccion antes de su único SaveChanges, así que
    /// o se guardan la firma y los hallazgos juntos o no se guarda nada.
    /// </summary>
    List<Hallazgo> CrearDesdeNovedades(Inspeccion inspeccion, long idUsuarioRegistro, DateTime fechaRegistro);
}

public sealed class HallazgoService(ECARDbContext context) : IHallazgoService
{
    public List<Hallazgo> CrearDesdeNovedades(Inspeccion inspeccion, long idUsuarioRegistro, DateTime fechaRegistro)
    {
        var preguntas = inspeccion.Checklist.Preguntas.ToDictionary(p => p.IdPregunta);
        var hallazgos = inspeccion.Respuestas
            .Where(r => preguntas.TryGetValue(r.IdPregunta, out var p) &&
                        p.TipoRespuesta == TiposRespuesta.SiNo &&
                        string.Equals(r.Respuesta, "No", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => preguntas[r.IdPregunta].Orden)
            .ThenBy(r => r.IdPregunta)
            .Select(r => new Hallazgo
            {
                IdInspeccion = inspeccion.IdInspeccion,
                IdPregunta = r.IdPregunta,
                // La regla 4 obliga a que toda novedad tenga observación, así que nunca queda vacía.
                Descripcion = $"{preguntas[r.IdPregunta].Pregunta}: {r.Observacion?.Trim()}",
                Criticidad = inspeccion.Equipo.Criticidad,
                Estado = HallazgoEstados.Abierto,
                Origen = HallazgoOrigenes.Novedad,
                IdUsuarioRegistro = idUsuarioRegistro,
                FechaRegistro = fechaRegistro
            })
            .ToList();

        context.Hallazgos.AddRange(hallazgos);
        return hallazgos;
    }
}
