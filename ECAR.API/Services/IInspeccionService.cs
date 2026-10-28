using ECAR.Shared.DTOs;

namespace ECAR.API.Services;

public interface IInspeccionService
{
    Task GuardarRespuestasAsync(long inspeccionId, GuardarRespuestasDto dto, long usuarioId, bool esAdmin);
    /// <param name="leeTodas">Administrador, Supervisor o Estándar (AccesoInspeccion); si no, solo las suyas.</param>
    Task<List<RespuestaInspeccionDto>> ObtenerRespuestasAsync(long inspeccionId, long usuarioId, bool leeTodas);
    Task<List<InspeccionDto>> ObtenerMisInspeccionesAsync(long usuarioId, string? estado);
    /// <param name="leeTodas">Administrador, Supervisor o Estándar (AccesoInspeccion); si no, solo las suyas.</param>
    Task<PagedResultDto<RespuestaInspeccionDto>> ObtenerRespuestasPaginadasAsync(int pageNumber, int pageSize, string? search, long? idInspeccion, long usuarioId, bool leeTodas);
}