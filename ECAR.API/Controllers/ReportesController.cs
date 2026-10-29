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
                [new("Inspección"), new("Sede"), new("Código"), new("Activo fijo"), new("Equipo"), new("Marca / modelo"),
                 new("Área"), new("Fecha"), new("Resultado"), new("Observaciones"), new("Inspector")],
                f => [f.IdInspeccion, f.Sede, f.CodigoInterno, f.ActivoFijo, f.NombreEquipo, $"{f.Marca} {f.Modelo}".Trim(),
                      f.Area, HoraColombia.DesdeUtc(f.FechaInspeccion), Resultado(f.Resultado), f.Observaciones, f.Inspector]);

        [HttpGet("historial-area")]
        public Task<IActionResult> GetHistorialArea([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.HistorialArea, "Historial por área", filtro, formato, page, pageSize,
                () => _reporteService.HistorialArea(filtro),
                [new("Inspección"), new("Sede"), new("Planta"), new("Área"), new("Equipo"), new("Fecha"),
                 new("Resultado"), new("Responsable")],
                f => [f.IdInspeccion, f.Sede, f.Planta, f.Area, f.Equipo, HoraColombia.DesdeUtc(f.Fecha), Resultado(f.Resultado), f.Responsable]);

        [HttpGet("historial-usuario")]
        public Task<IActionResult> GetHistorialUsuario([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.HistorialUsuario, "Historial por usuario", filtro, formato, page, pageSize,
                () => _reporteService.HistorialUsuario(filtro),
                [new("Inspección"), new("Sede"), new("Usuario"), new("Equipo"), new("Fecha"), new("Resultado"),
                 new("Observaciones")],
                f => [f.IdInspeccion, f.Sede, f.Usuario, f.Equipo, HoraColombia.DesdeUtc(f.FechaInspeccion), Resultado(f.Resultado), f.Observaciones]);

        [HttpGet("equipos-novedades")]
        public Task<IActionResult> GetEquiposNovedades([FromQuery] ReporteFiltroDto filtro,
            [FromQuery] string formato = ReporteFormatos.Json, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            Tabular(ReporteTipos.EquiposNovedades, "Equipos con novedades", filtro, formato, page, pageSize,
                () => _reporteService.EquiposNovedades(filtro),
                [new("Novedad"), new("Sede"), new("Equipo"), new("Área"), new("Descripción"), new("Criticidad"),
                 new("Fecha"), new("Responsable"), new("Estado")],
                f => [f.IdNovedad, f.Sede, f.Equipo, f.Area, f.Novedad, f.Criticidad, HoraColombia.DesdeUtc(f.Fecha),
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
                ("Satisfactorias (sin novedades)", resultado.Satisfactorias.ToString()),
                ("Con novedad", resultado.ConNovedad.ToString())
            };

            return await Archivo(ReporteTipos.InspeccionesFechas, "Inspecciones por fechas", filtro, formato,
                [new("Planta"), new("Área"), new("Inspecciones"), new("Satisfactorias"), new("Con novedad")],
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
            var textoFiltros = await TextoFiltrosAsync(filtro);
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

        /// <summary>
        /// Los filtros para el pie del archivo y la auditoría, con el código del equipo y el nombre del
        /// usuario en lugar de sus id: «desde 01/10/2026, hasta 29/10/2026, plantas Norte y Sur, equipos BAL-01 y BOM-01».
        /// </summary>
        private async Task<string> TextoFiltrosAsync(ReporteFiltroDto f)
        {
            var partes = new List<string> { $"desde {f.Desde:dd/MM/yyyy}", $"hasta {f.Hasta:dd/MM/yyyy}" };
            Agregar(partes, "sede", "sedes", ReporteService.Valores(f.Sedes));
            Agregar(partes, "planta", "plantas", ReporteService.Valores(f.Plantas));
            Agregar(partes, "área", "áreas", ReporteService.Valores(f.Areas));

            var idEquipos = f.IdEquipos.Distinct().ToList();
            if (idEquipos.Count > 0)
            {
                var codigos = await _context.Equipos.AsNoTracking().Where(e => idEquipos.Contains(e.IdEquipo))
                    .OrderBy(e => e.CodigoInterno).Select(e => e.CodigoInterno).ToListAsync();
                Agregar(partes, "equipo", "equipos", codigos);
            }

            var idUsuarios = f.IdUsuarios.Distinct().ToList();
            if (idUsuarios.Count > 0)
            {
                var nombres = await _context.Usuarios.AsNoTracking().Where(u => idUsuarios.Contains(u.IdUsuario))
                    .OrderBy(u => u.Nombre).Select(u => u.Nombre).ToListAsync();
                Agregar(partes, "usuario", "usuarios", nombres);
            }

            Agregar(partes, "estado", "estados", ReporteService.Valores(f.Estados));
            Agregar(partes, "criticidad", "criticidades", ReporteService.Valores(f.Criticidades));
            return string.Join(", ", partes);
        }

        private static void Agregar(List<string> partes, string singular, string plural, IReadOnlyList<string> valores)
        {
            if (valores.Count == 1)
                partes.Add($"{singular} {valores[0]}");
            else if (valores.Count > 1)
                partes.Add($"{plural} {string.Join(", ", valores.Take(valores.Count - 1))} y {valores[^1]}");
        }

        private static string? Resultado(string? resultado) =>
            resultado == InspeccionResultados.ConNovedad ? "Con novedad" : resultado;
    }
}
