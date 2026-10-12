using ECAR.Shared.DTOs;

namespace ECAR.API.Services
{
    public interface IReporteService
    {
        Task<List<HistorialEquipoFilaDto>> GetHistorialEquipoAsync(long? idEquipo, DateTime? desde, DateTime? hasta);
        Task<List<HistorialAreaFilaDto>> GetHistorialAreaAsync(string? planta, string? area, DateTime? desde, DateTime? hasta);
        Task<List<HistorialTecnicoFilaDto>> GetHistorialTecnicoAsync(long? idUsuario, DateTime? desde, DateTime? hasta);
        Task<List<EquipoNovedadFilaDto>> GetEquiposNovedadesAsync(string? criticidad, string? estado, DateTime? desde, DateTime? hasta);
        Task<List<CumplimientoFilaDto>> GetCumplimientoAsync(string? planta, string? area, DateTime? desde, DateTime? hasta);
        Task<InspeccionesPorFechasDto> GetInspeccionesFechasAsync(string? planta, string? area, DateTime? desde, DateTime? hasta);
    }
}