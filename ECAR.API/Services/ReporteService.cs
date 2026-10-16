using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services
{
    /// <summary>
    /// Reportes del SRS §7 sobre inspecciones firmadas (las en curso y las anuladas no son
    /// registros terminados). Las fechas del filtro son días de Colombia, incluidos los dos extremos.
    /// </summary>
    public class ReporteService : IReporteService
    {
        private readonly ECARDbContext _context;

        public ReporteService(ECARDbContext context)
        {
            _context = context;
        }

        public IQueryable<HistorialEquipoFilaDto> HistorialEquipo(ReporteFiltroDto filtro) =>
            Cerradas(filtro)
                .OrderBy(i => i.Equipo.CodigoInterno).ThenBy(i => i.FechaInspeccion)
                .Select(i => new HistorialEquipoFilaDto
                {
                    IdInspeccion = i.IdInspeccion,
                    CodigoInterno = i.Equipo.CodigoInterno,
                    ActivoFijo = i.Equipo.ActivoFijo,
                    NombreEquipo = i.Equipo.NombreEquipo,
                    Marca = i.Equipo.Marca,
                    Modelo = i.Equipo.Modelo,
                    Area = i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Planta + " - " + i.Equipo.Ubicacion.Area : null,
                    FechaInspeccion = i.FechaInspeccion,
                    Resultado = i.Resultado,
                    Observaciones = i.Observaciones,
                    Inspector = i.Usuario.Nombre
                });

        public IQueryable<HistorialAreaFilaDto> HistorialArea(ReporteFiltroDto filtro) =>
            Cerradas(filtro)
                .OrderBy(i => i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Planta : "")
                .ThenBy(i => i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Area : "")
                .ThenBy(i => i.FechaInspeccion)
                .Select(i => new HistorialAreaFilaDto
                {
                    IdInspeccion = i.IdInspeccion,
                    Planta = i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Planta : SinUbicacion,
                    Area = i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Area : SinUbicacion,
                    Equipo = i.Equipo.CodigoInterno + " - " + i.Equipo.NombreEquipo,
                    Fecha = i.FechaInspeccion,
                    Resultado = i.Resultado,
                    Responsable = i.Usuario.Nombre
                });

        public IQueryable<HistorialTecnicoFilaDto> HistorialTecnico(ReporteFiltroDto filtro) =>
            Cerradas(filtro)
                .OrderBy(i => i.Usuario.Nombre).ThenBy(i => i.FechaInspeccion)
                .Select(i => new HistorialTecnicoFilaDto
                {
                    IdInspeccion = i.IdInspeccion,
                    Tecnico = i.Usuario.Nombre,
                    Equipo = i.Equipo.CodigoInterno + " - " + i.Equipo.NombreEquipo,
                    FechaInspeccion = i.FechaInspeccion,
                    Resultado = i.Resultado,
                    Observaciones = i.Observaciones
                });

        public IQueryable<EquipoNovedadFilaDto> EquiposNovedades(ReporteFiltroDto filtro)
        {
            var (inicio, fin) = HoraColombia.RangoUtc(filtro.Desde, filtro.Hasta);
            var query = _context.Hallazgos.AsNoTracking()
                .Where(h => h.FechaRegistro >= inicio!.Value && h.FechaRegistro < fin!.Value);

            if (!string.IsNullOrWhiteSpace(filtro.Estado))
                query = query.Where(h => h.Estado == filtro.Estado);
            if (!string.IsNullOrWhiteSpace(filtro.Criticidad))
                query = query.Where(h => h.Criticidad == filtro.Criticidad);
            if (filtro.IdEquipo.HasValue)
                query = query.Where(h => h.Inspeccion.IdEquipo == filtro.IdEquipo.Value);
            if (!string.IsNullOrWhiteSpace(filtro.Planta))
                query = query.Where(h => h.Inspeccion.Equipo.Ubicacion != null && h.Inspeccion.Equipo.Ubicacion.Planta == filtro.Planta);
            if (!string.IsNullOrWhiteSpace(filtro.Area))
                query = query.Where(h => h.Inspeccion.Equipo.Ubicacion != null && h.Inspeccion.Equipo.Ubicacion.Area == filtro.Area);

            return query
                .OrderBy(h => h.FechaRegistro).ThenBy(h => h.IdHallazgo)
                .Select(h => new EquipoNovedadFilaDto
                {
                    IdHallazgo = h.IdHallazgo,
                    Equipo = h.Inspeccion.Equipo.CodigoInterno + " - " + h.Inspeccion.Equipo.NombreEquipo,
                    Area = h.Inspeccion.Equipo.Ubicacion != null
                        ? h.Inspeccion.Equipo.Ubicacion.Planta + " - " + h.Inspeccion.Equipo.Ubicacion.Area
                        : null,
                    Hallazgo = h.Descripcion,
                    Criticidad = h.Criticidad,
                    Fecha = h.FechaRegistro,
                    Responsable = h.UsuarioResponsable != null ? h.UsuarioResponsable.Nombre : null,
                    Estado = h.Estado
                });
        }

        /// <summary>
        /// Cumplimiento por mes y por planta/área (pendiente de confirmar la definición con ECAR):
        /// programados = equipos activos con FrecuenciaInspeccion que ya existían en el periodo;
        /// inspeccionados = cuántos de ellos tienen al menos una inspección firmada en el periodo.
        /// Los de frecuencia Trimestral se programan una vez por trimestre, en el primer mes del
        /// trimestre que cae en el rango, y cuenta cualquier inspección de ese trimestre.
        /// Un equipo sin frecuencia no entra.
        /// </summary>
        public async Task<List<CumplimientoFilaDto>> CumplimientoAsync(ReporteFiltroDto filtro)
        {
            var desde = filtro.Desde!.Value.Date;
            var hasta = filtro.Hasta!.Value.Date;
            var (inicio, fin) = HoraColombia.RangoUtc(desde, hasta);

            var equiposQuery = _context.Equipos.AsNoTracking()
                .Where(e => e.Activo && e.FrecuenciaInspeccion != null);
            if (!string.IsNullOrWhiteSpace(filtro.Planta))
                equiposQuery = equiposQuery.Where(e => e.Ubicacion != null && e.Ubicacion.Planta == filtro.Planta);
            if (!string.IsNullOrWhiteSpace(filtro.Area))
                equiposQuery = equiposQuery.Where(e => e.Ubicacion != null && e.Ubicacion.Area == filtro.Area);

            var equipos = await equiposQuery
                .Select(e => new
                {
                    e.IdEquipo,
                    e.FrecuenciaInspeccion,
                    e.FechaCreacion,
                    Planta = e.Ubicacion != null ? e.Ubicacion.Planta : SinUbicacion,
                    Area = e.Ubicacion != null ? e.Ubicacion.Area : SinUbicacion
                })
                .ToListAsync();
            var idsEquipos = equipos.Select(e => e.IdEquipo).ToList();

            var inspecciones = await _context.Inspecciones.AsNoTracking()
                .Where(i => i.Estado == InspeccionEstados.Cerrada &&
                            i.FechaInspeccion >= inicio!.Value && i.FechaInspeccion < fin!.Value &&
                            idsEquipos.Contains(i.IdEquipo))
                .Select(i => new { i.IdEquipo, i.FechaInspeccion })
                .ToListAsync();
            var diasInspeccionados = inspecciones
                .GroupBy(i => i.IdEquipo)
                .ToDictionary(g => g.Key, g => g.Select(i => HoraColombia.DesdeUtc(i.FechaInspeccion).Date).ToList());

            var filas = new List<CumplimientoFilaDto>();
            var trimestresYaProgramados = new HashSet<(int Anio, int Trimestre)>();
            for (var mes = new DateTime(desde.Year, desde.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
            {
                var inicioPeriodo = mes < desde ? desde : mes;
                var finPeriodo = mes.AddMonths(1).AddDays(-1) > hasta ? hasta : mes.AddMonths(1).AddDays(-1);
                var trimestre = (mes.Year, (mes.Month - 1) / 3);
                var esPrimerMesDelTrimestre = trimestresYaProgramados.Add(trimestre);
                var inicioTrimestre = new DateTime(mes.Year, trimestre.Item2 * 3 + 1, 1);
                var finTrimestre = inicioTrimestre.AddMonths(3).AddDays(-1);

                foreach (var grupo in equipos.GroupBy(e => (e.Planta, e.Area)).OrderBy(g => g.Key.Planta).ThenBy(g => g.Key.Area))
                {
                    var programados = 0;
                    var inspeccionados = 0;
                    foreach (var equipo in grupo)
                    {
                        if (HoraColombia.DesdeUtc(equipo.FechaCreacion).Date > finPeriodo)
                        {
                            continue;
                        }

                        var trimestral = equipo.FrecuenciaInspeccion == FrecuenciasInspeccion.Trimestral;
                        if (trimestral && !esPrimerMesDelTrimestre)
                        {
                            continue;
                        }

                        programados++;
                        var (ventanaInicio, ventanaFin) = trimestral
                            ? (Max(inicioTrimestre, desde), Min(finTrimestre, hasta))
                            : (inicioPeriodo, finPeriodo);
                        if (diasInspeccionados.TryGetValue(equipo.IdEquipo, out var dias) &&
                            dias.Any(d => d >= ventanaInicio && d <= ventanaFin))
                        {
                            inspeccionados++;
                        }
                    }

                    if (programados > 0)
                    {
                        filas.Add(new CumplimientoFilaDto
                        {
                            Periodo = mes.ToString("yyyy-MM"),
                            Planta = grupo.Key.Planta,
                            Area = grupo.Key.Area,
                            EquiposProgramados = programados,
                            EquiposInspeccionados = inspeccionados,
                            PorcentajeCumplimiento = Math.Round(100m * inspeccionados / programados, 1)
                        });
                    }
                }
            }

            return filas;
        }

        public async Task<InspeccionesPorFechasDto> InspeccionesPorFechasAsync(ReporteFiltroDto filtro)
        {
            var porArea = await Cerradas(filtro)
                .GroupBy(i => new
                {
                    Planta = i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Planta : SinUbicacion,
                    Area = i.Equipo.Ubicacion != null ? i.Equipo.Ubicacion.Area : SinUbicacion
                })
                .Select(g => new InspeccionesPorFechasFilaDto
                {
                    Planta = g.Key.Planta,
                    Area = g.Key.Area,
                    TotalInspecciones = g.Count(),
                    Satisfactorias = g.Count(i => i.Resultado == InspeccionResultados.Conforme),
                    ConNovedad = g.Count(i => i.Resultado == InspeccionResultados.ConNovedad)
                })
                .ToListAsync();

            porArea = porArea.OrderBy(f => f.Planta).ThenBy(f => f.Area).ToList();
            return new InspeccionesPorFechasDto
            {
                Desde = filtro.Desde!.Value.Date,
                Hasta = filtro.Hasta!.Value.Date,
                TotalInspecciones = porArea.Sum(f => f.TotalInspecciones),
                Satisfactorias = porArea.Sum(f => f.Satisfactorias),
                ConNovedad = porArea.Sum(f => f.ConNovedad),
                PorArea = porArea
            };
        }

        private const string SinUbicacion = "Sin ubicación";

        /// <summary>Inspecciones firmadas del rango con los filtros comunes de los historiales.</summary>
        private IQueryable<Inspeccion> Cerradas(ReporteFiltroDto filtro)
        {
            var (inicio, fin) = HoraColombia.RangoUtc(filtro.Desde, filtro.Hasta);
            var query = _context.Inspecciones.AsNoTracking()
                .Where(i => i.Estado == InspeccionEstados.Cerrada &&
                            i.FechaInspeccion >= inicio!.Value && i.FechaInspeccion < fin!.Value);

            if (filtro.IdEquipo.HasValue)
                query = query.Where(i => i.IdEquipo == filtro.IdEquipo.Value);
            if (filtro.IdUsuario.HasValue)
                query = query.Where(i => i.IdUsuario == filtro.IdUsuario.Value);
            if (!string.IsNullOrWhiteSpace(filtro.Planta))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Planta == filtro.Planta);
            if (!string.IsNullOrWhiteSpace(filtro.Area))
                query = query.Where(i => i.Equipo.Ubicacion != null && i.Equipo.Ubicacion.Area == filtro.Area);

            return query;
        }

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    }
}
