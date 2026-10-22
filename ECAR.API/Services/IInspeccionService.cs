using ECAR.Shared.DTOs;

namespace ECAR.API.Services;

public interface IInspeccionService
{
    Task GuardarRespuestasAsync(long inspeccionId, GuardarRespuestasDto dto, long usuarioId, bool esAdmin);
    /// <param name="leeTodas">Administrador o Supervisor.</param>
    /// <param name="veAjenasConNovedades">Estándar: también las ajenas que tienen novedades (AccesoInspeccion).</param>
    Task<List<RespuestaInspeccionDto>> ObtenerRespuestasAsync(long inspeccionId, long usuarioId, bool leeTodas, bool veAjenasConNovedades);
    Task<List<InspeccionDto>> ObtenerMisInspeccionesAsync(long usuarioId, string? estado);
    Task<PagedResultDto<RespuestaInspeccionDto>> ObtenerRespuestasPaginadasAsync(int pageNumber, int pageSize, string? search, long? idInspeccion, long usuarioId, bool esAdmin, bool esSupervisor);
}