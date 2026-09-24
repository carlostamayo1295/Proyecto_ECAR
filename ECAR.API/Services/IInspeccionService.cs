using ECAR.Shared.DTOs;

namespace ECAR.API.Services;

public interface IInspeccionService
{
    Task GuardarRespuestasAsync(long inspeccionId, GuardarRespuestasDto dto, long usuarioId);
    Task<IEnumerable<object>> ObtenerMisInspeccionesAsync(long usuarioId);
    Task<IEnumerable<object>> ObtenerRespuestasAsync(long inspeccionId);
}