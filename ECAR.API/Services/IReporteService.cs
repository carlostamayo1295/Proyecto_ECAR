using ECAR.Shared.DTOs;

namespace ECAR.API.Services
{
    /// <summary>
    /// Consultas de los reportes del SRS §7 (endpoints 15 a 20). Las cuatro tabulares devuelven
    /// una consulta ordenada para que el controlador pagine la vista previa o lleve todo el rango
    /// al archivo; cumplimiento e inspecciones por fechas se calculan en memoria.
    /// </summary>
    public interface IReporteService
    {
        IQueryable<HistorialEquipoFilaDto> HistorialEquipo(ReporteFiltroDto filtro);
        IQueryable<HistorialAreaFilaDto> HistorialArea(ReporteFiltroDto filtro);
        IQueryable<HistorialTecnicoFilaDto> HistorialTecnico(ReporteFiltroDto filtro);
        IQueryable<EquipoNovedadFilaDto> EquiposNovedades(ReporteFiltroDto filtro);
        Task<List<CumplimientoFilaDto>> CumplimientoAsync(ReporteFiltroDto filtro);
        Task<InspeccionesPorFechasDto> InspeccionesPorFechasAsync(ReporteFiltroDto filtro);
    }
}
