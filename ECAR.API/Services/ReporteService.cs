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

        public IQueryable<HistorialUsuarioFilaDto> HistorialUsuario(ReporteFiltroDto filtro) =>
            Cerradas(filtro)
                .OrderBy(i => i.Usuario.Nombre).ThenBy(i => i.FechaInspeccion)
                .Select(i => new HistorialUsuarioFilaDto
                {
                    IdInspeccion = i.IdInspeccion,
                    Usuario = i.Usuario.Nombre,
                    Equipo = i.Equipo.CodigoInterno + " - " + i.Equipo.NombreEquipo,
                    FechaInspeccion = i.FechaInspeccion,
                    Resultado = i.Resultado,
                    Observaciones = i.Observaciones
                });

        public IQueryable<EquipoNovedadFilaDto> EquiposNovedades(ReporteFiltroDto filtro)
        {
            var (inicio, fin) = HoraColombia.RangoUtc(filtro.Desde, filtro.Hasta);
            var query = _context.Novedades.AsNoTracking()
                .Where(n => n.FechaRegistro >= inicio!.Value && n.FechaRegistro < fin!.Value);

            var estados = Valores(filtro.Estados);
            var criticidades = Valores(filtro.Criticidades);
            var plantas = Valores(filtro.Plantas);
            var areas = Valores(filtro.Areas);
            var equipos = filtro.IdEquipos.Distinct().ToList();
            if (estados.Count > 0)
                query = query.Where(n => estados.Contains(n.Estado));
            if (criticidades.Count > 0)
                query = query.Where(n => n.Criticidad != null && criticidades.Contains(n.Criticidad));
            if (equipos.Count > 0)
                query = query.Where(n => equipos.Contains(n.Inspeccion.IdEquipo));
            if (plantas.Count > 0)
                query = query.Where(n => n.Inspeccion.Equipo.Ubicacion != null && plantas.Contains(n.Inspeccion.Equipo.Ubicacion.Planta));
            if (areas.Count > 0)
                query = query.Where(n => n.Inspeccion.Equipo.Ubicacion != null && areas.Contains(n.Inspeccion.Equipo.Ubicacion.Area));

            // "Responsable" (SRS §7) es quien firmó la resolución; vacío mientras está pendiente.
            return query
                .OrderBy(n => n.FechaRegistro).ThenBy(n => n.IdNovedad)
                .Select(n => new EquipoNovedadFilaDto
                {
                    IdNovedad = n.IdNovedad,
                    Equipo = n.Inspeccion.Equipo.CodigoInterno + " - " + n.Inspeccion.Equipo.NombreEquipo,
                    Area = n.Inspeccion.Equipo.Ubicacion != null
                        ? n.Inspeccion.Equipo.Ubicacion.Planta + " - " + n.Inspeccion.Equipo.Ubicacion.Area
                        : null,
                    Novedad = n.Descripcion,
                    Criticidad = n.Criticidad,
                    Fecha = n.FechaRegistro,
                    Responsable = n.UsuarioResolucion != null ? n.UsuarioResolucion.Nombre : null,
                    Estado = n.Estado
                });
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
                    // "Con novedad" es haber encontrado alguna, resuelta o no: al resolverse la última, el
                    // resultado pasa a Conforme, pero la inspección sigue contando aquí. ConNovedad (sin
                    // filas en Novedades) es de las firmadas antes de las novedades. Las firmadas sin
                    // novedades antes del 29/10/2026 quedaron Conforme en lugar de Satisfactorio.
                    Satisfactorias = g.Count(i => i.Resultado == InspeccionResultados.Satisfactorio ||
                                                  (i.Resultado == InspeccionResultados.Conforme && !i.Novedades.Any())),
                    ConNovedad = g.Count(i => i.Novedades.Any() || i.Resultado == InspeccionResultados.ConNovedad ||
                                              i.Resultado == InspeccionResultados.Pendiente)
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

        /// <summary>
        /// Inspecciones firmadas del rango (cerradas o con novedades pendientes) con los filtros
        /// comunes de los historiales.
        /// </summary>
        private IQueryable<Inspeccion> Cerradas(ReporteFiltroDto filtro)
        {
            var (inicio, fin) = HoraColombia.RangoUtc(filtro.Desde, filtro.Hasta);
            var query = _context.Inspecciones.AsNoTracking()
                .Where(i => (i.Estado == InspeccionEstados.Cerrada || i.Estado == InspeccionEstados.NovedadesPendientes) &&
                            i.FechaInspeccion >= inicio!.Value && i.FechaInspeccion < fin!.Value);

            var equipos = filtro.IdEquipos.Distinct().ToList();
            var usuarios = filtro.IdUsuarios.Distinct().ToList();
            var plantas = Valores(filtro.Plantas);
            var areas = Valores(filtro.Areas);
            if (equipos.Count > 0)
                query = query.Where(i => equipos.Contains(i.IdEquipo));
            if (usuarios.Count > 0)
                query = query.Where(i => usuarios.Contains(i.IdUsuario));
            if (plantas.Count > 0)
                query = query.Where(i => i.Equipo.Ubicacion != null && plantas.Contains(i.Equipo.Ubicacion.Planta));
            if (areas.Count > 0)
                query = query.Where(i => i.Equipo.Ubicacion != null && areas.Contains(i.Equipo.Ubicacion.Area));

            return query;
        }

        /// <summary>Los valores de un filtro de texto sin espacios externos, sin vacíos y sin repetir.</summary>
        internal static List<string> Valores(IEnumerable<string>? valores) =>
            (valores ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
