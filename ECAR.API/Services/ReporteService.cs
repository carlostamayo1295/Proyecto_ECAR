using ECAR.Infrastructure.Data;
using ECAR.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services
{
    public class ReporteService : IReporteService
    {
        private readonly ECARDbContext _context;

        public ReporteService(ECARDbContext context)
        {
            _context = context;
        }

        public async Task<List<HistorialEquipoFilaDto>> GetHistorialEquipoAsync(long? idEquipo, DateTime? desde, DateTime? hasta)
        {
            // Con Select a DTO no hacen falta los Include: EF trae solo las columnas usadas.
            var query = _context.Inspecciones
                .AsNoTracking()
                .AsQueryable();

            if (idEquipo.HasValue)
                query = query.Where(i => i.IdEquipo == idEquipo.Value);

            if (desde.HasValue)
                query = query.Where(i => i.FechaInspeccion >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(i => i.FechaInspeccion <= hasta.Value);

            return await query.Select(i => new HistorialEquipoFilaDto
            {
                IdInspeccion = i.IdInspeccion,
                CodigoInterno = i.Equipo != null ? i.Equipo.CodigoInterno : string.Empty,
                ActivoFijo = i.Equipo != null ? i.Equipo.ActivoFijo : string.Empty,
                NombreEquipo = i.Equipo != null ? i.Equipo.NombreEquipo : string.Empty,
                Marca = i.Equipo != null ? i.Equipo.Marca : null,
                Modelo = i.Equipo != null ? i.Equipo.Modelo : null,
                Area = i.Equipo != null && i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Area : null,
                FechaInspeccion = i.FechaInspeccion,
                Resultado = i.Resultado,
                Observaciones = i.Observaciones,
                Inspector = i.Usuario != null ? i.Usuario.Nombre : string.Empty
            }).ToListAsync();
        }

        public async Task<List<HistorialAreaFilaDto>> GetHistorialAreaAsync(string? planta, string? area, DateTime? desde, DateTime? hasta)
        {
            var query = _context.Inspecciones
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(planta))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Planta == planta);

            if (!string.IsNullOrEmpty(area))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Area == area);

            if (desde.HasValue)
                query = query.Where(i => i.FechaInspeccion >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(i => i.FechaInspeccion <= hasta.Value);

            return await query.Select(i => new HistorialAreaFilaDto
            {
                IdInspeccion = i.IdInspeccion,
                Planta = i.Equipo != null && i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Planta : string.Empty,
                Area = i.Equipo != null && i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Area : string.Empty,
                Equipo = i.Equipo != null ? i.Equipo.NombreEquipo : string.Empty,
                Fecha = i.FechaInspeccion,
                Resultado = i.Resultado,
                Responsable = i.Usuario != null ? i.Usuario.Nombre : string.Empty
            }).ToListAsync();
        }

        public async Task<List<HistorialTecnicoFilaDto>> GetHistorialTecnicoAsync(long? idUsuario, DateTime? desde, DateTime? hasta)
        {
            var query = _context.Inspecciones
                .AsNoTracking()
                .AsQueryable();

            if (idUsuario.HasValue)
                query = query.Where(i => i.IdUsuario == idUsuario.Value);

            if (desde.HasValue)
                query = query.Where(i => i.FechaInspeccion >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(i => i.FechaInspeccion <= hasta.Value);

            return await query.Select(i => new HistorialTecnicoFilaDto
            {
                IdInspeccion = i.IdInspeccion,
                Tecnico = i.Usuario != null ? i.Usuario.Nombre : string.Empty,
                Equipo = i.Equipo != null ? i.Equipo.NombreEquipo : string.Empty,
                FechaInspeccion = i.FechaInspeccion,
                Resultado = i.Resultado,
                Observaciones = i.Observaciones
            }).ToListAsync();
        }

        public async Task<List<EquipoNovedadFilaDto>> GetEquiposNovedadesAsync(string? criticidad, string? estado, DateTime? desde, DateTime? hasta)
        {
            var query = _context.Hallazgos
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(criticidad))
                query = query.Where(h => h.Criticidad == criticidad);

            if (!string.IsNullOrEmpty(estado))
                query = query.Where(h => h.Estado == estado);

            // La fecha del hallazgo en la entidad se llama FechaRegistro.
            if (desde.HasValue)
                query = query.Where(h => h.FechaRegistro >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(h => h.FechaRegistro <= hasta.Value);

            return await query.Select(h => new EquipoNovedadFilaDto
            {
                IdHallazgo = h.IdHallazgo,
                Equipo = h.Inspeccion != null && h.Inspeccion.Equipo != null ? h.Inspeccion.Equipo.NombreEquipo : string.Empty,
                Area = h.Inspeccion != null && h.Inspeccion.Equipo != null && h.Inspeccion.Equipo.Ubicacion != null ? h.Inspeccion.Equipo.Ubicacion.Area : null,
                Hallazgo = h.Descripcion,
                Criticidad = h.Criticidad,
                Fecha = h.FechaRegistro,
                // El plan pide el responsable del hallazgo (puede no estar asignado).
                Responsable = h.UsuarioResponsable != null ? h.UsuarioResponsable.Nombre : string.Empty,
                Estado = h.Estado
            }).ToListAsync();
        }

        public async Task<List<CumplimientoFilaDto>> GetCumplimientoAsync(string? planta, string? area, DateTime? desde, DateTime? hasta)
        {
            // Aquí se agrupa en memoria, así que sí se necesitan los Include.
            var query = _context.Inspecciones
                .AsNoTracking()
                .Include(i => i.Equipo)
                .ThenInclude(e => e.Ubicacion)
                .AsQueryable();

            if (!string.IsNullOrEmpty(planta))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Planta == planta);

            if (!string.IsNullOrEmpty(area))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Area == area);

            if (desde.HasValue)
                query = query.Where(i => i.FechaInspeccion >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(i => i.FechaInspeccion <= hasta.Value);

            var inspecciones = await query.ToListAsync();

            var resultado = inspecciones
                .GroupBy(i => new
                {
                    Planta = i.Equipo != null && i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Planta : "General",
                    Area = i.Equipo != null && i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Area : "General"
                })
                .Select(g => new CumplimientoFilaDto
                {
                    Periodo = desde?.ToString("yyyy-MM") ?? "Actual",
                    Planta = g.Key.Planta,
                    Area = g.Key.Area,
                    EquiposProgramados = g.Select(x => x.IdEquipo).Distinct().Count(),
                    EquiposInspeccionados = g.Count(),
                    PorcentajeCumplimiento = g.Count() > 0 ? 100m : 0m
                }).ToList();

            return resultado;
        }

        public async Task<InspeccionesPorFechasDto> GetInspeccionesFechasAsync(string? planta, string? area, DateTime? desde, DateTime? hasta)
        {
            // Aquí también se agrupa en memoria, así que se mantienen los Include.
            var query = _context.Inspecciones
                .AsNoTracking()
                .Include(i => i.Equipo)
                .ThenInclude(e => e.Ubicacion)
                .AsQueryable();

            if (!string.IsNullOrEmpty(planta))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Planta == planta);

            if (!string.IsNullOrEmpty(area))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Area == area);

            if (desde.HasValue)
                query = query.Where(i => i.FechaInspeccion >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(i => i.FechaInspeccion <= hasta.Value);

            var listado = await query.ToListAsync();

            var total = listado.Count;
            var conNovedad = listado.Count(i => i.Estado == "ConNovedad" || i.Resultado == "ConNovedad");
            var satisfactorias = total - conNovedad;

            var porArea = listado
                .GroupBy(i => new
                {
                    Planta = i.Equipo != null && i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Planta : "General",
                    Area = i.Equipo != null && i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Area : "General"
                })
                .Select(g => new InspeccionesPorFechasFilaDto
                {
                    Planta = g.Key.Planta,
                    Area = g.Key.Area,
                    TotalInspecciones = g.Count(),
                    Satisfactorias = g.Count(i => i.Estado != "ConNovedad" && i.Resultado != "ConNovedad"),
                    ConNovedad = g.Count(i => i.Estado == "ConNovedad" || i.Resultado == "ConNovedad")
                }).ToList();

            return new InspeccionesPorFechasDto
            {
                Desde = desde ?? DateTime.MinValue,
                Hasta = hasta ?? DateTime.MaxValue,
                TotalInspecciones = total,
                Satisfactorias = satisfactorias,
                ConNovedad = conNovedad,
                PorArea = porArea
            };
        }
    }
}