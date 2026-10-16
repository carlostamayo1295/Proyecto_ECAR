using ECAR.API.Services;
using ECAR.API.Services.Documentos;
using ECAR.Infrastructure.Data;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers
{
    /// <summary>
    /// Reportes del SRS §7 (endpoints 15 a 20, PLAN_FASE4_TAREAS §3.9). Un solo endpoint por reporte
    /// con formato=json (vista previa paginada), pdf o xlsx (todo el rango), así que la vista previa
    /// y el archivo no pueden discrepar. Las fechas son obligatorias, con un año como máximo, y cada
    /// archivo generado queda en la auditoría como Exportacion.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador,Auditor")]
    public class ReportesController : ControllerBase
    {
        private readonly IReporteService _reporteService;
        private readonly ECARDbContext _context;
        private readonly ICurrentUser _currentUser;

        public ReportesController(IReporteService reporteService, ECARDbContext context, ICurrentUser currentUser)
        {
            _reporteService = reporteService;
            _context = context;
            _currentUser = currentUser;
        }

        [HttpGet("historial-equipo")]
        public Task<IActionResult> GetHistorialEquipo([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.HistorialEquipo, "Historial por equipo", filtro, formato, page, pageSize,
                () => _reporteService.HistorialEquipo(filtro),
                [new("Inspección", 1.6), new("Código", 2), new("Activo fijo", 2), new("Equipo", 4), new("Marca / modelo", 3),
                 new("Área", 3), new("Fecha", 2.4), new("Resultado", 2), new("Observaciones", 3.6), new("Inspector", 2.5)],
                f => [f.IdInspeccion, f.CodigoInterno, f.ActivoFijo, f.NombreEquipo, $"{f.Marca} {f.Modelo}".Trim(),
                      f.Area, HoraColombia.DesdeUtc(f.FechaInspeccion), Resultado(f.Resultado), f.Observaciones, f.Inspector]);

        [HttpGet("historial-area")]
        public Task<IActionResult> GetHistorialArea([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.HistorialArea, "Historial por área", filtro, formato, page, pageSize,
                () => _reporteService.HistorialArea(filtro),
                [new("Inspección", 2), new("Planta", 3.5), new("Área", 3.5), new("Equipo", 6), new("Fecha", 3),
                 new("Resultado", 3), new("Responsable", 4.7)],
                f => [f.IdInspeccion, f.Planta, f.Area, f.Equipo, HoraColombia.DesdeUtc(f.Fecha), Resultado(f.Resultado), f.Responsable]);

        [HttpGet("historial-tecnico")]
        public Task<IActionResult> GetHistorialTecnico([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.HistorialTecnico, "Historial por técnico", filtro, formato, page, pageSize,
                () => _reporteService.HistorialTecnico(filtro),
                [new("Inspección", 2), new("Técnico", 4.5), new("Equipo", 6), new("Fecha", 3), new("Resultado", 3),
                 new("Observaciones", 7.2)],
                f => [f.IdInspeccion, f.Tecnico, f.Equipo, HoraColombia.DesdeUtc(f.FechaInspeccion), Resultado(f.Resultado), f.Observaciones]);

        [HttpGet("equipos-novedades")]
        public Task<IActionResult> GetEquiposNovedades([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.EquiposNovedades, "Equipos con novedades", filtro, formato, page, pageSize,
                () => _reporteService.EquiposNovedades(filtro),
                [new("Hallazgo", 1.6), new("Equipo", 4.5), new("Área", 3.2), new("Descripción", 7), new("Criticidad", 1.8),
                 new("Fecha", 2.4), new("Responsable", 3), new("Estado", 2.2)],
                f => [f.IdHallazgo, f.Equipo, f.Area, f.Hallazgo, f.Criticidad, HoraColombia.DesdeUtc(f.Fecha),
                      f.Responsable ?? "Sin asignar", Estado(f.Estado)]);

        [HttpGet("cumplimiento")]
        public async Task<IActionResult> GetCumplimiento([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var error = Validar(filtro, formato);
            if (error != null)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(error));
            }

            var filas = await _reporteService.CumplimientoAsync(filtro);
            if (formato == ReporteFormatos.Json)
            {
                return Ok(ApiResponse<PagedResultDto<CumplimientoFilaDto>>.SuccessResponse(Paginar(filas, page, pageSize)));
            }

            var programados = filas.Sum(f => f.EquiposProgramados);
            var inspeccionados = filas.Sum(f => f.EquiposInspeccionados);
            var resumen = new List<(string, string)>
            {
                ("Equipos programados", programados.ToString()),
                ("Equipos inspeccionados", inspeccionados.ToString()),
                ("Cumplimiento total", programados == 0 ? "—" : $"{Math.Round(100m * inspeccionados / programados, 1)} %"),
                ("Definición", "programados = equipos activos con frecuencia de inspección; inspeccionados = con al menos una inspección firmada en el periodo")
            };

            return await Archivo(ReporteTipos.Cumplimiento, "Cumplimiento de inspecciones", filtro, formato,
                [new("Periodo", 2.5), new("Planta", 5), new("Área", 5), new("Programados", 3.5), new("Inspeccionados", 3.5),
                 new("Cumplimiento %", 3.5)],
                filas.Select(f => (IReadOnlyList<object?>)[f.Periodo, f.Planta, f.Area, f.EquiposProgramados,
                    f.EquiposInspeccionados, f.PorcentajeCumplimiento]).ToList(),
                resumen);
        }

        [HttpGet("inspecciones-fechas")]
        public async Task<IActionResult> GetInspeccionesFechas([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json)
        {
            var error = Validar(filtro, formato);
            if (error != null)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(error));
            }

            var resultado = await _reporteService.InspeccionesPorFechasAsync(filtro);
            if (formato == ReporteFormatos.Json)
            {
                return Ok(ApiResponse<InspeccionesPorFechasDto>.SuccessResponse(resultado));
            }

            var resumen = new List<(string, string)>
            {
                ("Total de inspecciones", resultado.TotalInspecciones.ToString()),
                ("Satisfactorias (conformes)", resultado.Satisfactorias.ToString()),
                ("Con novedad", resultado.ConNovedad.ToString())
            };

            return await Archivo(ReporteTipos.InspeccionesFechas, "Inspecciones por fechas", filtro, formato,
                [new("Planta", 6), new("Área", 6), new("Inspecciones", 4.5), new("Satisfactorias", 4.5), new("Con novedad", 4.5)],
                resultado.PorArea.Select(f => (IReadOnlyList<object?>)[f.Planta, f.Area, f.TotalInspecciones, f.Satisfactorias,
                    f.ConNovedad]).ToList(),
                resumen);
        }

        /// <summary>Vista previa paginada (json) o archivo con todo el rango (pdf/xlsx).</summary>
        private async Task<IActionResult> Tabular<T>(string tipo, string titulo, ReporteFiltroDto filtro, string formato,
            int page, int pageSize, Func<IQueryable<T>> consulta, Columna[] columnas, Func<T, IReadOnlyList<object?>> fila)
        {
            var error = Validar(filtro, formato);
            if (error != null)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(error));
            }

            var query = consulta();
            if (formato == ReporteFormatos.Json)
            {
                page = Math.Max(page, 1);
                pageSize = Math.Clamp(pageSize, 1, 200);
                var total = await query.CountAsync();
                var datos = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                return Ok(ApiResponse<PagedResultDto<T>>.SuccessResponse(new PagedResultDto<T>
                {
                    Data = datos,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize
                }));
            }

            var cantidad = await query.CountAsync();
            if (cantidad > ExportadorTabla.MaximoFilas)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(
                    $"El reporte tiene {cantidad} filas; el máximo por archivo es {ExportadorTabla.MaximoFilas}. Acote las fechas"));
            }

            var filas = (await query.ToListAsync()).Select(fila).ToList();
            return await Archivo(tipo, titulo, filtro, formato, columnas, filas, null);
        }

        private async Task<IActionResult> Archivo(string tipo, string titulo, ReporteFiltroDto filtro, string formato,
            Columna[] columnas, List<IReadOnlyList<object?>> filas, IReadOnlyList<(string, string)>? resumen)
        {
            var textoFiltros = TextoFiltros(filtro);
            var pie = new PieDocumento(titulo, textoFiltros, _currentUser.Nombre, DateTime.UtcNow);
            var bytes = formato == ReporteFormatos.Excel
                ? ExportadorTabla.Excel(titulo, columnas, filas, pie, resumen)
                : ExportadorTabla.Pdf(titulo, columnas, filas, pie, resumen);

            await _context.RegistrarEventoAsync(AuditoriaAcciones.Exportacion, _currentUser.IdUsuario,
                new { Reporte = tipo, Formato = formato, Filtros = textoFiltros, Filas = filas.Count });

            return File(bytes, formato == ReporteFormatos.Excel ? ExportadorTabla.TipoExcel : ExportadorTabla.TipoPdf,
                $"ECAR_{tipo}_{filtro.Desde:yyyyMMdd}_{filtro.Hasta:yyyyMMdd}.{formato}");
        }

        /// <summary>Fechas obligatorias, en orden y con un año como máximo (SRS §7, §3.9).</summary>
        private static string? Validar(ReporteFiltroDto filtro, string formato)
        {
            if (formato is not (ReporteFormatos.Json or ReporteFormatos.Pdf or ReporteFormatos.Excel))
                return "El formato debe ser json, pdf o xlsx";
            if (!filtro.Desde.HasValue || !filtro.Hasta.HasValue)
                return "Las fechas «Desde» y «Hasta» son obligatorias";
            if (filtro.Desde.Value.Date > filtro.Hasta.Value.Date)
                return "La fecha «Desde» no puede ser posterior a «Hasta»";
            if (filtro.Hasta.Value.Date > filtro.Desde.Value.Date.AddYears(1))
                return "El rango no puede superar un año";
            return null;
        }

        private static PagedResultDto<T> Paginar<T>(List<T> filas, int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);
            return new PagedResultDto<T>
            {
                Data = filas.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = filas.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        private static string TextoFiltros(ReporteFiltroDto f)
        {
            var partes = new List<string> { $"desde {f.Desde:dd/MM/yyyy}", $"hasta {f.Hasta:dd/MM/yyyy}" };
            if (!string.IsNullOrWhiteSpace(f.Planta)) partes.Add($"planta {f.Planta}");
            if (!string.IsNullOrWhiteSpace(f.Area)) partes.Add($"área {f.Area}");
            if (f.IdEquipo.HasValue) partes.Add($"equipo {f.IdEquipo}");
            if (f.IdUsuario.HasValue) partes.Add($"técnico {f.IdUsuario}");
            if (!string.IsNullOrWhiteSpace(f.Estado)) partes.Add($"estado {f.Estado}");
            if (!string.IsNullOrWhiteSpace(f.Criticidad)) partes.Add($"criticidad {f.Criticidad}");
            return string.Join(", ", partes);
        }

        private static string? Resultado(string? resultado) =>
            resultado == InspeccionResultados.ConNovedad ? "Con novedad" : resultado;

        private static string Estado(string estado) => estado == HallazgoEstados.EnProceso ? "En proceso" : estado;
    }
}
