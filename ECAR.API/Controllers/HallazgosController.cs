using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class HallazgosController : ControllerBase
{
    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IMotivoCambio _motivoCambio;

    public const string MensajeNoSeElimina = "Los hallazgos no se eliminan; se anulan con un motivo";
    public const string MensajeInspeccionAnulada = "La inspección está anulada y no admite hallazgos";
    public const string MensajeResponsableInvalido = "El responsable indicado no existe o no está activo";
    public const string MensajeFechaCompromisoPasada = "La fecha de compromiso no puede ser anterior a hoy";
    public const string MensajeEstadoFinal = "Un hallazgo cerrado o anulado no admite modificaciones";
    public const string MensajeEstadoPorPut = "El estado se cambia con POST api/hallazgos/{id}/estado";
    public const string MensajeAnularPorEstado = "Para anular un hallazgo use POST api/hallazgos/{id}/anular";
    public const string MensajeAccionCorrectivaRequerida = "La acción correctiva es obligatoria para cerrar el hallazgo";
    public const string MensajeMotivoRequerido = "El motivo es requerido y debe tener entre 10 y 500 caracteres";
    public const string MensajeConcurrencia = "El hallazgo cambió mientras se procesaba la solicitud; vuelva a cargarlo";

    public HallazgosController(ECARDbContext context, ICurrentUser currentUser, IMotivoCambio motivoCambio)
    {
        _context = context;
        _currentUser = currentUser;
        _motivoCambio = motivoCambio;
    }

    /// <summary>
    /// GET api/hallazgos (endpoint 4). Filtros opcionales y combinables: estado, criticidad,
    /// equipo, inspección y rango de fechas de registro (desde y hasta incluyen el día entero).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<HallazgoDto>>>> GetHallazgos(
        [FromQuery] HallazgoFiltroDto filtro, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        // Máximo 100 por página, como en el resto de listados: sin límite, pageSize=100000 devolvía la tabla entera.
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!string.IsNullOrWhiteSpace(filtro.Estado) && !HallazgoEstados.EsValido(filtro.Estado))
        {
            return BadRequest(ApiResponse<PagedResultDto<HallazgoDto>>.ErrorResponse(
                $"El estado debe ser uno de: {string.Join(", ", HallazgoEstados.Todos)}"));
        }

        if (filtro.Desde.HasValue && filtro.Hasta.HasValue && filtro.Desde.Value.Date > filtro.Hasta.Value.Date)
        {
            return BadRequest(ApiResponse<PagedResultDto<HallazgoDto>>.ErrorResponse(
                "La fecha inicial no puede ser posterior a la fecha final"));
        }

        var query = ConDetalle().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            query = query.Where(h => h.Estado == filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Criticidad))
        {
            var criticidad = filtro.Criticidad.Trim();
            query = query.Where(h => h.Criticidad == criticidad);
        }

        if (filtro.IdEquipo.HasValue)
        {
            query = query.Where(h => h.Inspeccion.IdEquipo == filtro.IdEquipo.Value);
        }

        if (filtro.IdInspeccion.HasValue)
        {
            query = query.Where(h => h.IdInspeccion == filtro.IdInspeccion.Value);
        }

        if (filtro.Desde.HasValue)
        {
            var desde = filtro.Desde.Value.Date;
            query = query.Where(h => h.FechaRegistro >= desde);
        }

        if (filtro.Hasta.HasValue)
        {
            // Hasta el final del día indicado: el cliente envía solo la fecha (yyyy-MM-dd).
            var hastaExclusivo = filtro.Hasta.Value.Date.AddDays(1);
            query = query.Where(h => h.FechaRegistro < hastaExclusivo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var search = filtro.Search.Trim();
            query = query.Where(h =>
                h.Descripcion.Contains(search) ||
                h.Inspeccion.Equipo.NombreEquipo.Contains(search) ||
                h.Inspeccion.Equipo.CodigoInterno.Contains(search) ||
                (h.Criticidad != null && h.Criticidad.Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var hallazgos = await query
            .OrderByDescending(h => h.FechaRegistro)
            .ThenByDescending(h => h.IdHallazgo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pagedResult = new PagedResultDto<HallazgoDto>
        {
            Data = hallazgos.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<HallazgoDto>>.SuccessResponse(pagedResult));
    }

    /// <summary>GET api/hallazgos/{id} (endpoint 5): el hallazgo y la novedad o inspección de la que salió.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<HallazgoDetalleDto>>> GetHallazgo(long id)
    {
        var hallazgo = await ConDetalle()
            .AsNoTracking()
            .Include(h => h.Inspeccion)
                .ThenInclude(i => i.Usuario)
            .Include(h => h.Pregunta)
            .Include(h => h.Respuesta)
            .FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDetalleDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        return Ok(ApiResponse<HallazgoDetalleDto>.SuccessResponse(MapToDetalleDto(hallazgo)));
    }

    /// <summary>
    /// Registro manual de un hallazgo sobre una inspección existente (en curso o cerrada; una
    /// anulada no admite hallazgos). Nace Abierto, con origen Manual y a nombre de quien lo
    /// registra. El responsable y la fecha de compromiso son opcionales aquí y se validan igual
    /// que en la asignación.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CreateHallazgo(CreateHallazgoDto createDto)
    {
        // [Required] ya lo rechaza en la validación del modelo; esto cubre a quien llame sin ella.
        if (string.IsNullOrWhiteSpace(createDto.Descripcion))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La descripción es requerida"));
        }

        var usuario = await _context.Usuarios.FindAsync(_currentUser.IdUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(
                "El usuario autenticado no existe o no está habilitado en ECAR"));
        }

        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .FirstOrDefaultAsync(i => i.IdInspeccion == createDto.IdInspeccion);

        if (inspeccion == null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La inspección indicada no existe"));
        }

        if (inspeccion.Estado == InspeccionEstados.Anulada)
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(MensajeInspeccionAnulada));
        }

        var errorAsignacion = await ValidarAsignacionAsync(createDto.IdUsuarioResponsable, createDto.FechaCompromiso);
        if (errorAsignacion != null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(errorAsignacion));
        }

        var hallazgo = new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Descripcion = createDto.Descripcion.Trim(),
            Criticidad = string.IsNullOrWhiteSpace(createDto.Criticidad) ? null : createDto.Criticidad.Trim(),
            Estado = HallazgoEstados.Abierto,
            FechaRegistro = DateTime.UtcNow,
            Origen = HallazgoOrigenes.Manual,
            IdUsuarioRegistro = usuario.IdUsuario,
            IdUsuarioResponsable = createDto.IdUsuarioResponsable,
            FechaCompromiso = createDto.FechaCompromiso
        };

        _context.Hallazgos.Add(hallazgo);
        await _context.SaveChangesAsync();

        var creado = await ConDetalle().AsNoTracking().SingleAsync(h => h.IdHallazgo == hallazgo.IdHallazgo);

        return CreatedAtAction(nameof(GetHallazgo), new { id = hallazgo.IdHallazgo },
            ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(creado), "Hallazgo registrado exitosamente"));
    }

    /// <summary>
    /// PUT api/hallazgos/{id} (endpoint 7). Edita la descripción y la criticidad y asigna el
    /// responsable y la fecha de compromiso: cada campo se cambia solo si viene en el cuerpo. Un
    /// hallazgo Cerrado o Anulado no admite modificaciones (409). El estado no se cambia aquí:
    /// si llega distinto del actual se rechaza (400); igual al actual se ignora, porque el
    /// formulario de edición lo envía siempre.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> UpdateHallazgo(long id, UpdateHallazgoDto updateDto)
    {
        var hallazgo = await _context.Hallazgos.FirstOrDefaultAsync(h => h.IdHallazgo == id);

        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Estado) && updateDto.Estado != hallazgo.Estado)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(MensajeEstadoPorPut));
        }

        var modificaAlgo = !string.IsNullOrWhiteSpace(updateDto.Descripcion)
            || updateDto.Criticidad != null
            || updateDto.IdUsuarioResponsable.HasValue
            || updateDto.FechaCompromiso.HasValue;
        if (modificaAlgo && TransicionesHallazgo.EsFinal(hallazgo.Estado))
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(MensajeEstadoFinal));
        }

        // La asignación se valida antes de tocar nada, para no dejar la edición a medias.
        if (updateDto.IdUsuarioResponsable.HasValue || updateDto.FechaCompromiso.HasValue)
        {
            var errorAsignacion = await ValidarAsignacionAsync(updateDto.IdUsuarioResponsable, updateDto.FechaCompromiso);
            if (errorAsignacion != null)
            {
                return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(errorAsignacion));
            }

            if (updateDto.IdUsuarioResponsable.HasValue)
                hallazgo.IdUsuarioResponsable = updateDto.IdUsuarioResponsable;

            if (updateDto.FechaCompromiso.HasValue)
                hallazgo.FechaCompromiso = updateDto.FechaCompromiso;
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Descripcion))
            hallazgo.Descripcion = updateDto.Descripcion;

        if (updateDto.Criticidad != null)
            hallazgo.Criticidad = updateDto.Criticidad;

        _motivoCambio.Establecer(updateDto.Motivo);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(MensajeConcurrencia));
        }

        // Se relee para que el nombre del responsable sea el del recién asignado.
        var actualizado = await ConDetalle().AsNoTracking().SingleAsync(h => h.IdHallazgo == id);

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(actualizado), "Hallazgo actualizado exitosamente"));
    }

    /// <summary>
    /// POST api/hallazgos/{id}/estado (endpoint 8): mueve el hallazgo según la matriz de
    /// <see cref="TransicionesHallazgo"/>. Cerrar exige acción correctiva y reabrir exige motivo.
    /// Para anular se usa POST {id}/anular.
    /// </summary>
    [HttpPost("{id:long}/estado")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CambiarEstado(long id, CambiarEstadoHallazgoDto cambioDto)
    {
        if (!HallazgoEstados.EsValido(cambioDto.Estado))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(
                $"El estado debe ser uno de: {string.Join(", ", HallazgoEstados.Todos)}"));
        }

        if (cambioDto.Estado == HallazgoEstados.Anulado)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(MensajeAnularPorEstado));
        }

        return await AplicarTransicionAsync(id, cambioDto.Estado, cambioDto.AccionCorrectiva, cambioDto.Motivo);
    }

    /// <summary>POST api/hallazgos/{id}/anular (endpoint 9): solo Administrador y siempre con motivo.</summary>
    [HttpPost("{id:long}/anular")]
    [Authorize(Roles = "Administrador,Técnico")]
    public Task<ActionResult<ApiResponse<HallazgoDto>>> AnularHallazgo(long id, MotivoDto motivoDto)
    {
        // El rol lo decide la matriz (403 para un Técnico), igual que en reabrir.
        return AplicarTransicionAsync(id, HallazgoEstados.Anulado, accionCorrectiva: null, motivoDto.Motivo);
    }

    /// <summary>
    /// Aplica una transición en este orden: 404 si no existe, 409 si la matriz no la permite,
    /// 403 si exige Administrador, 400 si falta la acción correctiva o el motivo. Guarda el
    /// motivo en la auditoría y responde 409 si otra petición cambió el hallazgo a la vez.
    /// </summary>
    private async Task<ActionResult<ApiResponse<HallazgoDto>>> AplicarTransicionAsync(
        long id, string destino, string? accionCorrectiva, string? motivo)
    {
        var hallazgo = await _context.Hallazgos.FirstOrDefaultAsync(h => h.IdHallazgo == id);
        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        var regla = TransicionesHallazgo.Buscar(hallazgo.Estado, destino);
        if (regla == null)
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(
                $"Un hallazgo en estado {hallazgo.Estado} no puede pasar a {destino}"));
        }

        if (regla.SoloAdministrador && !_currentUser.IsInRole("Administrador"))
        {
            return Forbid();
        }

        var accion = accionCorrectiva?.Trim();
        if (regla.ExigeAccionCorrectiva && string.IsNullOrEmpty(accion))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(MensajeAccionCorrectivaRequerida));
        }

        var motivoLimpio = motivo?.Trim();
        if (regla.ExigeMotivo
            && (motivoLimpio == null || motivoLimpio.Length is < MotivoDto.LongitudMinima or > MotivoDto.LongitudMaxima))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(MensajeMotivoRequerido));
        }

        var estadoAnterior = hallazgo.Estado;
        hallazgo.Estado = destino;
        switch (destino)
        {
            case HallazgoEstados.Cerrado:
                hallazgo.AccionCorrectiva = accion;
                hallazgo.FechaCierre = DateTime.UtcNow;
                hallazgo.IdUsuarioCierre = _currentUser.IdUsuario;
                break;
            case HallazgoEstados.Anulado:
                hallazgo.MotivoAnulacion = motivoLimpio;
                break;
            case HallazgoEstados.Abierto when estadoAnterior == HallazgoEstados.Cerrado:
                // Reabrir: la última acción correctiva se conserva; el historial queda en Auditoria.
                hallazgo.FechaCierre = null;
                hallazgo.IdUsuarioCierre = null;
                break;
        }

        _motivoCambio.Establecer(motivoLimpio);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(MensajeConcurrencia));
        }

        var actualizado = await ConDetalle().AsNoTracking().SingleAsync(h => h.IdHallazgo == id);
        return Ok(ApiResponse<HallazgoDto>.SuccessResponse(MapToDto(actualizado),
            $"Hallazgo {DescribirTransicion(estadoAnterior, destino)} exitosamente"));
    }

    private static string DescribirTransicion(string desde, string hacia) => hacia switch
    {
        HallazgoEstados.EnProceso => "en proceso",
        HallazgoEstados.Cerrado => "cerrado",
        HallazgoEstados.Anulado => "anulado",
        HallazgoEstados.Abierto when desde == HallazgoEstados.Cerrado => "reabierto",
        _ => "actualizado"
    };

    /// <summary>
    /// Un hallazgo nunca se borra físicamente: se anula con motivo. La ruta se conserva porque el
    /// cliente aún la llama, y responde 409 en lugar de 404/405 para que el motivo llegue al usuario.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteHallazgo(long id)
    {
        var existe = await _context.Hallazgos.AnyAsync(h => h.IdHallazgo == id);

        if (!existe)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Hallazgo no encontrado"));
        }

        return Conflict(ApiResponse<bool>.ErrorResponse(MensajeNoSeElimina));
    }

    /// <summary>
    /// Reglas del responsable y la fecha de compromiso, compartidas por el alta y la asignación:
    /// el responsable debe ser un usuario activo y la fecha no puede ser anterior a hoy (fecha
    /// local del servidor). Devuelve el mensaje de error, o null si todo es válido.
    /// </summary>
    private async Task<string?> ValidarAsignacionAsync(long? idUsuarioResponsable, DateTime? fechaCompromiso)
    {
        if (idUsuarioResponsable.HasValue
            && !await _context.Usuarios.AnyAsync(u => u.IdUsuario == idUsuarioResponsable.Value && u.Activo))
        {
            return MensajeResponsableInvalido;
        }

        if (fechaCompromiso.HasValue && fechaCompromiso.Value.Date < DateTime.Today)
        {
            return MensajeFechaCompromisoPasada;
        }

        return null;
    }

    /// <summary>Todo lo que muestra una fila del listado: equipo con su ubicación y los tres usuarios.</summary>
    private IQueryable<Hallazgo> ConDetalle() => _context.Hallazgos
        .Include(h => h.Inspeccion)
            .ThenInclude(i => i.Equipo)
                .ThenInclude(e => e.Ubicacion)
        .Include(h => h.UsuarioRegistro)
        .Include(h => h.UsuarioResponsable)
        .Include(h => h.UsuarioCierre);

    private static HallazgoDto MapToDto(Hallazgo h) => Completar(new HallazgoDto(), h);

    private static HallazgoDetalleDto MapToDetalleDto(Hallazgo h)
    {
        var dto = Completar(new HallazgoDetalleDto(), h);
        dto.IdPregunta = h.IdPregunta;
        dto.Pregunta = h.Pregunta?.Pregunta;
        dto.IdRespuesta = h.IdRespuesta;
        dto.ObservacionRespuesta = h.Respuesta?.Observacion;
        dto.FechaInspeccion = h.Inspeccion.FechaInspeccion;
        dto.EstadoInspeccion = h.Inspeccion.Estado;
        dto.NombreInspector = h.Inspeccion.Usuario?.Nombre;
        return dto;
    }

    /// <summary>Campos comunes al listado y al detalle; las navegaciones que no se cargaron quedan en null.</summary>
    private static T Completar<T>(T dto, Hallazgo h) where T : HallazgoDto
    {
        var equipo = h.Inspeccion?.Equipo;

        dto.IdHallazgo = h.IdHallazgo;
        dto.IdInspeccion = h.IdInspeccion;
        dto.Descripcion = h.Descripcion;
        dto.Criticidad = h.Criticidad;
        dto.Estado = h.Estado;
        dto.FechaRegistro = h.FechaRegistro;
        dto.Origen = h.Origen;
        dto.IdEquipo = h.Inspeccion?.IdEquipo ?? 0;
        dto.NombreEquipo = equipo?.NombreEquipo;
        dto.CodigoInterno = equipo?.CodigoInterno;
        dto.Area = equipo?.Ubicacion == null ? null : $"{equipo.Ubicacion.Planta} - {equipo.Ubicacion.Area}";
        dto.IdUsuarioRegistro = h.IdUsuarioRegistro;
        dto.NombreUsuarioRegistro = h.UsuarioRegistro?.Nombre;
        dto.IdUsuarioResponsable = h.IdUsuarioResponsable;
        dto.NombreResponsable = h.UsuarioResponsable?.Nombre;
        dto.FechaCompromiso = h.FechaCompromiso;
        dto.AccionCorrectiva = h.AccionCorrectiva;
        dto.FechaCierre = h.FechaCierre;
        dto.NombreUsuarioCierre = h.UsuarioCierre?.Nombre;
        dto.MotivoAnulacion = h.MotivoAnulacion;
        return dto;
    }
}
