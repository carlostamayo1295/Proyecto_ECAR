using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.API.Services;
using ECAR.API.Services.Documentos;
using ECAR.API.Services.Notificaciones;
using ECAR.API.Exceptions;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RolesSistema.Todos)]
public class InspeccionesController : ControllerBase
{
    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IInspeccionService _inspeccionService;
    private readonly IEvidenciaStorage _storage;
    private readonly ILogger<InspeccionesController> _logger;
    private readonly IMotivoCambio _motivoCambio;
    private readonly INotificadorNovedades? _notificador;

    /// <param name="motivoCambio">
    /// Motivo que la auditoría copia en sus filas (anular). Es opcional solo para no cambiar las
    /// pruebas que ya construyen el controlador; en el API siempre lo inyecta el contenedor.
    /// </param>
    /// <param name="notificador">El correo de cada novedad a la mesa de ayuda; opcional por lo mismo.</param>
    public InspeccionesController(ECARDbContext context, ICurrentUser currentUser, IInspeccionService inspeccionService,
        IEvidenciaStorage storage, ILogger<InspeccionesController> logger, IMotivoCambio? motivoCambio = null,
        INotificadorNovedades? notificador = null)
    {
        _context = context;
        _currentUser = currentUser;
        _inspeccionService = inspeccionService;
        _storage = storage;
        _logger = logger;
        _motivoCambio = motivoCambio ?? new MotivoCambio();
        _notificador = notificador;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<InspeccionDto>>>> GetInspecciones([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? estado = null, [FromQuery] long? idEquipo = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .AsQueryable();

        if (EsInspectorSinLecturaGlobal())
        {
            var idUsuario = _currentUser.IdUsuario;
            query = query.Where(i => i.IdUsuario == idUsuario);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(i =>
                i.Equipo.NombreEquipo.Contains(search) ||
                i.Usuario.Nombre.Contains(search) ||
                (i.Resultado != null && i.Resultado.Contains(search)));
        }

        // Filtro por estado (EnCurso / NovedadesPendientes / Cerrada / Anulada) antes de paginar. Sin esto el cliente
        // filtraba sobre la página ya cargada y podía decir "no hay inspecciones en curso"
        // habiéndolas en otras páginas. Sin filtro se listan todas, también las anuladas: se
        // conservan como evidencia y se distinguen por Estado y por los campos de anulación.
        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(i => i.Estado == estado);
        }

        // Consulta histórica de un equipo (SRS §2 y §6, Gestión de Equipos). Un Operador sigue
        // viendo solo las suyas: el filtro de arriba ya se aplicó.
        if (idEquipo.HasValue)
        {
            query = query.Where(i => i.IdEquipo == idEquipo.Value);
        }

        var totalCount = await query.CountAsync();

        var inspecciones = await query
            .OrderByDescending(i => i.FechaInspeccion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InspeccionDto
            {
                IdInspeccion = i.IdInspeccion,
                IdEquipo = i.IdEquipo,
                NombreEquipo = i.Equipo.NombreEquipo,
                IdUsuario = i.IdUsuario,
                NombreUsuario = i.Usuario.Nombre,
                IdChecklist = i.IdChecklist,
                NombreChecklist = i.Checklist.Nombre,
                FechaInspeccion = i.FechaInspeccion,
                Estado = i.Estado,
                FechaCierre = i.FechaCierre,
                Resultado = i.Resultado,
                Observaciones = i.Observaciones,
                TieneFirma = i.FirmaDigital != null && i.FirmaDigital != "",
                TotalEvidencias = i.Evidencias.Count(e => !e.Retirada),
                TotalNovedades = i.Novedades.Count,
                NovedadesPendientes = i.Novedades.Count(n => n.Estado == NovedadEstados.Pendiente),
                MotivoAnulacion = i.MotivoAnulacion,
                FechaAnulacion = i.FechaAnulacion,
                IdUsuarioAnulacion = i.IdUsuarioAnulacion,
                NombreUsuarioAnulacion = i.UsuarioAnulacion != null ? i.UsuarioAnulacion.Nombre : null
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<InspeccionDto>
        {
            Data = inspecciones,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResultDto<InspeccionDto>>.SuccessResponse(pagedResult));
    }

    // Punto 2: GET /api/inspecciones/mias?estado=
    [HttpGet("mias")]
    [Authorize(Roles = RolesSistema.Inspectores)]
    public async Task<ActionResult<ApiResponse<List<InspeccionDto>>>> GetMisInspecciones([FromQuery] string? estado)
    {
        var usuarioId = _currentUser.IdUsuario;
        var result = await _inspeccionService.ObtenerMisInspeccionesAsync(usuarioId, estado);
        return Ok(ApiResponse<List<InspeccionDto>>.SuccessResponse(result));
    }

    // Punto 1: GET /api/inspecciones/{id}/respuestas
    [HttpGet("{id}/respuestas")]
    public async Task<ActionResult<ApiResponse<List<RespuestaInspeccionDto>>>> GetRespuestas(long id)
    {
        try
        {
            var respuestas = await _inspeccionService.ObtenerRespuestasAsync(id, _currentUser.IdUsuario,
                AccesoInspeccion.LeeTodas(_currentUser));
            return Ok(ApiResponse<List<RespuestaInspeccionDto>>.SuccessResponse(respuestas));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<List<RespuestaInspeccionDto>>.ErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> GetInspeccion(long id)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Include(i => i.Evidencias)
            .Include(i => i.Novedades)
            .Include(i => i.UsuarioAnulacion)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeLeer(inspeccion))
        {
            return Forbid();
        }

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion)));
    }

    [HttpPost("iniciar")]
    [Authorize(Roles = RolesSistema.Inspeccionan)]
    public async Task<ActionResult<ApiResponse<InspeccionEjecucionDto>>> IniciarInspeccion(
        IniciarInspeccionDto iniciarDto)
    {
        var usuario = await _context.Usuarios.FindAsync(_currentUser.IdUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "El usuario autenticado no existe o no está habilitado en ECAR"));
        }

        var equipoExiste = await _context.Equipos.AnyAsync(e =>
            e.IdEquipo == iniciarDto.IdEquipo && e.Activo);
        if (!equipoExiste)
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "El equipo indicado no existe o no está activo"));
        }

        var checklistExiste = await _context.Checklists.AnyAsync(c =>
            c.IdChecklist == iniciarDto.IdChecklist && c.Activo);
        if (!checklistExiste)
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "El checklist indicado no existe o no está activo"));
        }

        if (!await _context.PreguntasChecklist.AnyAsync(p => p.IdChecklist == iniciarDto.IdChecklist))
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(MensajeChecklistSinPreguntas));
        }

        var existente = await _context.Inspecciones
            .Where(i => i.IdEquipo == iniciarDto.IdEquipo
                && i.IdUsuario == usuario.IdUsuario
                && i.Estado == InspeccionEstados.EnCurso)
            .Select(i => i.IdInspeccion)
            .FirstOrDefaultAsync();

        if (existente != 0)
        {
            var ejecucionExistente = await CargarEjecucionAsync(existente);
            return Conflict(ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
                MapToEjecucionDto(ejecucionExistente!),
                "Ya existe una inspección en curso para este equipo; puede continuarla"));
        }

        var inspeccion = new Inspeccion
        {
            IdEquipo = iniciarDto.IdEquipo,
            IdChecklist = iniciarDto.IdChecklist,
            IdUsuario = usuario.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };

        _context.Inspecciones.Add(inspeccion);
        await _context.SaveChangesAsync();

        var ejecucion = await CargarEjecucionAsync(inspeccion.IdInspeccion);
        return CreatedAtAction(
            nameof(GetEjecucion),
            new { id = inspeccion.IdInspeccion },
            ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
                MapToEjecucionDto(ejecucion!),
                "Inspección iniciada exitosamente"));
    }

    [HttpGet("{id}/ejecucion")]
    public async Task<ActionResult<ApiResponse<InspeccionEjecucionDto>>> GetEjecucion(long id)
    {
        var inspeccion = await CargarEjecucionAsync(id);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(
                "Inspección no encontrada"));
        }

        if (!PuedeLeer(inspeccion))
        {
            return Forbid();
        }

        return Ok(ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
            MapToEjecucionDto(inspeccion)));
    }

    [HttpPost]
    [Authorize(Roles = RolesSistema.Administrador)]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> CreateInspeccion(CreateInspeccionDto createDto)
    {
        // Misma regla que iniciar desde el QR: un equipo desactivado no se inspecciona.
        var equipo = await _context.Equipos.FindAsync(createDto.IdEquipo);
        if (equipo == null || !equipo.Activo)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("El equipo indicado no existe o no está activo"));
        }

        var usuario = await _context.Usuarios.FindAsync(_currentUser.IdUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse("El usuario autenticado no existe o no está habilitado en ECAR"));
        }

        var checklist = await _context.Checklists.FirstOrDefaultAsync(c =>
            c.IdChecklist == createDto.IdChecklist && c.Activo);
        if (checklist == null)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse(
                "El checklist indicado no existe o no está activo"));
        }

        if (!await _context.PreguntasChecklist.AnyAsync(p => p.IdChecklist == checklist.IdChecklist))
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse(MensajeChecklistSinPreguntas));
        }

        // La fecha es la del servidor, como en iniciar (plan §8; ALCOA+ contemporáneo): la que
        // mande el cliente se ignora, para que nadie registre una inspección con fecha pasada.
        var inspeccion = new Inspeccion
        {
            IdEquipo = createDto.IdEquipo,
            IdUsuario = usuario.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Observaciones = createDto.Observaciones,
            Estado = InspeccionEstados.EnCurso
        };

        _context.Inspecciones.Add(inspeccion);
        await _context.SaveChangesAsync();

        await _context.Entry(inspeccion).Reference(i => i.Equipo).LoadAsync();
        await _context.Entry(inspeccion).Reference(i => i.Usuario).LoadAsync();
        await _context.Entry(inspeccion).Reference(i => i.Checklist).LoadAsync();

        return CreatedAtAction(nameof(GetInspeccion), new { id = inspeccion.IdInspeccion },
            ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion), "Inspección registrada exitosamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = RolesSistema.Inspeccionan)]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> UpdateInspeccion(long id, UpdateInspeccionDto updateDto)
    {
        var inspeccion = await _context.Inspecciones
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Include(i => i.Evidencias)
            .Include(i => i.Novedades)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);

        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeModificar(inspeccion))
        {
            return Forbid();
        }

        // Regla 6 del SRS: una inspección cerrada es evidencia histórica y no admite cambios;
        // una anulada tampoco (§3.6).
        if (InmutabilidadInspeccion.MensajeSiNoAdmiteCambios(inspeccion.Estado) is { } noAdmite)
        {
            return Conflict(ApiResponse<InspeccionDto>.ErrorResponse(noAdmite));
        }

        if (updateDto.Resultado != null || updateDto.FirmaDigital != null)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse(
                "El resultado y la firma solo se establecen mediante el cierre firmado de la inspección"));
        }

        if (updateDto.Observaciones != null)
            inspeccion.Observaciones = updateDto.Observaciones;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra petición la firmó o la anuló mientras tanto (RowVersion): nada se guardó.
            return Conflict(ApiResponse<InspeccionDto>.ErrorResponse(
                MensajeSegunEstadoActual(await _context.ObtenerEstadoEscrituraAsync(id)) ?? MensajeConcurrencia));
        }

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(inspeccion), "Inspección actualizada exitosamente"));
    }

    // Punto 6, 3, 4, 5: Guardar respuestas usando el servicio y devolviendo 409 Conflict si está cerrada
    [HttpPut("{id}/respuestas")]
    [Authorize(Roles = RolesSistema.Inspeccionan)]
    public async Task<ActionResult<ApiResponse<InspeccionEjecucionDto>>> GuardarRespuestas(
        long id,
        [FromBody] GuardarRespuestasDto dto)
    {
        try
        {
            var usuarioId = _currentUser.IdUsuario;
            var esAdmin = _currentUser.IsInRole(RolesSistema.Administrador);

            await _inspeccionService.GuardarRespuestasAsync(id, dto, usuarioId, esAdmin);

            var ejecucionActualizada = await CargarEjecucionAsync(id);
            return Ok(ApiResponse<InspeccionEjecucionDto>.SuccessResponse(
                MapToEjecucionDto(ejecucionActualizada!),
                "Respuestas guardadas exitosamente"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InspeccionCerradaException ex) // Retorna 409 Conflict
        {
            return Conflict(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex) // Retorna 400 BadRequest para validaciones
        {
            return BadRequest(ApiResponse<InspeccionEjecucionDto>.ErrorResponse(ex.Message));
        }
    }

    public const string MensajeMotivoRequerido = "El motivo es requerido y debe tener entre 10 y 500 caracteres";

    /// <summary>
    /// POST api/inspecciones/{id}/anular (§3.6): sustituye al borrado. Solo una inspección
    /// EnCurso y solo la anula el Administrador (pedido del 22/10/2026; antes también su inspector),
    /// siempre con motivo. Se conservan sus respuestas, evidencias y fotos, y queda inmutable. Estado,
    /// motivo, fecha, usuario y la fila "Anular" de la auditoría se guardan en una transacción.
    /// Orden: 404, 409 (no está en curso), 403, 400 (motivo).
    /// </summary>
    [HttpPost("{id:long}/anular")]
    [Authorize(Roles = RolesSistema.Administrador)]
    public async Task<ActionResult<ApiResponse<InspeccionDto>>> AnularInspeccion(long id, MotivoDto motivoDto)
    {
        var inspeccion = await _context.Inspecciones.FirstOrDefaultAsync(i => i.IdInspeccion == id);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (inspeccion.Estado != InspeccionEstados.EnCurso)
        {
            return Conflict(ApiResponse<InspeccionDto>.ErrorResponse(inspeccion.Estado == InspeccionEstados.Anulada
                ? "La inspección ya está anulada"
                : "Una inspección cerrada y firmada no se puede anular"));
        }

        // El [Authorize] ya lo exige; se repite por quien llame al controlador sin pasar por él.
        if (!_currentUser.IsInRole(RolesSistema.Administrador))
        {
            return Forbid();
        }

        // [Required]/[StringLength] de MotivoDto ya lo rechazan; esto cubre a quien llame sin
        // validación del modelo y descarta un motivo hecho solo de espacios.
        var motivo = motivoDto.Motivo?.Trim();
        if (motivo == null || motivo.Length is < MotivoDto.LongitudMinima or > MotivoDto.LongitudMaxima)
        {
            return BadRequest(ApiResponse<InspeccionDto>.ErrorResponse(MensajeMotivoRequerido));
        }

        inspeccion.Estado = InspeccionEstados.Anulada;
        inspeccion.MotivoAnulacion = motivo;
        inspeccion.FechaAnulacion = DateTime.UtcNow;
        inspeccion.IdUsuarioAnulacion = _currentUser.IdUsuario;
        _motivoCambio.Establecer(motivo);

        // Un único SaveChanges: el cambio y su fila de auditoría van en la misma transacción.
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra petición la firmó o la anuló mientras tanto (RowVersion): nada se guardó.
            return Conflict(ApiResponse<InspeccionDto>.ErrorResponse(
                MensajeSegunEstadoActual(await _context.ObtenerEstadoEscrituraAsync(id)) ?? MensajeConcurrencia));
        }

        var anulada = await _context.Inspecciones
            .AsNoTracking()
            .Include(i => i.Equipo)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Include(i => i.Evidencias)
            .Include(i => i.Novedades)
            .Include(i => i.UsuarioAnulacion)
            .SingleAsync(i => i.IdInspeccion == id);

        return Ok(ApiResponse<InspeccionDto>.SuccessResponse(MapToDto(anulada), "Inspección anulada exitosamente"));
    }

    /// <summary>
    /// Firma y cierra la inspección: son la misma acción, no existe cerrada sin firma.
    /// Valida obligatorias (regla 3 del SRS), novedad→observación (regla 4), y la firma PNG;
    /// después calcula Resultado y FirmaHash y deja la inspección inmutable (regla 6). Con
    /// novedades queda NovedadesPendientes y con resultado "Pendiente": se crea una novedad por cada
    /// "No", se avisa a la mesa de ayuda con un solo correo con todas ellas y pasa sola a Cerrada al
    /// resolverse la última.
    /// </summary>
    [HttpPost("{id}/firmar")]
    [Authorize(Roles = RolesSistema.Inspeccionan)]
    public async Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> FirmarInspeccion(
        long id,
        FirmarInspeccionDto firmarDto,
        [FromServices] IAuthService authService,
        [FromServices] VerificadorIntegridad verificador)
    {
        var inspeccion = await CargarCierreAsync(id, seguimiento: true);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionResultadoDto>.ErrorResponse("Inspección no encontrada"));
        }

        // La firma es personal: solo el inspector que ejecutó la inspección puede cerrarla,
        // aunque quien llame sea Administrador.
        if (inspeccion.IdUsuario != _currentUser.IdUsuario)
        {
            return Forbid();
        }

        // Ni una cerrada ni una anulada se firman.
        if (InmutabilidadInspeccion.MensajeSiNoAdmiteCambios(inspeccion.Estado) is { } noAdmite)
        {
            return Conflict(ApiResponse<InspeccionResultadoDto>.ErrorResponse(noAdmite));
        }

        var faltantes = ValidarContenidoParaCierre(inspeccion);
        if (faltantes.Count > 0)
        {
            return BadRequest(ApiResponse<InspeccionResultadoDto>.ErrorResponse(
                "La inspección no puede cerrarse: faltan datos obligatorios", faltantes));
        }

        if (!FirmaInspeccion.TryValidarPng(firmarDto.FirmaPngBase64, out var firmaPng, out var errorFirma))
        {
            return BadRequest(ApiResponse<InspeccionResultadoDto>.ErrorResponse(errorFirma));
        }

        if (string.IsNullOrEmpty(firmarDto.Password))
        {
            return BadRequest(ApiResponse<InspeccionResultadoDto>.ErrorResponse(
                "Debe ingresar su contraseña para firmar"));
        }

        try
        {
            if (!await authService.VerificarPasswordAsync(_currentUser.IdUsuario, firmarDto.Password, AuthService.OperacionFirma))
            {
                return BadRequest(ApiResponse<InspeccionResultadoDto>.ErrorResponse(
                    "La contraseña es incorrecta; la inspección no se cerró"));
            }
        }
        catch (CuentaBloqueadaException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, ApiResponse<InspeccionResultadoDto>.ErrorResponse(ex.Message));
        }

        var fechaCierre = DateTime.UtcNow;
        var conNovedades = TieneNovedades(inspeccion);
        inspeccion.Estado = conNovedades ? InspeccionEstados.NovedadesPendientes : InspeccionEstados.Cerrada;
        inspeccion.FechaCierre = fechaCierre;
        // Sin novedades, Satisfactorio. Con novedades, Pendiente hasta resolverse la última, y entonces
        // Conforme (NovedadesController).
        inspeccion.Resultado = conNovedades ? InspeccionResultados.Pendiente : InspeccionResultados.Satisfactorio;

        if (!string.IsNullOrWhiteSpace(firmarDto.Observaciones))
        {
            inspeccion.Observaciones = firmarDto.Observaciones;
        }

        inspeccion.FirmaDigital = firmaPng;
        inspeccion.FirmaNombre = inspeccion.Usuario.Nombre;
        inspeccion.FirmaSignificado = VerificadorIntegridad.SignificadoEjecucion;
        inspeccion.FirmaAlgoritmo = VerificadorIntegridad.AlgoritmoHmacV2;
        inspeccion.FirmaHash = await verificador.CalcularSelloAsync(inspeccion);

        var novedades = AgregarNovedades(inspeccion, fechaCierre);

        // Un único SaveChanges: estado, resultado, firma, hash y novedades se guardan en la misma
        // transacción, de modo que no puede quedar una inspección cerrada a medias ni una
        // respuesta "No" sin su novedad. Si falla cualquiera de ellos, la firma entera se revierte.
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Otra petición firmó o anuló la inspección entre la comprobación y el guardado: el
            // RowVersion (o, con novedades, el índice único de novedades) rechazó esta y la
            // transacción entera se revirtió. Se responde como a cualquier escritura sobre una
            // cerrada o anulada; otro fallo sigue su curso.
            if (MensajeSegunEstadoActual(await _context.ObtenerEstadoEscrituraAsync(id)) is not { } mensaje)
            {
                throw;
            }

            return Conflict(ApiResponse<InspeccionResultadoDto>.ErrorResponse(mensaje));
        }

        // El correo sale después de confirmar la firma y no la detiene si falla. Uno solo por
        // inspección, con todas sus novedades.
        if (novedades.Count > 0)
        {
            _notificador?.Encolar(inspeccion.IdInspeccion);
        }

        return Ok(ApiResponse<InspeccionResultadoDto>.SuccessResponse(
            MapToResultadoDto(inspeccion),
            novedades.Count == 0
                ? "Inspección firmada y cerrada exitosamente"
                : $"Inspección firmada. Quedan {novedades.Count} novedad(es) por resolver; se avisará por correo a la mesa de ayuda"));
    }

    /// <summary>
    /// Endpoint 14 (Parte 11 §11.10(b)): copia legible y completa de una inspección cerrada, con la
    /// manifestación de la firma. Cada descarga queda en la auditoría como Exportacion.
    /// </summary>
    [HttpGet("{id:long}/registro.pdf")]
    public async Task<IActionResult> ObtenerRegistroPdf(long id)
    {
        var inspeccion = await CargarCierreAsync(id, seguimiento: false);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeLeer(inspeccion))
        {
            return Forbid();
        }

        if (!EstaFirmada(inspeccion))
        {
            return Conflict(ApiResponse<bool>.ErrorResponse("Solo las inspecciones firmadas tienen registro PDF"));
        }

        var imagenes = new Dictionary<long, byte[]>();
        foreach (var evidencia in inspeccion.Evidencias.Where(e => !e.Retirada))
        {
            try
            {
                await using var stream = await _storage.AbrirAsync(evidencia.Archivo);
                using var copia = new MemoryStream();
                await stream.CopyToAsync(copia);
                imagenes[evidencia.IdEvidencia] = copia.ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // El registro sale igual; la miniatura que falte se indica en el documento.
                _logger.LogWarning(ex, "No se pudo leer la evidencia {IdEvidencia} para el registro PDF", evidencia.IdEvidencia);
            }
        }

        var bytes = RegistroInspeccionPdf.Generar(inspeccion, imagenes, _currentUser.Nombre, DateTime.UtcNow);
        await _context.RegistrarEventoAsync(AuditoriaAcciones.Exportacion, inspeccion.IdInspeccion,
            new { Documento = "registro.pdf", inspeccion.IdInspeccion });
        return File(bytes, ExportadorTabla.TipoPdf, $"ECAR-INSP-{inspeccion.IdInspeccion}.pdf");
    }

    /// <summary>
    /// Endpoint 13 (Parte 11 §11.70): recalcula la huella con el algoritmo con que se firmó y la
    /// compara con la guardada. Solo Administrador y Supervisor (§3.4). Cada verificación queda en la
    /// auditoría.
    /// </summary>
    [HttpGet("{id}/integridad")]
    [Authorize(Roles = RolesSistema.Supervisan)]
    public async Task<ActionResult<ApiResponse<IntegridadInspeccionDto>>> VerificarIntegridad(
        long id,
        [FromServices] VerificadorIntegridad verificador)
    {
        var inspeccion = await CargarCierreAsync(id, seguimiento: false);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<IntegridadInspeccionDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (!EstaFirmada(inspeccion))
        {
            return Conflict(ApiResponse<IntegridadInspeccionDto>.ErrorResponse(
                "Solo se verifica una inspección firmada"));
        }

        var resultado = new IntegridadInspeccionDto
        {
            Integra = await verificador.VerificarAsync(inspeccion),
            Algoritmo = VerificadorIntegridad.NombreAlgoritmo(inspeccion) ?? string.Empty,
            VerificadoEn = DateTime.UtcNow
        };

        await _context.RegistrarEventoAsync(AuditoriaAcciones.VerificacionIntegridad,
            detalle: new { IdInspeccion = id, resultado.Integra, resultado.Algoritmo });

        return Ok(ApiResponse<IntegridadInspeccionDto>.SuccessResponse(resultado,
            resultado.Integra
                ? "La inspección no ha cambiado desde que se firmó"
                : "La inspección fue alterada después de firmarse"));
    }

    /// <summary>Vista de solo lectura de una inspección ya cerrada: resumen, respuestas, evidencias, firma y hash.</summary>
    [HttpGet("{id}/resultado")]
    public async Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> GetResultado(long id)
    {
        var inspeccion = await CargarCierreAsync(id, seguimiento: false);
        if (inspeccion == null)
        {
            return NotFound(ApiResponse<InspeccionResultadoDto>.ErrorResponse("Inspección no encontrada"));
        }

        if (!PuedeLeer(inspeccion))
        {
            return Forbid();
        }

        if (!EstaFirmada(inspeccion))
        {
            return Conflict(ApiResponse<InspeccionResultadoDto>.ErrorResponse(inspeccion.Estado == InspeccionEstados.Anulada
                ? "La inspección está anulada y no tiene resultado firmado; consúltela en /api/inspecciones/{id}/ejecucion"
                : "La inspección aún está en curso; consúltela en /api/inspecciones/{id}/ejecucion"));
        }

        return Ok(ApiResponse<InspeccionResultadoDto>.SuccessResponse(MapToResultadoDto(inspeccion)));
    }

    private const string MensajeInspeccionCerrada = InmutabilidadInspeccion.MensajeCerrada;

    public const string MensajeConcurrencia =
        "La inspección cambió mientras se procesaba la solicitud; vuelva a cargarla";

    /// <summary>Tras un conflicto al guardar: el mensaje del 409 si ahora está cerrada o anulada.</summary>
    private static string? MensajeSegunEstadoActual(EstadoEscritura estado) => estado switch
    {
        EstadoEscritura.Cerrada => InmutabilidadInspeccion.MensajeCerrada,
        EstadoEscritura.Anulada => InmutabilidadInspeccion.MensajeAnulada,
        _ => null
    };

    /// <summary>Firmada: Cerrada o con novedades pendientes. Las dos tienen resultado, registro PDF y sello.</summary>
    private static bool EstaFirmada(Inspeccion inspeccion) => InspeccionEstados.EstaFirmada(inspeccion.Estado);

    /// <summary>
    /// Comprueba las reglas 3 y 4 del SRS y devuelve todos los incumplimientos de una vez,
    /// para que la pantalla de firma pueda listarle al técnico qué le falta sin ir de uno en uno.
    /// </summary>
    public const string MensajeChecklistSinPreguntas =
        "El checklist no tiene preguntas: agréguelas antes de usarlo en una inspección";

    private static List<string> ValidarContenidoParaCierre(Inspeccion inspeccion)
    {
        var respuestasPorPregunta = inspeccion.Respuestas
            .ToDictionary(respuesta => respuesta.IdPregunta);
        var faltantes = new List<string>();

        // Una inspección sin preguntas no registra nada: no se firma.
        if (inspeccion.Checklist.Preguntas.Count == 0)
        {
            faltantes.Add(MensajeChecklistSinPreguntas);
        }

        foreach (var pregunta in inspeccion.Checklist.Preguntas
            .OrderBy(pregunta => pregunta.Orden)
            .ThenBy(pregunta => pregunta.IdPregunta))
        {
            respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta);
            var sinResponder = respuesta == null || string.IsNullOrWhiteSpace(respuesta.Respuesta);

            if (pregunta.Obligatoria && sinResponder)
            {
                faltantes.Add($"La pregunta obligatoria «{pregunta.Pregunta}» no tiene respuesta");
                continue;
            }

            if (!sinResponder
                && EsNovedad(pregunta.TipoRespuesta, respuesta!.Respuesta)
                && string.IsNullOrWhiteSpace(respuesta.Observacion))
            {
                faltantes.Add($"La novedad de «{pregunta.Pregunta}» requiere una observación");
            }
        }

        return faltantes;
    }

    private static bool TieneNovedades(Inspeccion inspeccion)
    {
        var tipoPorPregunta = inspeccion.Checklist.Preguntas
            .ToDictionary(pregunta => pregunta.IdPregunta, pregunta => pregunta.TipoRespuesta);

        return inspeccion.Respuestas.Any(respuesta =>
            tipoPorPregunta.TryGetValue(respuesta.IdPregunta, out var tipo)
            && EsNovedad(tipo, respuesta.Respuesta));
    }

    /// <summary>
    /// Una novedad Pendiente por cada respuesta "No" (regla 4 del SRS), a nombre del inspector que
    /// firma y con la criticidad del equipo. Solo las agrega al contexto: se guardan en el mismo
    /// SaveChanges que la firma. Una pregunta que ya tenga su novedad en esta inspección se salta,
    /// así que repetir la firma no duplica nada.
    /// </summary>
    private List<Novedad> AgregarNovedades(Inspeccion inspeccion, DateTime fechaCierre)
    {
        var preguntas = inspeccion.Checklist.Preguntas.ToDictionary(pregunta => pregunta.IdPregunta);
        var yaRegistradas = inspeccion.Novedades.Select(novedad => novedad.IdPregunta).ToHashSet();
        var nuevas = new List<Novedad>();

        foreach (var respuesta in inspeccion.Respuestas.OrderBy(respuesta => respuesta.IdPregunta))
        {
            if (!preguntas.TryGetValue(respuesta.IdPregunta, out var pregunta)
                || !EsNovedad(pregunta.TipoRespuesta, respuesta.Respuesta)
                || yaRegistradas.Contains(pregunta.IdPregunta))
            {
                continue;
            }

            var novedad = new Novedad
            {
                IdInspeccion = inspeccion.IdInspeccion,
                IdPregunta = pregunta.IdPregunta,
                IdRespuesta = respuesta.IdRespuesta,
                Descripcion = $"{pregunta.Pregunta}: {respuesta.Observacion!.Trim()}",
                Criticidad = inspeccion.Equipo.Criticidad,
                Estado = NovedadEstados.Pendiente,
                FechaRegistro = fechaCierre,
                IdUsuarioRegistro = inspeccion.IdUsuario
            };
            _context.Novedades.Add(novedad);
            nuevas.Add(novedad);
        }

        return nuevas;
    }

    /// <summary>Una respuesta Sí/No marcada como "No" es una novedad (regla 4 del SRS).</summary>
    private static bool EsNovedad(string? tipoRespuesta, string? respuesta) =>
        tipoRespuesta == TiposRespuesta.SiNo
        && string.Equals(respuesta, "No", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Carga todo lo que interviene en el cierre. Con <paramref name="seguimiento"/> en true
    /// para firmar (hay que escribir) y en false para la consulta del resultado.
    /// </summary>
    private Task<Inspeccion?> CargarCierreAsync(long id, bool seguimiento)
    {
        var query = _context.Inspecciones.AsQueryable();
        if (!seguimiento)
        {
            query = query.AsNoTracking();
        }

        return query
            .Include(i => i.Equipo)
                .ThenInclude(e => e.Ubicacion)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
                .ThenInclude(c => c.Preguntas)
            .Include(i => i.Respuestas)
            .Include(i => i.Evidencias)
                .ThenInclude(e => e.UsuarioCargaDetalle)
            .Include(i => i.Novedades)
            .FirstOrDefaultAsync(i => i.IdInspeccion == id);
    }

    private static InspeccionResultadoDto MapToResultadoDto(Inspeccion inspeccion)
    {
        var respuestasPorPregunta = inspeccion.Respuestas
            .ToDictionary(respuesta => respuesta.IdPregunta);

        var preguntas = inspeccion.Checklist.Preguntas
            .OrderBy(pregunta => pregunta.Orden)
            .ThenBy(pregunta => pregunta.IdPregunta)
            .Select(pregunta =>
            {
                respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta);
                return new PreguntaEjecucionDto
                {
                    IdPregunta = pregunta.IdPregunta,
                    Pregunta = pregunta.Pregunta,
                    TipoRespuesta = pregunta.TipoRespuesta,
                    Obligatoria = pregunta.Obligatoria,
                    Orden = pregunta.Orden,
                    IdRespuesta = respuesta?.IdRespuesta,
                    Respuesta = respuesta?.Respuesta,
                    Observacion = respuesta?.Observacion
                };
            })
            .ToList();

        return new InspeccionResultadoDto
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Estado = inspeccion.Estado,
            Resultado = inspeccion.Resultado ?? string.Empty,
            FechaInspeccion = inspeccion.FechaInspeccion,
            FechaCierre = inspeccion.FechaCierre,
            Observaciones = inspeccion.Observaciones,
            IdEquipo = inspeccion.IdEquipo,
            CodigoInterno = inspeccion.Equipo.CodigoInterno,
            NombreEquipo = inspeccion.Equipo.NombreEquipo,
            UbicacionNombre = inspeccion.Equipo.Ubicacion == null
                ? null
                : $"{inspeccion.Equipo.Ubicacion.Planta} - {inspeccion.Equipo.Ubicacion.Area}",
            NombreChecklist = inspeccion.Checklist.Nombre,
            VersionChecklist = inspeccion.Checklist.Version,
            NombreUsuario = inspeccion.Usuario.Nombre,
            Preguntas = preguntas,
            TotalNovedades = preguntas.Count(pregunta => pregunta.EsNovedad),
            NovedadesPendientes = inspeccion.Novedades.Count(novedad => novedad.Estado == NovedadEstados.Pendiente),
            Evidencias = inspeccion.Evidencias
                .Where(evidencia => !evidencia.Retirada)
                .OrderBy(evidencia => evidencia.FechaCarga)
                .Select(evidencia => new EvidenciaDto
                {
                    IdEvidencia = evidencia.IdEvidencia,
                    IdInspeccion = evidencia.IdInspeccion,
                    NombreEquipo = inspeccion.Equipo.NombreEquipo,
                    Archivo = evidencia.Archivo,
                    NombreOriginal = evidencia.NombreOriginal,
                    TipoContenido = evidencia.TipoContenido,
                    TamanoBytes = evidencia.TamanoBytes,
                    FechaCarga = evidencia.FechaCarga,
                    IdUsuarioCarga = evidencia.IdUsuarioCarga,
                    UsuarioCarga = evidencia.UsuarioCargaDetalle.Nombre,
                    IdUsuarioInspeccion = inspeccion.IdUsuario,
                    EstadoInspeccion = inspeccion.Estado
                })
                .ToList(),
            TieneFirma = !string.IsNullOrEmpty(inspeccion.FirmaDigital),
            FirmaPngBase64 = inspeccion.FirmaDigital,
            FirmaHash = inspeccion.FirmaHash,
            FirmaNombre = inspeccion.FirmaNombre,
            FirmaSignificado = inspeccion.FirmaSignificado,
            FirmaAlgoritmo = VerificadorIntegridad.NombreAlgoritmo(inspeccion)
        };
    }

    private Task<Inspeccion?> CargarEjecucionAsync(long id) => _context.Inspecciones
        .AsNoTracking()
        .Include(i => i.Equipo)
            .ThenInclude(e => e.Ubicacion)
        .Include(i => i.Usuario)
        .Include(i => i.Checklist)
            .ThenInclude(c => c.Preguntas)
        .Include(i => i.Respuestas)
        .Include(i => i.Evidencias)
            .ThenInclude(e => e.UsuarioCargaDetalle)
        .FirstOrDefaultAsync(i => i.IdInspeccion == id);

    /// <summary>El Operador: su listado son sus inspecciones.</summary>
    private bool EsInspectorSinLecturaGlobal() => !AccesoInspeccion.LeeTodas(_currentUser);

    /// <summary>
    /// Administrador, Supervisor y Estándar, cualquiera, en solo lectura; el Operador, las suyas
    /// (<see cref="AccesoInspeccion"/>).
    /// </summary>
    private bool PuedeLeer(Inspeccion inspeccion) => AccesoInspeccion.PuedeLeer(_currentUser, inspeccion.IdUsuario);

    private bool PuedeModificar(Inspeccion inspeccion) =>
        _currentUser.IsInRole(RolesSistema.Administrador)
        || ((_currentUser.IsInRole(RolesSistema.Operador) || _currentUser.IsInRole(RolesSistema.Estandar))
            && inspeccion.IdUsuario == _currentUser.IdUsuario);

    private static InspeccionEjecucionDto MapToEjecucionDto(Inspeccion inspeccion)
    {
        var respuestasPorPregunta = inspeccion.Respuestas
            .ToDictionary(respuesta => respuesta.IdPregunta);

        var preguntasChecklist = inspeccion.Checklist.Preguntas.ToList();
        var obligatorias = preguntasChecklist.Where(pregunta => pregunta.Obligatoria).ToList();
        var obligatoriasRespondidas = obligatorias.Count(pregunta =>
            respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta)
            && !string.IsNullOrWhiteSpace(respuesta.Respuesta));
        var novedades = preguntasChecklist.Count(pregunta =>
            pregunta.TipoRespuesta == TiposRespuesta.SiNo
            && respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta)
            && string.Equals(respuesta.Respuesta, "No", StringComparison.OrdinalIgnoreCase));

        return new InspeccionEjecucionDto
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdEquipo = inspeccion.IdEquipo,
            CodigoInternoEquipo = inspeccion.Equipo.CodigoInterno,
            NombreEquipo = inspeccion.Equipo.NombreEquipo,
            UbicacionNombre = inspeccion.Equipo.Ubicacion == null
                ? null
                : $"{inspeccion.Equipo.Ubicacion.Planta} - {inspeccion.Equipo.Ubicacion.Area}",
            Criticidad = inspeccion.Equipo.Criticidad,
            TotalObligatorias = obligatorias.Count,
            ObligatoriasRespondidas = obligatoriasRespondidas,
            TotalNovedades = novedades,
            IdChecklist = inspeccion.IdChecklist,
            NombreChecklist = inspeccion.Checklist.Nombre,
            VersionChecklist = inspeccion.Checklist.Version,
            IdUsuario = inspeccion.IdUsuario,
            NombreUsuario = inspeccion.Usuario.Nombre,
            FechaInspeccion = inspeccion.FechaInspeccion,
            Estado = inspeccion.Estado,
            Preguntas = inspeccion.Checklist.Preguntas
                .OrderBy(pregunta => pregunta.Orden)
                .ThenBy(pregunta => pregunta.IdPregunta)
                .Select(pregunta =>
                {
                    respuestasPorPregunta.TryGetValue(pregunta.IdPregunta, out var respuesta);
                    return new PreguntaEjecucionDto
                    {
                        IdPregunta = pregunta.IdPregunta,
                        Pregunta = pregunta.Pregunta,
                        TipoRespuesta = pregunta.TipoRespuesta,
                        Obligatoria = pregunta.Obligatoria,
                        Orden = pregunta.Orden,
                        IdRespuesta = respuesta?.IdRespuesta,
                        Respuesta = respuesta?.Respuesta,
                        Observacion = respuesta?.Observacion
                    };
                })
                .ToList(),
            Evidencias = inspeccion.Evidencias
                .Where(evidencia => !evidencia.Retirada)
                .OrderBy(evidencia => evidencia.FechaCarga)
                .Select(evidencia => new EvidenciaDto
                {
                    IdEvidencia = evidencia.IdEvidencia,
                    IdInspeccion = evidencia.IdInspeccion,
                    NombreEquipo = inspeccion.Equipo.NombreEquipo,
                    Archivo = evidencia.Archivo,
                    NombreOriginal = evidencia.NombreOriginal,
                    TipoContenido = evidencia.TipoContenido,
                    TamanoBytes = evidencia.TamanoBytes,
                    FechaCarga = evidencia.FechaCarga,
                    IdUsuarioCarga = evidencia.IdUsuarioCarga,
                    UsuarioCarga = evidencia.UsuarioCargaDetalle.Nombre,
                    IdUsuarioInspeccion = inspeccion.IdUsuario,
                    EstadoInspeccion = inspeccion.Estado
                })
                .ToList()
        };
    }

    private static InspeccionDto MapToDto(Inspeccion i)
    {
        return new InspeccionDto
        {
            IdInspeccion = i.IdInspeccion,
            IdEquipo = i.IdEquipo,
            NombreEquipo = i.Equipo?.NombreEquipo,
            IdUsuario = i.IdUsuario,
            NombreUsuario = i.Usuario?.Nombre,
            IdChecklist = i.IdChecklist,
            NombreChecklist = i.Checklist?.Nombre,
            FechaInspeccion = i.FechaInspeccion,
            Estado = i.Estado,
            FechaCierre = i.FechaCierre,
            Resultado = i.Resultado,
            Observaciones = i.Observaciones,
            TieneFirma = !string.IsNullOrEmpty(i.FirmaDigital),
            TotalEvidencias = i.Evidencias?.Count(e => !e.Retirada) ?? 0,
            TotalNovedades = i.Novedades?.Count ?? 0,
            NovedadesPendientes = i.Novedades?.Count(n => n.Estado == NovedadEstados.Pendiente) ?? 0,
            MotivoAnulacion = i.MotivoAnulacion,
            FechaAnulacion = i.FechaAnulacion,
            IdUsuarioAnulacion = i.IdUsuarioAnulacion,
            NombreUsuarioAnulacion = i.UsuarioAnulacion?.Nombre
        };
    }
}
