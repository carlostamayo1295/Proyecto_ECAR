using System.Linq.Expressions;
using ECAR.API.Services;
using ECAR.API.Services.Documentos;
using ECAR.Infrastructure.Data;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FilaAuditoria = ECAR.Infrastructure.Entities.Auditoria;

namespace ECAR.API.Controllers;

/// <summary>
/// Consulta, verificación y exportación de la auditoría (endpoints 1, 2, 3 y 21; Parte 11
/// §11.10(e): disponible "para revisión y copia"). La auditoría solo se lee aquí: la escribe
/// ECARDbContext y el trigger impide modificarla.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Auditor")]
public class AuditoriaController : ControllerBase
{
    /// <summary>La verificación recorre la tabla en lotes para no cargarla entera en memoria.</summary>
    private const int TamanoLote = 1000;

    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;

    public AuditoriaController(ECARDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    /// <summary>Endpoint 1: filtros por tabla, registro, usuario, acción y fechas, y búsqueda libre.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<AuditoriaDto>>>> GetAuditoria(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null,
        [FromQuery] string? tabla = null, [FromQuery] long? registroId = null, [FromQuery] long? idUsuario = null,
        [FromQuery] string? accion = null, [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = Filtrar(search, tabla, registroId, idUsuario, accion, desde, hasta);
        var totalCount = await query.CountAsync();
        var registros = await query
            .OrderByDescending(a => a.IdAuditoria)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Proyeccion)
            .ToListAsync();

        return Ok(ApiResponse<PagedResultDto<AuditoriaDto>>.SuccessResponse(new PagedResultDto<AuditoriaDto>
        {
            Data = registros,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }));
    }

    // {id:long}: sin la restricción, GET api/auditoria/verificar o /exportar entraban
    // aquí con "verificar" como id y respondían 400 "datos inválidos" en lugar de 404.
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AuditoriaDto>>> GetAuditoriaRegistro(long id)
    {
        var registro = await _context.Auditoria.AsNoTracking()
            .Where(a => a.IdAuditoria == id)
            .Select(Proyeccion)
            .FirstOrDefaultAsync();

        return registro == null
            ? NotFound(ApiResponse<AuditoriaDto>.ErrorResponse("Registro de auditoría no encontrado"))
            : Ok(ApiResponse<AuditoriaDto>.SuccessResponse(registro));
    }

    /// <summary>Endpoint 2: el historial completo de un registro, del más antiguo al más reciente.</summary>
    [HttpGet("registro/{tabla}/{registroId:long}")]
    public async Task<ActionResult<ApiResponse<List<AuditoriaDto>>>> GetHistorialRegistro(string tabla, long registroId)
    {
        var historial = await _context.Auditoria.AsNoTracking()
            .Where(a => a.Tabla == tabla && a.RegistroId == registroId)
            .OrderBy(a => a.IdAuditoria)
            .Select(Proyeccion)
            .ToListAsync();

        return Ok(ApiResponse<List<AuditoriaDto>>.SuccessResponse(historial));
    }

    /// <summary>
    /// Endpoint 3: recorre la cadena de hashes por IdAuditoria (toda o en un rango de fechas) y
    /// dice si alguna fila fue alterada o falta. Cada verificación queda a su vez en la auditoría.
    /// </summary>
    [HttpGet("verificar")]
    public async Task<ActionResult<ApiResponse<VerificacionAuditoriaDto>>> Verificar(
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        var (inicio, fin) = HoraColombia.RangoUtc(desde, hasta);
        var rango = _context.Auditoria.AsNoTracking().AsQueryable();
        if (inicio.HasValue)
            rango = rango.Where(a => a.FechaHora >= inicio.Value);
        if (fin.HasValue)
            rango = rango.Where(a => a.FechaHora < fin.Value);

        var primera = await rango.MinAsync(a => (long?)a.IdAuditoria);
        var ultima = await rango.MaxAsync(a => (long?)a.IdAuditoria);
        var resultado = new ResultadoCadena(true, 0, null, null);

        if (primera.HasValue && ultima.HasValue)
        {
            // La primera fila del rango debe colgar de la fila anterior, aunque esa quede fuera.
            var esperado = await _context.Auditoria.AsNoTracking()
                .Where(a => a.IdAuditoria < primera.Value)
                .OrderByDescending(a => a.IdAuditoria)
                .Select(a => a.Hash)
                .FirstOrDefaultAsync();

            var revisadas = 0;
            var desdeId = primera.Value;
            while (desdeId <= ultima.Value)
            {
                var lote = await _context.Auditoria.AsNoTracking()
                    .Where(a => a.IdAuditoria >= desdeId && a.IdAuditoria <= ultima.Value)
                    .OrderBy(a => a.IdAuditoria)
                    .Take(TamanoLote)
                    .ToListAsync();
                if (lote.Count == 0)
                {
                    break;
                }

                var parcial = CadenaAuditoria.Verificar(lote, esperado);
                revisadas += parcial.FilasRevisadas;
                if (!parcial.Valida)
                {
                    resultado = parcial with { FilasRevisadas = revisadas };
                    break;
                }

                esperado = parcial.UltimoHash;
                desdeId = lote[^1].IdAuditoria + 1;
                resultado = new ResultadoCadena(true, revisadas, null, esperado);
            }
        }

        var dto = new VerificacionAuditoriaDto
        {
            Valida = resultado.Valida,
            FilasRevisadas = resultado.FilasRevisadas,
            PrimeraFilaRota = resultado.PrimeraFilaRota,
            VerificadoEn = DateTime.UtcNow
        };
        await _context.RegistrarEventoAsync(AuditoriaAcciones.VerificacionIntegridad, _currentUser.IdUsuario,
            new { Objeto = "Auditoria", desde, hasta, dto.Valida, dto.FilasRevisadas, dto.PrimeraFilaRota });

        return Ok(ApiResponse<VerificacionAuditoriaDto>.SuccessResponse(dto, dto.Valida
            ? $"La cadena de auditoría está íntegra ({dto.FilasRevisadas} filas revisadas)"
            : $"La cadena de auditoría está rota a partir de la fila {dto.PrimeraFilaRota}"));
    }

    /// <summary>
    /// Endpoint 21: los mismos filtros del endpoint 1 en Excel (una fila por cambio) o PDF. La
    /// exportación queda en la auditoría con los filtros usados.
    /// </summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(
        [FromQuery] string formato = ReporteFormatos.Excel, [FromQuery] string? search = null,
        [FromQuery] string? tabla = null, [FromQuery] long? registroId = null, [FromQuery] long? idUsuario = null,
        [FromQuery] string? accion = null, [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        if (formato is not (ReporteFormatos.Excel or ReporteFormatos.Pdf))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse("El formato debe ser xlsx o pdf"));
        }

        var query = Filtrar(search, tabla, registroId, idUsuario, accion, desde, hasta);
        var total = await query.CountAsync();
        if (total > ExportadorTabla.MaximoFilas)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                $"Hay {total} filas; el máximo por archivo es {ExportadorTabla.MaximoFilas}. Acote las fechas o los filtros"));
        }

        var filas = await query.OrderBy(a => a.IdAuditoria).ToListAsync();
        var filtros = TextoFiltros(search, tabla, registroId, idUsuario, accion, desde, hasta);
        var pie = new PieDocumento("Auditoría", filtros, _currentUser.Nombre, DateTime.UtcNow);

        byte[] archivo;
        if (formato == ReporteFormatos.Excel)
        {
            archivo = ExportadorTabla.Excel("Auditoría", ColumnasExcel, filas.Select(a => (IReadOnlyList<object?>)
            [
                a.IdAuditoria, HoraColombia.DesdeUtc(a.FechaHora), a.Tabla, a.RegistroId, a.Accion, a.Usuario,
                a.IdUsuario, a.Motivo, a.ValorAnterior, a.ValorNuevo, a.DireccionIp, a.AgenteUsuario, a.HashAnterior, a.Hash
            ]), pie);
        }
        else
        {
            archivo = ExportadorTabla.Pdf("Auditoría", ColumnasPdf, filas.Select(a => (IReadOnlyList<object?>)
            [
                a.IdAuditoria, HoraColombia.DesdeUtc(a.FechaHora), a.Tabla, a.RegistroId, a.Accion, a.Usuario,
                a.Motivo, Resumir(a.ValorAnterior), Resumir(a.ValorNuevo), a.DireccionIp
            ]), pie);
        }

        await _context.RegistrarEventoAsync(AuditoriaAcciones.Exportacion, _currentUser.IdUsuario,
            new { Documento = "Auditoría", Formato = formato, Filtros = filtros, Filas = total });

        var nombre = $"ECAR_auditoria_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.{formato}";
        return File(archivo, formato == ReporteFormatos.Excel ? ExportadorTabla.TipoExcel : ExportadorTabla.TipoPdf, nombre);
    }

    private static readonly Columna[] ColumnasExcel =
    [
        new("Id", 0), new("Fecha y hora", 0), new("Tabla", 0), new("Registro", 0), new("Acción", 0),
        new("Usuario", 0), new("Id usuario", 0), new("Motivo", 0), new("Valor anterior", 0),
        new("Valor nuevo", 0), new("IP", 0), new("Agente", 0), new("Hash anterior", 0), new("Hash", 0)
    ];

    private static readonly Columna[] ColumnasPdf =
    [
        new("Id", 1.1), new("Fecha y hora", 2.3), new("Tabla", 2.3), new("Registro", 1.4), new("Acción", 2),
        new("Usuario", 3.4), new("Motivo", 3.4), new("Antes", 4), new("Después", 4), new("IP", 1.8)
    ];

    private static string? Resumir(string? json) =>
        json is { Length: > 300 } ? json[..300] + "…" : json;

    private IQueryable<FilaAuditoria> Filtrar(string? search, string? tabla, long? registroId, long? idUsuario,
        string? accion, DateTime? desde, DateTime? hasta)
    {
        var query = _context.Auditoria.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tabla))
            query = query.Where(a => a.Tabla == tabla);
        if (registroId.HasValue)
            query = query.Where(a => a.RegistroId == registroId.Value);
        if (idUsuario.HasValue)
            query = query.Where(a => a.IdUsuario == idUsuario.Value);
        if (!string.IsNullOrWhiteSpace(accion))
            query = query.Where(a => a.Accion == accion);

        var (inicio, fin) = HoraColombia.RangoUtc(desde, hasta);
        if (inicio.HasValue)
            query = query.Where(a => a.FechaHora >= inicio.Value);
        if (fin.HasValue)
            query = query.Where(a => a.FechaHora < fin.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var texto = search.Trim();
            query = query.Where(a =>
                a.Tabla.Contains(texto) ||
                a.Accion.Contains(texto) ||
                a.Usuario.Contains(texto) ||
                (a.Motivo != null && a.Motivo.Contains(texto)));
        }

        return query;
    }

    private static string TextoFiltros(string? search, string? tabla, long? registroId, long? idUsuario,
        string? accion, DateTime? desde, DateTime? hasta)
    {
        var partes = new List<string>();
        if (desde.HasValue) partes.Add($"desde {desde:dd/MM/yyyy}");
        if (hasta.HasValue) partes.Add($"hasta {hasta:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(tabla)) partes.Add($"tabla {tabla}");
        if (registroId.HasValue) partes.Add($"registro {registroId}");
        if (idUsuario.HasValue) partes.Add($"usuario {idUsuario}");
        if (!string.IsNullOrWhiteSpace(accion)) partes.Add($"acción {accion}");
        if (!string.IsNullOrWhiteSpace(search)) partes.Add($"texto «{search.Trim()}»");
        return partes.Count == 0 ? "ninguno" : string.Join(", ", partes);
    }

    private static readonly Expression<Func<FilaAuditoria, AuditoriaDto>> Proyeccion = a => new AuditoriaDto
    {
        IdAuditoria = a.IdAuditoria,
        Tabla = a.Tabla,
        RegistroId = a.RegistroId,
        Accion = a.Accion,
        ValorAnterior = a.ValorAnterior,
        ValorNuevo = a.ValorNuevo,
        Usuario = a.Usuario,
        FechaHora = a.FechaHora,
        IdUsuario = a.IdUsuario,
        Motivo = a.Motivo,
        DireccionIp = a.DireccionIp,
        AgenteUsuario = a.AgenteUsuario,
        Hash = a.Hash
    };
}
