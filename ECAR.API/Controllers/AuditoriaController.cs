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

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RolesSistema.Supervisan)]
public class AuditoriaController : ControllerBase
{
    private readonly ECARDbContext _context;

    public AuditoriaController(ECARDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET api/auditoria (endpoint 1). Filtros opcionales y combinables (<see cref="AuditoriaFiltroDto"/>):
    /// texto libre, tabla, registro, usuario, acción y rango de días. "desde" y "hasta" son días
    /// locales completos que se convierten a UTC, porque FechaHora se guarda en UTC.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<AuditoriaDto>>>> GetAuditoria(
        [FromQuery] AuditoriaFiltroDto filtro, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (ValidarFiltro(filtro) is { } error)
        {
            return BadRequest(ApiResponse<PagedResultDto<AuditoriaDto>>.ErrorResponse(error));
        }

        var query = Filtrar(filtro);

        var totalCount = await query.CountAsync();

        var registros = await query
            .OrderByDescending(a => a.FechaHora)
            .ThenByDescending(a => a.IdAuditoria)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProyeccionDto)
            .ToListAsync();

        var pagedResult = new PagedResultDto<AuditoriaDto>
        {
            Data = registros,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<AuditoriaDto>>.SuccessResponse(pagedResult));
    }

    /// <summary>
    /// GET api/auditoria/exportar (endpoint 21, Parte 11 §11.10(e): la auditoría disponible "para
    /// revisión y copia"). Los mismos filtros del endpoint 1, en Excel (una fila por cambio, con
    /// todos los campos) o PDF. Cada exportación queda en la auditoría con los filtros usados.
    /// </summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(
        [FromQuery] AuditoriaFiltroDto filtro, [FromServices] ICurrentUser currentUser,
        [FromQuery] string formato = ReporteFormatos.Excel)
    {
        if (formato is not (ReporteFormatos.Excel or ReporteFormatos.Pdf))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse("El formato debe ser xlsx o pdf"));
        }

        if (ValidarFiltro(filtro) is { } error)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(error));
        }

        var query = Filtrar(filtro);
        var total = await query.CountAsync();
        if (total > ExportadorTabla.MaximoFilas)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                $"Hay {total} filas; el máximo por archivo es {ExportadorTabla.MaximoFilas}. Acote las fechas o los filtros"));
        }

        var filas = await query.OrderBy(a => a.IdAuditoria).ToListAsync();
        var filtros = TextoFiltros(filtro);
        var pie = new PieDocumento("Auditoría", filtros, currentUser.Nombre, DateTime.UtcNow);

        var archivo = formato == ReporteFormatos.Excel
            ? ExportadorTabla.Excel("Auditoría", ColumnasExcel, filas.Select(a => (IReadOnlyList<object?>)
            [
                a.IdAuditoria, HoraColombia.DesdeUtc(a.FechaHora), a.Tabla, a.RegistroId, a.Accion, a.Usuario,
                a.IdUsuario, a.Motivo, a.ValorAnterior, a.ValorNuevo, a.DireccionIp, a.AgenteUsuario, a.HashAnterior, a.Hash
            ]), pie)
            : ExportadorTabla.Pdf("Auditoría", ColumnasPdf, filas.Select(a => (IReadOnlyList<object?>)
            [
                a.IdAuditoria, HoraColombia.DesdeUtc(a.FechaHora), a.Tabla, a.RegistroId, a.Accion, a.Usuario,
                a.Motivo, Resumir(a.ValorAnterior), Resumir(a.ValorNuevo), a.DireccionIp
            ]), pie);

        await _context.RegistrarEventoAsync(AuditoriaAcciones.Exportacion, currentUser.IdUsuario,
            new { Documento = "Auditoría", Formato = formato, Filtros = filtros, Filas = total });

        var nombre = $"ECAR_auditoria_{filtro.Desde:yyyyMMdd}_{filtro.Hasta:yyyyMMdd}.{formato}";
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

    private static string TextoFiltros(AuditoriaFiltroDto filtro)
    {
        var partes = new List<string>();
        if (filtro.Desde.HasValue) partes.Add($"desde {filtro.Desde:dd/MM/yyyy}");
        if (filtro.Hasta.HasValue) partes.Add($"hasta {filtro.Hasta:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(filtro.Tabla)) partes.Add($"tabla {filtro.Tabla}");
        if (filtro.RegistroId.HasValue) partes.Add($"registro {filtro.RegistroId}");
        if (filtro.IdUsuario.HasValue) partes.Add($"usuario {filtro.IdUsuario}");
        if (!string.IsNullOrWhiteSpace(filtro.Accion)) partes.Add($"acción {filtro.Accion}");
        if (!string.IsNullOrWhiteSpace(filtro.Search)) partes.Add($"texto «{filtro.Search.Trim()}»");
        return partes.Count == 0 ? "ninguno" : string.Join(", ", partes);
    }

    /// <summary>Las reglas de los filtros del endpoint 1, compartidas con la exportación (21).</summary>
    private static string? ValidarFiltro(AuditoriaFiltroDto filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Tabla) && !AuditoriaTablas.Todas.Contains(filtro.Tabla))
        {
            return $"La tabla debe ser una de: {string.Join(", ", AuditoriaTablas.Todas)}";
        }

        if (!string.IsNullOrWhiteSpace(filtro.Accion) && !AuditoriaAcciones.Todas.Contains(filtro.Accion))
        {
            return $"La acción debe ser una de: {string.Join(", ", AuditoriaAcciones.Todas)}";
        }

        if (filtro.Desde.HasValue && filtro.Hasta.HasValue && filtro.Desde.Value.Date > filtro.Hasta.Value.Date)
        {
            return "La fecha inicial no puede ser posterior a la fecha final";
        }

        return null;
    }

    private IQueryable<FilaAuditoria> Filtrar(AuditoriaFiltroDto filtro)
    {
        var query = _context.Auditoria.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(filtro.Search))
        {
            var search = filtro.Search;
            query = query.Where(a =>
                a.Tabla.Contains(search) ||
                a.Accion.Contains(search) ||
                a.Usuario.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tabla))
        {
            query = query.Where(a => a.Tabla == filtro.Tabla);
        }

        if (filtro.RegistroId.HasValue)
        {
            query = query.Where(a => a.RegistroId == filtro.RegistroId.Value);
        }

        if (filtro.IdUsuario.HasValue)
        {
            query = query.Where(a => a.IdUsuario == filtro.IdUsuario.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Accion))
        {
            query = query.Where(a => a.Accion == filtro.Accion);
        }

        if (filtro.Desde.HasValue)
        {
            var desdeUtc = InicioDelDiaEnUtc(filtro.Desde.Value);
            query = query.Where(a => a.FechaHora >= desdeUtc);
        }

        if (filtro.Hasta.HasValue)
        {
            var hastaUtcExclusivo = InicioDelDiaEnUtc(filtro.Hasta.Value.AddDays(1));
            query = query.Where(a => a.FechaHora < hastaUtcExclusivo);
        }

        return query;
    }

    // {id:long}: sin la restricción, GET api/auditoria/verificar o /exportar (Fase 4) entraban
    // aquí con "verificar" como id y respondían 400 "datos inválidos" en lugar de 404.
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AuditoriaDto>>> GetAuditoriaRegistro(long id)
    {
        var registro = await _context.Auditoria
            .AsNoTracking()
            .Where(a => a.IdAuditoria == id)
            .Select(ProyeccionDto)
            .FirstOrDefaultAsync();

        if (registro == null)
        {
            return NotFound(ApiResponse<AuditoriaDto>.ErrorResponse("Registro de auditoría no encontrado"));
        }

        return Ok(ApiResponse<AuditoriaDto>.SuccessResponse(registro));
    }

    /// <summary>
    /// GET api/auditoria/registro/{tabla}/{registroId} (endpoint 2): el historial completo de un
    /// registro —por ejemplo Inspecciones/12—, en orden cronológico (de la creación al último
    /// cambio). Sin paginar: es la lista que espera el cliente y el historial de un registro es
    /// corto. Un registro sin filas de auditoría devuelve la lista vacía.
    /// </summary>
    [HttpGet("registro/{tabla}/{registroId:long}")]
    public async Task<ActionResult<ApiResponse<List<AuditoriaDto>>>> GetHistorialRegistro(string tabla, long registroId)
    {
        if (!AuditoriaTablas.Todas.Contains(tabla))
        {
            return BadRequest(ApiResponse<List<AuditoriaDto>>.ErrorResponse(
                $"La tabla debe ser una de: {string.Join(", ", AuditoriaTablas.Todas)}"));
        }

        var historial = await _context.Auditoria
            .AsNoTracking()
            .Where(a => a.Tabla == tabla && a.RegistroId == registroId)
            .OrderBy(a => a.FechaHora)
            .ThenBy(a => a.IdAuditoria)
            .Select(ProyeccionDto)
            .ToListAsync();

        return Ok(ApiResponse<List<AuditoriaDto>>.SuccessResponse(historial));
    }

    /// <summary>Filas que se leen de la base por cada lote de la verificación.</summary>
    public const int TamanoLoteVerificacion = 1000;

    /// <summary>
    /// GET api/auditoria/verificar (endpoint 3): recorre la cadena de hashes y dice si alguna fila
    /// fue alterada o borrada, y cuál es la primera inconsistente. Sin fechas revisa la tabla
    /// entera; con "desde"/"hasta" (días locales completos) revisa ese tramo. Una cadena rota no
    /// es un error de la petición: responde 200 con Valida = false. Cada verificación queda a su
    /// vez en la auditoría como evento VerificacionIntegridad.
    /// </summary>
    /// <remarks>
    /// Truncados: borrar las últimas filas no rompe la cadena. Por eso una verificación válida
    /// devuelve UltimaFila y UltimoHash, que el auditor guarda fuera de la base; si en la siguiente
    /// los envía como anclaFila y anclaHash y esa fila ya no está o cambió, la verificación falla
    /// con PrimeraFilaRota = anclaFila. Sin ancla el comportamiento es el de siempre.
    /// </remarks>
    [HttpGet("verificar")]
    public async Task<ActionResult<ApiResponse<VerificacionAuditoriaDto>>> VerificarCadena(
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null,
        [FromQuery] long? anclaFila = null, [FromQuery] string? anclaHash = null)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
        {
            return BadRequest(ApiResponse<VerificacionAuditoriaDto>.ErrorResponse(
                "La fecha inicial no puede ser posterior a la fecha final"));
        }

        var conAncla = anclaFila.HasValue || !string.IsNullOrWhiteSpace(anclaHash);
        if (conAncla && (!anclaFila.HasValue || !EsHashValido(anclaHash)))
        {
            return BadRequest(ApiResponse<VerificacionAuditoriaDto>.ErrorResponse(
                "El ancla necesita anclaFila y anclaHash (64 caracteres hexadecimales), los dos juntos"));
        }

        var resultado = await CadenaAuditoria.VerificarTablaAsync(_context,
            desde.HasValue ? InicioDelDiaEnUtc(desde.Value) : null,
            hasta.HasValue ? InicioDelDiaEnUtc(hasta.Value.AddDays(1)) : null,
            TamanoLoteVerificacion);

        // La cadena se revisa primero: si está rota, esa es la primera inconsistencia. Si está
        // íntegra, el ancla dice si además le faltan filas por el final o se reescribió.
        bool? anclaIntacta = null;
        if (conAncla && resultado.Valida)
        {
            anclaIntacta = await CadenaAuditoria.AnclaIntactaAsync(_context, anclaFila!.Value, anclaHash!);
        }

        var valida = resultado.Valida && anclaIntacta != false;
        var verificacion = new VerificacionAuditoriaDto
        {
            Valida = valida,
            FilasRevisadas = resultado.FilasRevisadas,
            PrimeraFilaRota = anclaIntacta == false ? anclaFila : resultado.PrimeraFilaRota,
            VerificadoEn = DateTime.UtcNow,
            UltimaFila = valida ? resultado.UltimaFila : null,
            UltimoHash = valida && resultado.UltimaFila.HasValue ? resultado.UltimoHash : null
        };

        // Después de verificar: la fila del evento se encadena al final y no entra en el recorrido.
        // El ancla queda también aquí como rastro, pero el control lo da la copia que se guarda fuera.
        await _context.RegistrarEventoAsync(AuditoriaAcciones.VerificacionIntegridad, detalle: new
        {
            verificacion.Valida,
            verificacion.FilasRevisadas,
            verificacion.PrimeraFilaRota,
            verificacion.UltimaFila,
            verificacion.UltimoHash,
            AnclaFila = anclaFila,
            AnclaIntacta = anclaIntacta,
            Desde = desde?.Date,
            Hasta = hasta?.Date
        });

        var mensaje = (resultado.Valida, anclaIntacta) switch
        {
            (false, _) => $"La cadena de auditoría está rota: la primera fila inconsistente es la {verificacion.PrimeraFilaRota}",
            (true, false) => $"La fila ancla {anclaFila} ya no está en la auditoría o cambió: la tabla fue truncada o alterada",
            (true, true) => "La cadena de auditoría está íntegra y conserva la fila ancla",
            _ => "La cadena de auditoría está íntegra"
        };

        return Ok(ApiResponse<VerificacionAuditoriaDto>.SuccessResponse(verificacion, mensaje));
    }

    private static bool EsHashValido(string? hash) =>
        hash?.Trim() is { Length: 64 } limpio && limpio.All(Uri.IsHexDigit);

    /// <summary>
    /// Medianoche de ese día en hora de Colombia, en UTC. No depende de la zona del servidor: en
    /// producción puede estar en UTC y los días se correrían cinco horas.
    /// </summary>
    private static DateTime InicioDelDiaEnUtc(DateTime dia) =>
        DateTime.SpecifyKind(dia.Date, DateTimeKind.Utc) - HoraColombia.Desfase;

    /// <summary>Todos los campos de la fila, también los de la Fase 4 (usuario, motivo, IP, agente y hash).</summary>
    private static readonly Expression<Func<FilaAuditoria, AuditoriaDto>> ProyeccionDto =
        a => new AuditoriaDto
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
