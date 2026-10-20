using ECAR.API.Exceptions;
using ECAR.API.Services;
using ECAR.API.Services.Notificaciones;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

/// <summary>
/// Novedades: una por cada respuesta "No" de una inspección firmada. Las crea el servidor al
/// firmar (InspeccionesController) y aquí solo se consultan, se resuelven con firma electrónica o,
/// si se registraron por error, las anula el Administrador. No hay alta manual, edición ni borrado:
/// POST api/novedades y DELETE api/novedades/{id} responden 405.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class NovedadesController : ControllerBase
{
    public const string MensajeNoPendiente = "La novedad ya está resuelta o anulada y no admite cambios";
    public const string MensajeSolucion = "Describa qué se hizo para resolver la novedad (entre 10 y 2000 caracteres)";
    public const string MensajePasswordRequerida = "Debe ingresar su contraseña para firmar";
    public const string MensajePasswordIncorrecta = "La contraseña es incorrecta; la novedad no se resolvió";
    public const string MensajeMotivoRequerido = "El motivo es requerido y debe tener entre 10 y 500 caracteres";
    public const string MensajeConcurrencia = "La novedad cambió mientras se procesaba la solicitud; vuelva a cargarla";
    public const string MensajeSinResolver = "Solo se verifica una novedad resuelta";

    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IMotivoCambio _motivoCambio;
    private readonly INotificadorNovedades? _notificador;

    /// <param name="notificador">Opcional solo para las pruebas; en el API lo inyecta el contenedor.</param>
    public NovedadesController(ECARDbContext context, ICurrentUser currentUser, IMotivoCambio motivoCambio,
        INotificadorNovedades? notificador = null)
    {
        _context = context;
        _currentUser = currentUser;
        _motivoCambio = motivoCambio;
        _notificador = notificador;
    }

    /// <summary>
    /// GET api/novedades. Filtros opcionales y combinables: estado, criticidad, equipo, inspección
    /// y días de registro (desde y hasta incluidos, en hora de Colombia). Los tres roles ven todas:
    /// cualquier Técnico puede resolver una novedad, la haya reportado o no.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<NovedadDto>>>> GetNovedades(
        [FromQuery] NovedadFiltroDto filtro, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!string.IsNullOrWhiteSpace(filtro.Estado) && !NovedadEstados.EsValido(filtro.Estado))
        {
            return BadRequest(ApiResponse<PagedResultDto<NovedadDto>>.ErrorResponse(
                $"El estado debe ser uno de: {string.Join(", ", NovedadEstados.Todos)}"));
        }

        if (filtro.Desde.HasValue && filtro.Hasta.HasValue && filtro.Desde.Value.Date > filtro.Hasta.Value.Date)
        {
            return BadRequest(ApiResponse<PagedResultDto<NovedadDto>>.ErrorResponse(
                "La fecha inicial no puede ser posterior a la fecha final"));
        }

        var query = ConDetalle().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            query = query.Where(n => n.Estado == filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Criticidad))
        {
            var criticidad = filtro.Criticidad.Trim();
            query = query.Where(n => n.Criticidad == criticidad);
        }

        if (filtro.IdEquipo.HasValue)
        {
            query = query.Where(n => n.Inspeccion.IdEquipo == filtro.IdEquipo.Value);
        }

        if (filtro.IdInspeccion.HasValue)
        {
            query = query.Where(n => n.IdInspeccion == filtro.IdInspeccion.Value);
        }

        var (desde, hasta) = HoraColombia.RangoUtc(filtro.Desde, filtro.Hasta);
        if (desde.HasValue)
        {
            query = query.Where(n => n.FechaRegistro >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(n => n.FechaRegistro < hasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var search = filtro.Search.Trim();
            query = query.Where(n =>
                n.Descripcion.Contains(search) ||
                n.Inspeccion.Equipo.NombreEquipo.Contains(search) ||
                n.Inspeccion.Equipo.CodigoInterno.Contains(search) ||
                (n.Criticidad != null && n.Criticidad.Contains(search)));
        }

        var totalCount = await query.CountAsync();
        var novedades = await query
            .OrderByDescending(n => n.FechaRegistro)
            .ThenBy(n => n.IdNovedad)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(ApiResponse<PagedResultDto<NovedadDto>>.SuccessResponse(new PagedResultDto<NovedadDto>
        {
            Data = novedades.Select(n => Completar(new NovedadDto(), n)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }));
    }

    /// <summary>GET api/novedades/{id}: la novedad, su inspección y la resolución firmada.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<NovedadDetalleDto>>> GetNovedad(long id)
    {
        var novedad = await ConDetalle()
            .AsNoTracking()
            .Include(n => n.Respuesta)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Checklist)
            .Include(n => n.UsuarioAnulacion)
            .FirstOrDefaultAsync(n => n.IdNovedad == id);

        if (novedad == null)
        {
            return NotFound(ApiResponse<NovedadDetalleDto>.ErrorResponse("Novedad no encontrada"));
        }

        return Ok(ApiResponse<NovedadDetalleDto>.SuccessResponse(MapToDetalleDto(novedad)));
    }

    /// <summary>
    /// POST api/novedades/{id}/resolver: firma la resolución, como se firma una inspección (Parte 11
    /// §11.50, §11.70 y §11.200(a)(1)). La firma un Técnico o un Administrador, aunque no haya hecho
    /// la inspección, con su contraseña. Si era la última pendiente de la inspección, esta pasa a
    /// Cerrada con resultado "ConNovedad" en la misma transacción. Orden: 404, 409, 400 (datos), 400
    /// (contraseña, sin Errors para que el diálogo de firma siga abierto) o 423.
    /// </summary>
    [HttpPost("{id:long}/resolver")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<NovedadDto>>> ResolverNovedad(
        long id,
        ResolverNovedadDto resolverDto,
        [FromServices] IAuthService authService,
        [FromServices] VerificadorIntegridad verificador)
    {
        var novedad = await _context.Novedades
            .Include(n => n.Inspeccion)
            .FirstOrDefaultAsync(n => n.IdNovedad == id);
        if (novedad == null)
        {
            return NotFound(ApiResponse<NovedadDto>.ErrorResponse("Novedad no encontrada"));
        }

        if (novedad.Estado != NovedadEstados.Pendiente)
        {
            return Conflict(ApiResponse<NovedadDto>.ErrorResponse(MensajeNoPendiente));
        }

        var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsuario == _currentUser.IdUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return BadRequest(ApiResponse<NovedadDto>.ErrorResponse(
                "El usuario autenticado no existe o no está habilitado en ECAR"));
        }

        var errores = new List<string>();
        var solucion = resolverDto.Solucion?.Trim();
        if (solucion == null || solucion.Length is < ResolverNovedadDto.LongitudMinimaSolucion
                or > ResolverNovedadDto.LongitudMaximaSolucion)
        {
            errores.Add(MensajeSolucion);
        }

        if (!FirmaInspeccion.TryValidarPng(resolverDto.FirmaPngBase64, out var firmaPng, out var errorFirma))
        {
            errores.Add(errorFirma);
        }

        if (errores.Count > 0)
        {
            return BadRequest(ApiResponse<NovedadDto>.ErrorResponse("La novedad no puede resolverse: faltan datos", errores));
        }

        if (string.IsNullOrEmpty(resolverDto.Password))
        {
            return BadRequest(ApiResponse<NovedadDto>.ErrorResponse(MensajePasswordRequerida));
        }

        try
        {
            if (!await authService.VerificarPasswordAsync(_currentUser.IdUsuario, resolverDto.Password, AuthService.OperacionFirma))
            {
                return BadRequest(ApiResponse<NovedadDto>.ErrorResponse(MensajePasswordIncorrecta));
            }
        }
        catch (CuentaBloqueadaException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, ApiResponse<NovedadDto>.ErrorResponse(ex.Message));
        }

        return await CambiarEstadoAsync(novedad, NovedadEstados.Resuelta, () =>
        {
            novedad.Solucion = solucion;
            novedad.FechaResolucion = DateTime.UtcNow;
            novedad.IdUsuarioResolucion = usuario.IdUsuario;
            novedad.FirmaDigital = firmaPng;
            novedad.FirmaNombre = usuario.Nombre;
            novedad.FirmaSignificado = VerificadorIntegridad.SignificadoResolucion;
            novedad.FirmaAlgoritmo = VerificadorIntegridad.AlgoritmoNovedad;
            novedad.FirmaHash = verificador.CalcularSelloNovedad(novedad);
        }, motivo: null, "Novedad resuelta");
    }

    /// <summary>
    /// POST api/novedades/{id}/anular: solo el Administrador, con motivo, y solo una pendiente
    /// (registrada por error). Si era la última pendiente, la inspección se cierra igual que al resolver.
    /// </summary>
    [HttpPost("{id:long}/anular")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<NovedadDto>>> AnularNovedad(long id, MotivoDto motivoDto)
    {
        var novedad = await _context.Novedades
            .Include(n => n.Inspeccion)
            .FirstOrDefaultAsync(n => n.IdNovedad == id);
        if (novedad == null)
        {
            return NotFound(ApiResponse<NovedadDto>.ErrorResponse("Novedad no encontrada"));
        }

        if (novedad.Estado != NovedadEstados.Pendiente)
        {
            return Conflict(ApiResponse<NovedadDto>.ErrorResponse(MensajeNoPendiente));
        }

        var motivo = motivoDto.Motivo?.Trim();
        if (motivo == null || motivo.Length is < MotivoDto.LongitudMinima or > MotivoDto.LongitudMaxima)
        {
            return BadRequest(ApiResponse<NovedadDto>.ErrorResponse(MensajeMotivoRequerido));
        }

        return await CambiarEstadoAsync(novedad, NovedadEstados.Anulada, () =>
        {
            novedad.MotivoAnulacion = motivo;
            novedad.FechaAnulacion = DateTime.UtcNow;
            novedad.IdUsuarioAnulacion = _currentUser.IdUsuario;
        }, motivo, "Novedad anulada");
    }

    /// <summary>
    /// GET api/novedades/{id}/integridad (Parte 11 §11.70): recalcula el sello de la resolución y lo
    /// compara con el guardado. Solo Administrador y Auditor; cada verificación queda en la auditoría.
    /// </summary>
    [HttpGet("{id:long}/integridad")]
    [Authorize(Roles = "Administrador,Auditor")]
    public async Task<ActionResult<ApiResponse<IntegridadInspeccionDto>>> VerificarIntegridad(
        long id, [FromServices] VerificadorIntegridad verificador)
    {
        var novedad = await _context.Novedades
            .AsNoTracking()
            .Include(n => n.Inspeccion)
            .FirstOrDefaultAsync(n => n.IdNovedad == id);
        if (novedad == null)
        {
            return NotFound(ApiResponse<IntegridadInspeccionDto>.ErrorResponse("Novedad no encontrada"));
        }

        if (novedad.Estado != NovedadEstados.Resuelta)
        {
            return Conflict(ApiResponse<IntegridadInspeccionDto>.ErrorResponse(MensajeSinResolver));
        }

        var resultado = new IntegridadInspeccionDto
        {
            Integra = verificador.VerificarNovedad(novedad),
            Algoritmo = novedad.FirmaAlgoritmo ?? string.Empty,
            VerificadoEn = DateTime.UtcNow
        };

        await _context.RegistrarEventoAsync(AuditoriaAcciones.VerificacionIntegridad,
            detalle: new { IdNovedad = id, resultado.Integra, resultado.Algoritmo });

        return Ok(ApiResponse<IntegridadInspeccionDto>.SuccessResponse(resultado,
            resultado.Integra
                ? "La resolución no ha cambiado desde que se firmó"
                : "La resolución fue alterada después de firmarse"));
    }

    /// <summary>POST api/novedades/{id}/notificar: el Administrador vuelve a poner en cola el correo.</summary>
    [HttpPost("{id:long}/notificar")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<bool>>> Notificar(long id)
    {
        if (!await _context.Novedades.AnyAsync(n => n.IdNovedad == id))
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Novedad no encontrada"));
        }

        _notificador?.Encolar([id]);
        return Ok(ApiResponse<bool>.SuccessResponse(true,
            "El correo de la novedad se está enviando; el estado del envío aparece en el detalle en unos segundos"));
    }

    /// <summary>
    /// Cambia el estado de una novedad pendiente y, si era la última pendiente de su inspección,
    /// cierra la inspección: Cerrada con resultado ConNovedad. Todo en una transacción en la que la
    /// fila de la inspección se bloquea primero, para que dos novedades de la misma inspección
    /// resueltas a la vez no se vean pendientes entre sí y la dejen abierta para siempre.
    /// </summary>
    private async Task<ActionResult<ApiResponse<NovedadDto>>> CambiarEstadoAsync(Novedad novedad, string destino,
        Action aplicar, string? motivo, string mensaje)
    {
        var idInspeccion = novedad.IdInspeccion;
        await using var transaccion = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        bool inspeccionCerrada;
        try
        {
            if (transaccion != null)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM [Inspecciones] WITH (UPDLOCK, HOLDLOCK) WHERE [IdInspeccion] = {idInspeccion}");
                await _context.Entry(novedad.Inspeccion).ReloadAsync();
            }

            novedad.Estado = destino;
            aplicar();

            var quedanOtras = await _context.Novedades.AnyAsync(n =>
                n.IdInspeccion == idInspeccion && n.IdNovedad != novedad.IdNovedad && n.Estado == NovedadEstados.Pendiente);
            inspeccionCerrada = !quedanOtras && novedad.Inspeccion.Estado == InspeccionEstados.NovedadesPendientes;
            if (inspeccionCerrada)
            {
                novedad.Inspeccion.Estado = InspeccionEstados.Cerrada;
                novedad.Inspeccion.Resultado = InspeccionResultados.ConNovedad;
            }

            _motivoCambio.Establecer(motivo);
            await _context.SaveChangesAsync();
            if (transaccion != null)
            {
                await transaccion.CommitAsync();
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(ApiResponse<NovedadDto>.ErrorResponse(MensajeConcurrencia));
        }

        var actualizada = await ConDetalle().AsNoTracking().SingleAsync(n => n.IdNovedad == novedad.IdNovedad);
        var pendientes = await _context.Novedades.CountAsync(n =>
            n.IdInspeccion == idInspeccion && n.Estado == NovedadEstados.Pendiente);
        var detalle = inspeccionCerrada
            ? $"{mensaje}. La inspección #{idInspeccion} quedó cerrada"
            : pendientes > 0
                ? $"{mensaje}. La inspección #{idInspeccion} tiene {pendientes} novedad(es) pendiente(s)"
                : $"{mensaje} exitosamente";
        return Ok(ApiResponse<NovedadDto>.SuccessResponse(Completar(new NovedadDto(), actualizada), detalle));
    }

    /// <summary>Todo lo que muestra una fila del listado: equipo con su ubicación, pregunta y usuarios.</summary>
    private IQueryable<Novedad> ConDetalle() => _context.Novedades
        .Include(n => n.Inspeccion)
            .ThenInclude(i => i.Equipo)
                .ThenInclude(e => e.Ubicacion)
        .Include(n => n.Pregunta)
        .Include(n => n.UsuarioRegistro)
        .Include(n => n.UsuarioResolucion);

    private static NovedadDetalleDto MapToDetalleDto(Novedad n)
    {
        var dto = Completar(new NovedadDetalleDto(), n);
        dto.ObservacionRespuesta = n.Respuesta?.Observacion;
        dto.FechaInspeccion = n.Inspeccion.FechaInspeccion;
        dto.NombreChecklist = n.Inspeccion.Checklist == null
            ? null
            : $"{n.Inspeccion.Checklist.Nombre} v. {n.Inspeccion.Checklist.Version}";
        dto.EstadoInspeccion = n.Inspeccion.Estado;
        dto.Solucion = n.Solucion;
        dto.FirmaPngBase64 = n.FirmaDigital;
        dto.FirmaNombre = n.FirmaNombre;
        dto.FirmaSignificado = n.FirmaSignificado;
        dto.FirmaAlgoritmo = n.FirmaAlgoritmo;
        dto.FirmaHash = n.FirmaHash;
        dto.FechaAnulacion = n.FechaAnulacion;
        dto.NombreUsuarioAnulacion = n.UsuarioAnulacion?.Nombre;
        return dto;
    }

    /// <summary>Campos comunes al listado y al detalle; las navegaciones que no se cargaron quedan en null.</summary>
    private static T Completar<T>(T dto, Novedad n) where T : NovedadDto
    {
        var equipo = n.Inspeccion?.Equipo;

        dto.IdNovedad = n.IdNovedad;
        dto.IdInspeccion = n.IdInspeccion;
        dto.IdEquipo = n.Inspeccion?.IdEquipo ?? 0;
        dto.NombreEquipo = equipo?.NombreEquipo;
        dto.CodigoInterno = equipo?.CodigoInterno;
        dto.Area = equipo?.Ubicacion == null ? null : $"{equipo.Ubicacion.Planta} - {equipo.Ubicacion.Area}";
        dto.IdPregunta = n.IdPregunta;
        dto.Pregunta = n.Pregunta?.Pregunta;
        dto.Descripcion = n.Descripcion;
        dto.Criticidad = n.Criticidad;
        dto.Estado = n.Estado;
        dto.FechaRegistro = n.FechaRegistro;
        dto.IdUsuarioRegistro = n.IdUsuarioRegistro;
        dto.NombreInspector = n.UsuarioRegistro?.Nombre;
        dto.FechaResolucion = n.FechaResolucion;
        dto.NombreUsuarioResolucion = n.UsuarioResolucion?.Nombre;
        dto.FechaNotificacion = n.FechaNotificacion;
        dto.MotivoAnulacion = n.MotivoAnulacion;
        return dto;
    }
}
