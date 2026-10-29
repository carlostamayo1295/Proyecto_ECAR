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
    [Authorize(Roles = RolesSistema.Supervisan)]
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

        /// <summary>Máximo de usuarios por búsqueda: el filtro del cliente pide 20.</summary>
        public const int MaximoUsuariosBusqueda = 20;

        /// <summary>
        /// Búsqueda de usuarios para el filtro «Usuario» (B-04). GET api/usuarios es solo del
        /// Administrador; esta la usa también el Supervisor y devuelve solo id, nombre y correo.
        /// Incluye a los inactivos, porque sus inspecciones siguen en los reportes.
        /// </summary>
        [HttpGet("usuarios")]
        public async Task<ActionResult<ApiResponse<List<UsuarioBusquedaDto>>>> GetUsuarios([FromQuery] string? texto = null)
        {
            var query = _context.Usuarios.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(texto))
            {
                var term = texto.Trim();
                query = query.Where(u => u.Nombre.Contains(term) || u.Correo.Contains(term));
            }

            var usuarios = await query
                .OrderBy(u => u.Nombre)
                .Take(MaximoUsuariosBusqueda)
                .Select(u => new UsuarioBusquedaDto
                {
                    IdUsuario = u.IdUsuario,
                    Nombre = u.Nombre,
                    Correo = u.Correo
                })
                .ToListAsync();

            return Ok(ApiResponse<List<UsuarioBusquedaDto>>.SuccessResponse(usuarios));
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

        [HttpGet("historial-usuario")]
        public Task<IActionResult> GetHistorialUsuario([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.HistorialUsuario, "Historial por usuario", filtro, formato, page, pageSize,
                () => _reporteService.HistorialUsuario(filtro),
                [new("Inspección", 2), new("Usuario", 4.5), new("Equipo", 6), new("Fecha", 3), new("Resultado", 3),
                 new("Observaciones", 7.2)],
                f => [f.IdInspeccion, f.Usuario, f.Equipo, HoraColombia.DesdeUtc(f.FechaInspeccion), Resultado(f.Resultado), f.Observaciones]);

        [HttpGet("equipos-novedades")]
        public Task<IActionResult> GetEquiposNovedades([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.EquiposNovedades, "Equipos con novedades", filtro, formato, page, pageSize,
                () => _reporteService.EquiposNovedades(filtro),
                [new("Novedad", 1.6), new("Equipo", 4.5), new("Área", 3.2), new("Descripción", 7), new("Criticidad", 1.8),
                 new("Fecha", 2.4), new("Responsable", 3), new("Estado", 2.2)],
                f => [f.IdNovedad, f.Equipo, f.Area, f.Novedad, f.Criticidad, HoraColombia.DesdeUtc(f.Fecha),
                      f.Responsable ?? "Sin resolver", f.Estado]);

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

        private static string TextoFiltros(ReporteFiltroDto f)
        {
            var partes = new List<string> { $"desde {f.Desde:dd/MM/yyyy}", $"hasta {f.Hasta:dd/MM/yyyy}" };
            if (!string.IsNullOrWhiteSpace(f.Planta)) partes.Add($"planta {f.Planta}");
            if (!string.IsNullOrWhiteSpace(f.Area)) partes.Add($"área {f.Area}");
            if (f.IdEquipo.HasValue) partes.Add($"equipo {f.IdEquipo}");
            if (f.IdUsuario.HasValue) partes.Add($"usuario {f.IdUsuario}");
            if (!string.IsNullOrWhiteSpace(f.Estado)) partes.Add($"estado {f.Estado}");
            if (!string.IsNullOrWhiteSpace(f.Criticidad)) partes.Add($"criticidad {f.Criticidad}");
            return string.Join(", ", partes);
        }

        private static string? Resultado(string? resultado) =>
            resultado == InspeccionResultados.ConNovedad ? "Con novedad" : resultado;
    }
}
