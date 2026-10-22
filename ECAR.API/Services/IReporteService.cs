using ECAR.Shared.DTOs;

namespace ECAR.API.Services
{
    /// <summary>
    /// Consultas de los reportes del SRS §7. Las cuatro tabulares devuelven una consulta ordenada
    /// para que el controlador pagine la vista previa o lleve todo el rango al archivo; inspecciones
    /// por fechas se calcula en memoria. El de cumplimiento se retiró a pedido de ECAR (21/10/2026).
    /// </summary>
    public interface IReporteService
    {
        IQueryable<HistorialEquipoFilaDto> HistorialEquipo(ReporteFiltroDto filtro);
        IQueryable<HistorialAreaFilaDto> HistorialArea(ReporteFiltroDto filtro);
        IQueryable<HistorialUsuarioFilaDto> HistorialUsuario(ReporteFiltroDto filtro);
        IQueryable<EquipoNovedadFilaDto> EquiposNovedades(ReporteFiltroDto filtro);
        Task<InspeccionesPorFechasDto> InspeccionesPorFechasAsync(ReporteFiltroDto filtro);
    }
}
