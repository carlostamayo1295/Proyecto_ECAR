using System.Linq.Expressions;
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

/// <summary>
/// Hallazgos de la Fase 4 (endpoints 4 a 9, PLAN_FASE4_TAREAS §3.4 y §3.5). Un hallazgo nunca se
/// borra: se cierra con su acción correctiva o se anula con motivo, y cada cambio queda en la
/// auditoría. No hay DELETE: la ruta responde 405.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class HallazgosController : ControllerBase
{
    private static readonly string[] CriticidadesValidas = ["Alta", "Media", "Baja"];

    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IMotivoCambio _motivoCambio;

    public HallazgosController(ECARDbContext context, ICurrentUser currentUser, IMotivoCambio motivoCambio)
    {
        _context = context;
        _currentUser = currentUser;
        _motivoCambio = motivoCambio;
    }

    /// <summary>Endpoint 4: filtros, paginado, con equipo, área y responsable.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<HallazgoDto>>>> GetHallazgos(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null,
        [FromQuery] string? estado = null, [FromQuery] string? criticidad = null, [FromQuery] long? idEquipo = null,
        [FromQuery] long? idInspeccion = null, [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Hallazgos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(h => h.Estado == estado);
        if (!string.IsNullOrWhiteSpace(criticidad))
            query = query.Where(h => h.Criticidad == criticidad);
        if (idEquipo.HasValue)
            query = query.Where(h => h.Inspeccion.IdEquipo == idEquipo.Value);
        if (idInspeccion.HasValue)
            query = query.Where(h => h.IdInspeccion == idInspeccion.Value);

        var (inicio, fin) = HoraColombia.RangoUtc(desde, hasta);
        if (inicio.HasValue)
            query = query.Where(h => h.FechaRegistro >= inicio.Value);
        if (fin.HasValue)
            query = query.Where(h => h.FechaRegistro < fin.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var texto = search.Trim();
            query = query.Where(h =>
                h.Descripcion.Contains(texto) ||
                h.Inspeccion.Equipo.NombreEquipo.Contains(texto) ||
                h.Inspeccion.Equipo.CodigoInterno.Contains(texto) ||
                (h.Criticidad != null && h.Criticidad.Contains(texto)) ||
                (h.UsuarioResponsable != null && h.UsuarioResponsable.Nombre.Contains(texto)));
        }

        var totalCount = await query.CountAsync();
        var hallazgos = await query
            .OrderByDescending(h => h.FechaRegistro)
            .ThenByDescending(h => h.IdHallazgo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Proyeccion)
            .ToListAsync();

        return Ok(ApiResponse<PagedResultDto<HallazgoDto>>.SuccessResponse(new PagedResultDto<HallazgoDto>
        {
            Data = hallazgos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }));
    }

    /// <summary>Endpoint 5: el hallazgo y de dónde salió (inspección, pregunta y observación).</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<HallazgoDetalleDto>>> GetHallazgo(long id)
    {
        var detalle = await _context.Hallazgos.AsNoTracking()
            .Where(h => h.IdHallazgo == id)
            .Select(h => new HallazgoDetalleDto
            {
                IdHallazgo = h.IdHallazgo,
                IdInspeccion = h.IdInspeccion,
                NombreEquipo = h.Inspeccion.Equipo.NombreEquipo,
                Descripcion = h.Descripcion,
                Criticidad = h.Criticidad,
                Estado = h.Estado,
                FechaRegistro = h.FechaRegistro,
                Origen = h.Origen,
                IdEquipo = h.Inspeccion.IdEquipo,
                CodigoInterno = h.Inspeccion.Equipo.CodigoInterno,
                Area = h.Inspeccion.Equipo.Ubicacion != null
                    ? h.Inspeccion.Equipo.Ubicacion.Planta + " - " + h.Inspeccion.Equipo.Ubicacion.Area
                    : null,
                IdUsuarioRegistro = h.IdUsuarioRegistro,
                NombreUsuarioRegistro = h.UsuarioRegistro.Nombre,
                IdUsuarioResponsable = h.IdUsuarioResponsable,
                NombreResponsable = h.UsuarioResponsable != null ? h.UsuarioResponsable.Nombre : null,
                FechaCompromiso = h.FechaCompromiso,
                AccionCorrectiva = h.AccionCorrectiva,
                FechaCierre = h.FechaCierre,
                NombreUsuarioCierre = h.UsuarioCierre != null ? h.UsuarioCierre.Nombre : null,
                MotivoAnulacion = h.MotivoAnulacion,
                IdPregunta = h.IdPregunta,
                Pregunta = h.Pregunta != null ? h.Pregunta.Pregunta : null,
                ObservacionRespuesta = h.IdPregunta == null
                    ? null
                    : h.Inspeccion.Respuestas
                        .Where(r => r.IdPregunta == h.IdPregunta)
                        .Select(r => r.Observacion)
                        .FirstOrDefault(),
                FechaInspeccion = h.Inspeccion.FechaInspeccion,
                EstadoInspeccion = h.Inspeccion.Estado,
                NombreInspector = h.Inspeccion.Usuario.Nombre
            })
            .FirstOrDefaultAsync();

        return detalle == null
            ? NotFound(ApiResponse<HallazgoDetalleDto>.ErrorResponse("Hallazgo no encontrado"))
            : Ok(ApiResponse<HallazgoDetalleDto>.SuccessResponse(detalle));
    }

    /// <summary>Endpoint 6: hallazgo Manual sobre una inspección existente, en curso o cerrada.</summary>
    [HttpPost]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CreateHallazgo(CreateHallazgoDto createDto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .FirstOrDefaultAsync(i => i.IdInspeccion == createDto.IdInspeccion);
        if (inspeccion == null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La inspección indicada no existe"));
        }

        if (inspeccion.Estado == InspeccionEstados.Anulada)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La inspección está anulada"));
        }

        if (!EsAdministrador && inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        var descripcion = createDto.Descripcion?.Trim() ?? string.Empty;
        if (descripcion.Length == 0)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("La descripción es requerida"));
        }

        var error = ValidarCriticidad(createDto.Criticidad)
                    ?? ValidarFechaCompromiso(createDto.FechaCompromiso)
                    ?? await ValidarResponsableAsync(createDto.IdUsuarioResponsable);
        if (error != null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(error));
        }

        var hallazgo = new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Descripcion = descripcion,
            Criticidad = string.IsNullOrWhiteSpace(createDto.Criticidad) ? inspeccion.Equipo.Criticidad : createDto.Criticidad,
            Estado = HallazgoEstados.Abierto,
            Origen = HallazgoOrigenes.Manual,
            IdUsuarioRegistro = _currentUser.IdUsuario,
            IdUsuarioResponsable = createDto.IdUsuarioResponsable,
            FechaCompromiso = NormalizarFecha(createDto.FechaCompromiso),
            FechaRegistro = DateTime.UtcNow
        };

        _context.Hallazgos.Add(hallazgo);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetHallazgo), new { id = hallazgo.IdHallazgo },
            ApiResponse<HallazgoDto>.SuccessResponse((await CargarDtoAsync(hallazgo.IdHallazgo))!,
                "Hallazgo registrado exitosamente"));
    }

    /// <summary>
    /// Endpoint 7: criticidad, responsable y fecha de compromiso, con motivo. Solo en Abierto o
    /// EnProceso; la descripción de un hallazgo de origen Novedad es la de la inspección firmada
    /// y no se edita. El estado se cambia con POST {id}/estado.
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> UpdateHallazgo(long id, UpdateHallazgoDto updateDto)
    {
        var hallazgo = await CargarParaEditarAsync(id);
        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        if (!PuedeGestionar(hallazgo))
        {
            return Forbid();
        }

        if (!EstaAbiertoOEnProceso(hallazgo))
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(
                $"Un hallazgo {Etiqueta(hallazgo.Estado)} no se modifica"));
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Estado))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(
                "El estado se cambia con POST /api/hallazgos/{id}/estado"));
        }

        var errorMotivo = ValidarMotivo(updateDto.Motivo);
        if (errorMotivo != null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(errorMotivo));
        }

        var descripcion = updateDto.Descripcion?.Trim();
        if (!string.IsNullOrEmpty(descripcion) && descripcion != hallazgo.Descripcion)
        {
            if (hallazgo.Origen == HallazgoOrigenes.Novedad)
            {
                return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(
                    "La descripción de un hallazgo generado al firmar no se edita"));
            }

            hallazgo.Descripcion = descripcion;
        }

        var error = ValidarCriticidad(updateDto.Criticidad)
                    ?? (updateDto.FechaCompromiso?.Date != hallazgo.FechaCompromiso?.Date ? ValidarFechaCompromiso(updateDto.FechaCompromiso) : null)
                    ?? await ValidarResponsableAsync(updateDto.IdUsuarioResponsable);
        if (error != null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(error));
        }

        _motivoCambio.Establecer(updateDto.Motivo);
        if (!string.IsNullOrWhiteSpace(updateDto.Criticidad))
            hallazgo.Criticidad = updateDto.Criticidad;
        // PUT reemplaza el seguimiento: un responsable o una fecha en null los quitan.
        hallazgo.IdUsuarioResponsable = updateDto.IdUsuarioResponsable;
        hallazgo.FechaCompromiso = NormalizarFecha(updateDto.FechaCompromiso);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse((await CargarDtoAsync(id))!, "Hallazgo actualizado exitosamente"));
    }

    /// <summary>
    /// Endpoint 8 (§3.5): Abierto → EnProceso → Cerrado (con acción correctiva) y, solo el
    /// Administrador, Cerrado → Abierto. Cualquier otra transición responde 409. Siempre con motivo.
    /// </summary>
    [HttpPost("{id:long}/estado")]
    [Authorize(Roles = "Administrador,Técnico")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> CambiarEstado(long id, CambiarEstadoHallazgoDto dto)
    {
        var hallazgo = await CargarParaEditarAsync(id);
        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        if (!PuedeGestionar(hallazgo))
        {
            return Forbid();
        }

        if (!HallazgoEstados.EsValido(dto.Estado))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse("El estado indicado no existe"));
        }

        if (dto.Estado == HallazgoEstados.Anulado)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(
                "Para anular un hallazgo use POST /api/hallazgos/{id}/anular"));
        }

        var permitida = (hallazgo.Estado, dto.Estado) switch
        {
            (HallazgoEstados.Abierto, HallazgoEstados.EnProceso) => true,
            (HallazgoEstados.EnProceso, HallazgoEstados.Cerrado) => true,
            (HallazgoEstados.Cerrado, HallazgoEstados.Abierto) => true,
            _ => false
        };
        if (!permitida)
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(
                $"Un hallazgo {Etiqueta(hallazgo.Estado)} no puede pasar a {Etiqueta(dto.Estado)}"));
        }

        if (hallazgo.Estado == HallazgoEstados.Cerrado && !EsAdministrador)
        {
            return Forbid();
        }

        var errorMotivo = ValidarMotivo(dto.Motivo);
        if (errorMotivo != null)
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(errorMotivo));
        }

        if (dto.Estado == HallazgoEstados.Cerrado && string.IsNullOrWhiteSpace(dto.AccionCorrectiva))
        {
            return BadRequest(ApiResponse<HallazgoDto>.ErrorResponse(
                "Para cerrar el hallazgo hay que describir la acción correctiva"));
        }

        _motivoCambio.Establecer(dto.Motivo);
        hallazgo.Estado = dto.Estado;
        if (dto.Estado == HallazgoEstados.Cerrado)
        {
            hallazgo.AccionCorrectiva = dto.AccionCorrectiva!.Trim();
            hallazgo.FechaCierre = DateTime.UtcNow;
            hallazgo.IdUsuarioCierre = _currentUser.IdUsuario;
        }
        else if (dto.Estado == HallazgoEstados.Abierto)
        {
            // Reabrir: el cierre anterior queda en la auditoría.
            hallazgo.FechaCierre = null;
            hallazgo.IdUsuarioCierre = null;
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponse<HallazgoDto>.SuccessResponse((await CargarDtoAsync(id))!,
            $"El hallazgo pasó a {Etiqueta(dto.Estado)}"));
    }

    /// <summary>Endpoint 9: solo el Administrador, con motivo, desde Abierto o EnProceso.</summary>
    [HttpPost("{id:long}/anular")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ApiResponse<HallazgoDto>>> AnularHallazgo(long id, MotivoDto dto)
    {
        var hallazgo = await _context.Hallazgos.FirstOrDefaultAsync(h => h.IdHallazgo == id);
        if (hallazgo == null)
        {
            return NotFound(ApiResponse<HallazgoDto>.ErrorResponse("Hallazgo no encontrado"));
        }

        if (!EstaAbiertoOEnProceso(hallazgo))
        {
            return Conflict(ApiResponse<HallazgoDto>.ErrorResponse(
                $"Un hallazgo {Etiqueta(hallazgo.Estado)} no se puede anular"));
        }

        _motivoCambio.Establecer(dto.Motivo);
        hallazgo.Estado = HallazgoEstados.Anulado;
        hallazgo.MotivoAnulacion = dto.Motivo.Trim();
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<HallazgoDto>.SuccessResponse((await CargarDtoAsync(id))!, "Hallazgo anulado"));
    }

    private static readonly Expression<Func<Hallazgo, HallazgoDto>> Proyeccion = h => new HallazgoDto
    {
        IdHallazgo = h.IdHallazgo,
        IdInspeccion = h.IdInspeccion,
        NombreEquipo = h.Inspeccion.Equipo.NombreEquipo,
        Descripcion = h.Descripcion,
        Criticidad = h.Criticidad,
        Estado = h.Estado,
        FechaRegistro = h.FechaRegistro,
        Origen = h.Origen,
        IdEquipo = h.Inspeccion.IdEquipo,
        CodigoInterno = h.Inspeccion.Equipo.CodigoInterno,
        Area = h.Inspeccion.Equipo.Ubicacion != null
            ? h.Inspeccion.Equipo.Ubicacion.Planta + " - " + h.Inspeccion.Equipo.Ubicacion.Area
            : null,
        IdUsuarioRegistro = h.IdUsuarioRegistro,
        NombreUsuarioRegistro = h.UsuarioRegistro.Nombre,
        IdUsuarioResponsable = h.IdUsuarioResponsable,
        NombreResponsable = h.UsuarioResponsable != null ? h.UsuarioResponsable.Nombre : null,
        FechaCompromiso = h.FechaCompromiso,
        AccionCorrectiva = h.AccionCorrectiva,
        FechaCierre = h.FechaCierre,
        NombreUsuarioCierre = h.UsuarioCierre != null ? h.UsuarioCierre.Nombre : null,
        MotivoAnulacion = h.MotivoAnulacion
    };

    private Task<HallazgoDto?> CargarDtoAsync(long id) =>
        _context.Hallazgos.AsNoTracking().Where(h => h.IdHallazgo == id).Select(Proyeccion).FirstOrDefaultAsync();

    private Task<Hallazgo?> CargarParaEditarAsync(long id) =>
        _context.Hallazgos.Include(h => h.Inspeccion).FirstOrDefaultAsync(h => h.IdHallazgo == id);

    private bool EsAdministrador => _currentUser.IsInRole("Administrador");

    /// <summary>
    /// El Administrador gestiona cualquier hallazgo; un Técnico, los que registró, los que tiene
    /// asignados y los de sus propias inspecciones.
    /// </summary>
    private bool PuedeGestionar(Hallazgo hallazgo) =>
        EsAdministrador ||
        (_currentUser.IsInRole("Técnico") &&
         (hallazgo.IdUsuarioRegistro == _currentUser.IdUsuario ||
          hallazgo.IdUsuarioResponsable == _currentUser.IdUsuario ||
          hallazgo.Inspeccion.IdUsuario == _currentUser.IdUsuario));

    private static bool EstaAbiertoOEnProceso(Hallazgo hallazgo) =>
        hallazgo.Estado is HallazgoEstados.Abierto or HallazgoEstados.EnProceso;

    private static string Etiqueta(string? estado) => estado switch
    {
        HallazgoEstados.Abierto => "abierto",
        HallazgoEstados.EnProceso => "en proceso",
        HallazgoEstados.Cerrado => "cerrado",
        HallazgoEstados.Anulado => "anulado",
        _ => estado ?? "-"
    };

    private static string? ValidarMotivo(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < MotivoDto.LongitudMinima ||
        motivo.Trim().Length > MotivoDto.LongitudMaxima
            ? $"El motivo es obligatorio y debe tener entre {MotivoDto.LongitudMinima} y {MotivoDto.LongitudMaxima} caracteres"
            : null;

    private static string? ValidarCriticidad(string? criticidad) =>
        string.IsNullOrWhiteSpace(criticidad) || CriticidadesValidas.Contains(criticidad)
            ? null
            : "La criticidad debe ser Alta, Media o Baja";

    private static string? ValidarFechaCompromiso(DateTime? fecha) =>
        fecha.HasValue && fecha.Value.Date < HoraColombia.DesdeUtc(DateTime.UtcNow).Date
            ? "La fecha de compromiso no puede ser anterior a hoy"
            : null;

    /// <summary>El responsable debe ser un usuario activo con rol Técnico o Administrador.</summary>
    private async Task<string?> ValidarResponsableAsync(long? idUsuario)
    {
        if (!idUsuario.HasValue)
        {
            return null;
        }

        var valido = await _context.Usuarios.AnyAsync(u =>
            u.IdUsuario == idUsuario.Value && u.Activo &&
            u.UsuarioRoles.Any(ur => ur.Rol.Nombre == "Técnico" || ur.Rol.Nombre == "Administrador"));
        return valido ? null : "El responsable debe ser un usuario activo con rol Técnico o Administrador";
    }

    /// <summary>
    /// Una fecha de compromiso es un día, no un instante: se guarda a las 12:00 UTC para que la
    /// conversión a hora local de cualquier zona de América siga cayendo en el mismo día.
    /// </summary>
    private static DateTime? NormalizarFecha(DateTime? fecha) =>
        fecha.HasValue ? DateTime.SpecifyKind(fecha.Value.Date.AddHours(12), DateTimeKind.Utc) : null;
}
