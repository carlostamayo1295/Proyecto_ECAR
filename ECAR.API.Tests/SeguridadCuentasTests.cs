using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ECAR.API.Configuration;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// BE-3, Fase 4 (PLAN_FASE4_TAREAS §3.7 y §3.8, Parte 11 §11.200 y §11.300): bloqueo por intentos,
/// auditoría de accesos, política e historial de contraseñas, restablecer y desbloquear, firma con
/// contraseña e integridad de la inspección.
/// </summary>
public class SeguridadCuentasTests
{
    private const string Clave = "Clave-segura-2026";
    private const string PngValido =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private sealed class Quien(long id, string nombre, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => id;
        public string Nombre => nombre;
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SinActiveDirectory : IActiveDirectoryAuthService
    {
        public Task<bool> AuthenticateAsync(string usuarioAd, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class SinArchivos : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => Task.FromResult("x" + extension);
        public Task<Stream> AbrirAsync(string rutaRelativa) => Task.FromResult<Stream>(new MemoryStream());
        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }

    private static ECARDbContext Base() =>
        new(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration Configuracion() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["JWT:Secret"] = "una-clave-secreta-de-prueba-suficientemente-larga-32b",
        ["JWT:Issuer"] = "ECAR-Pruebas",
        ["JWT:Audience"] = "ECAR-Pruebas"
    }).Build();

    private static AuthService Auth(ECARDbContext db) => new(db, Configuracion(), new SinActiveDirectory(),
        Options.Create(new EcarAuthenticationOptions { Mode = nameof(EcarAuthenticationMode.Local) }));

    private static async Task<Usuario> UsuarioAsync(ECARDbContext db, string correo = "ana@ecar.test", string clave = Clave)
    {
        var usuario = new Usuario { Nombre = "Ana Técnica", Correo = correo, PasswordHash = BCrypt.Net.BCrypt.HashPassword(clave, 4) };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private static LoginDto Login(string clave, string correo = "ana@ecar.test") => new() { CorreoOrUsuarioAD = correo, Password = clave };

    private static List<string> Acciones(ECARDbContext db) =>
        db.Auditoria.AsNoTracking().OrderBy(a => a.IdAuditoria).Select(a => a.Accion).ToList();

    [Fact]
    public async Task Cinco_intentos_fallidos_bloquean_la_cuenta_aunque_despues_la_clave_sea_correcta()
    {
        await using var db = Base();
        var usuario = await UsuarioAsync(db);
        var auth = Auth(db);

        for (var i = 1; i <= 4; i++)
        {
            Assert.Equal(EstadoCredencial.Invalida, (await auth.IniciarSesionAsync(Login("mala"))).Estado);
        }

        var quinto = await auth.IniciarSesionAsync(Login("mala"));
        var conLaBuena = await auth.IniciarSesionAsync(Login(Clave));

        Assert.Equal(EstadoCredencial.Bloqueada, quinto.Estado);
        Assert.Equal(EstadoCredencial.Bloqueada, conLaBuena.Estado);
        Assert.Contains("hora de Colombia", conLaBuena.MensajeBloqueo);
        var guardado = await db.Usuarios.AsNoTracking().SingleAsync();
        Assert.True(guardado.BloqueadoHasta > DateTime.UtcNow.AddMinutes(29));
        Assert.Contains(AuditoriaAcciones.CuentaBloqueada, Acciones(db));
        Assert.Equal(5, Acciones(db).Count(a => a == AuditoriaAcciones.LoginFallido));
        Assert.DoesNotContain(Clave, string.Join(" ", db.Auditoria.Select(a => a.ValorNuevo)));
    }

    [Fact]
    public async Task El_login_correcto_reinicia_el_contador_y_queda_en_la_auditoria()
    {
        await using var db = Base();
        await UsuarioAsync(db);
        var auth = Auth(db);
        await auth.IniciarSesionAsync(Login("mala"));

        var resultado = await auth.IniciarSesionAsync(Login(Clave));

        Assert.Equal(EstadoCredencial.Valida, resultado.Estado);
        Assert.False(resultado.Sesion!.DebeCambiarPassword);
        var guardado = await db.Usuarios.AsNoTracking().SingleAsync();
        Assert.Equal(0, guardado.IntentosFallidos);
        Assert.NotNull(guardado.UltimoAcceso);
        Assert.NotNull(guardado.FechaCambioPassword); // la caducidad empieza a contar
        Assert.Equal(AuditoriaAcciones.LoginExitoso, Acciones(db).Last());
    }

    [Fact]
    public async Task El_controlador_responde_401_generico_y_423_con_la_hora()
    {
        await using var db = Base();
        await UsuarioAsync(db);
        var controlador = new AuthController(Auth(db), db, new Quien(0, ""), Fabrica.Seguridad());

        var inexistente = await controlador.Login(Login(Clave, "nadie@ecar.test"));
        for (var i = 0; i < 5; i++)
        {
            await controlador.Login(Login("mala"));
        }

        var bloqueada = await controlador.Login(Login(Clave));

        var noAutorizado = Assert.IsType<UnauthorizedObjectResult>(inexistente.Result);
        Assert.Equal(MensajesCuenta.CredencialesIncorrectas, Assert.IsType<ApiResponse<LoginResponseDto>>(noAutorizado.Value).Message);
        var bloqueo = Assert.IsType<ObjectResult>(bloqueada.Result);
        Assert.Equal(StatusCodes.Status423Locked, bloqueo.StatusCode);
        Assert.StartsWith("Cuenta bloqueada hasta las", Assert.IsType<ApiResponse<LoginResponseDto>>(bloqueo.Value).Message);
    }

    [Fact]
    public async Task Una_contrasena_caducada_obliga_a_cambiarla_y_el_token_lo_dice()
    {
        await using var db = Base();
        var usuario = await UsuarioAsync(db);
        usuario.FechaCambioPassword = DateTime.UtcNow.AddDays(-91);
        await db.SaveChangesAsync();

        var resultado = await Auth(db).IniciarSesionAsync(Login(Clave));

        Assert.True(resultado.Sesion!.DebeCambiarPassword);
        Assert.True(resultado.Sesion.PasswordExpiraEl < DateTime.UtcNow);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(resultado.Sesion.Token);
        Assert.Equal("1", jwt.Claims.Single(c => c.Type == AuthService.ClaimDebeCambiarPassword).Value);
    }

    private static AuthController ControladorCuenta(ECARDbContext db, Usuario usuario) =>
        new(Auth(db), db, new Quien(usuario.IdUsuario, usuario.Nombre, "Técnico"), Fabrica.Seguridad());

    [Fact]
    public async Task Cambiar_la_contrasena_aplica_politica_e_historial()
    {
        await using var db = Base();
        var usuario = await UsuarioAsync(db);
        var sello = usuario.SecurityStamp;
        var controlador = ControladorCuenta(db, usuario);

        var actualMala = await controlador.CambiarPassword(new CambiarPasswordDto { Actual = "mala", Nueva = "Nueva-clave-2026" });
        var corta = await controlador.CambiarPassword(new CambiarPasswordDto { Actual = Clave, Nueva = "abc12" });
        var sinNumeros = await controlador.CambiarPassword(new CambiarPasswordDto { Actual = Clave, Nueva = "solo-letras-largas" });
        var igual = await controlador.CambiarPassword(new CambiarPasswordDto { Actual = Clave, Nueva = Clave });
        var buena = await controlador.CambiarPassword(new CambiarPasswordDto { Actual = Clave, Nueva = "Nueva-clave-2026" });

        Assert.IsType<BadRequestObjectResult>(actualMala.Result);
        Assert.IsType<BadRequestObjectResult>(corta.Result);
        Assert.IsType<BadRequestObjectResult>(sinNumeros.Result);
        Assert.IsType<BadRequestObjectResult>(igual.Result);
        Assert.IsType<OkObjectResult>(buena.Result);

        var guardado = await db.Usuarios.AsNoTracking().Include(u => u.HistorialPasswords).SingleAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify("Nueva-clave-2026", guardado.PasswordHash));
        Assert.Single(guardado.HistorialPasswords);
        Assert.NotEqual(sello, guardado.SecurityStamp); // los tokens anteriores dejan de valer
        Assert.Contains(AuditoriaAcciones.CambioPassword, Acciones(db));

        // Volver a la contraseña anterior está prohibido por el historial.
        var vuelta = await ControladorCuenta(db, guardado).CambiarPassword(
            new CambiarPasswordDto { Actual = "Nueva-clave-2026", Nueva = Clave });
        Assert.IsType<BadRequestObjectResult>(vuelta.Result);
    }

    [Fact]
    public async Task Restablecer_genera_una_temporal_que_obliga_a_cambiarla_al_entrar()
    {
        await using var db = Base();
        var usuario = await UsuarioAsync(db);

        var respuesta = await Fabrica.Usuarios(db).RestablecerPassword(usuario.IdUsuario, new MotivoDto { Motivo = "El usuario olvidó su contraseña" });

        var temporal = Assert.IsType<ApiResponse<PasswordTemporalDto>>(Assert.IsType<OkObjectResult>(respuesta.Result).Value).Data!;
        Assert.Empty(PoliticaPassword.Validar(temporal.PasswordTemporal, new SeguridadOptions()));
        var login = await Auth(db).IniciarSesionAsync(Login(temporal.PasswordTemporal));
        Assert.True(login.Sesion!.DebeCambiarPassword);
        Assert.Contains(AuditoriaAcciones.RestablecerPassword, Acciones(db));
    }

    [Fact]
    public async Task Desbloquear_solo_aplica_a_una_cuenta_bloqueada()
    {
        await using var db = Base();
        var usuario = await UsuarioAsync(db);
        var controlador = Fabrica.Usuarios(db);
        var motivo = new MotivoDto { Motivo = "Verificada la identidad por teléfono" };

        Assert.IsType<ConflictObjectResult>((await controlador.DesbloquearUsuario(usuario.IdUsuario, motivo)).Result);

        usuario.BloqueadoHasta = DateTime.UtcNow.AddMinutes(20);
        await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>((await controlador.DesbloquearUsuario(usuario.IdUsuario, motivo)).Result);
        Assert.Null((await db.Usuarios.AsNoTracking().SingleAsync()).BloqueadoHasta);
        Assert.Equal(EstadoCredencial.Valida, (await Auth(db).IniciarSesionAsync(Login(Clave))).Estado);
    }

    [Fact]
    public async Task Editar_un_usuario_ya_no_fija_contrasenas_ni_cambia_el_correo_de_quien_firmo()
    {
        await using var db = Base();
        var usuario = await UsuarioAsync(db);
        var equipo = new Equipo { CodigoInterno = "E", ActivoFijo = "A", NombreEquipo = "Equipo" };
        var checklist = new Checklist { Nombre = "C", Version = "1" };
        db.AddRange(equipo, checklist);
        await db.SaveChangesAsync();
        db.Inspecciones.Add(new Inspeccion
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = usuario.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, Estado = InspeccionEstados.Cerrada, FirmaDigital = PngValido
        });
        await db.SaveChangesAsync();
        var controlador = Fabrica.Usuarios(db);

        var conPassword = await controlador.UpdateUsuario(usuario.IdUsuario, new UpdateUsuarioDto { Password = "Otra-clave-2026" });
        var otroCorreo = await controlador.UpdateUsuario(usuario.IdUsuario, new UpdateUsuarioDto { Correo = "otra@ecar.test" });
        var mismoCorreo = await controlador.UpdateUsuario(usuario.IdUsuario, new UpdateUsuarioDto { Correo = "ana@ecar.test", Nombre = "Ana T." });

        Assert.IsType<BadRequestObjectResult>(conPassword.Result);
        Assert.IsType<BadRequestObjectResult>(otroCorreo.Result);
        Assert.IsType<OkObjectResult>(mismoCorreo.Result);
    }

    [Fact]
    public async Task Crear_un_usuario_con_contrasena_aplica_la_politica_y_obliga_a_cambiarla()
    {
        await using var db = Base();
        var controlador = Fabrica.Usuarios(db);

        var debil = await controlador.CreateUsuario(new CreateUsuarioDto { Nombre = "Eva", Correo = "eva@ecar.test", Password = "123456" });
        var buena = await controlador.CreateUsuario(new CreateUsuarioDto { Nombre = "Eva", Correo = "eva@ecar.test", Password = "Inicial-2026x" });

        Assert.IsType<BadRequestObjectResult>(debil.Result);
        Assert.IsType<CreatedAtActionResult>(buena.Result);
        Assert.True((await db.Usuarios.AsNoTracking().SingleAsync()).DebeCambiarPassword);
    }

    private static async Task<(ECARDbContext Db, Usuario Tecnico, Inspeccion Inspeccion)> InspeccionParaFirmarAsync()
    {
        var db = Base();
        var tecnico = await UsuarioAsync(db);
        var pregunta = new PreguntaChecklist { Pregunta = "¿Limpio?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 1 };
        var checklist = new Checklist { Nombre = "C", Version = "1", Preguntas = [pregunta] };
        var equipo = new Equipo { CodigoInterno = "E", ActivoFijo = "A", NombreEquipo = "Equipo" };
        db.AddRange(checklist, equipo);
        await db.SaveChangesAsync();
        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Respuestas = [new RespuestaInspeccion { IdPregunta = pregunta.IdPregunta, Respuesta = "Si" }]
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, tecnico, inspeccion);
    }

    private static InspeccionesController Inspecciones(ECARDbContext db, Usuario tecnico, string rol = "Técnico") =>
        Fabrica.Inspecciones(db, new Quien(tecnico.IdUsuario, tecnico.Nombre, rol), new SinArchivos(), Auth(db));

    [Fact]
    public async Task Firmar_exige_la_contrasena_y_una_erronea_es_400_sin_lista_y_cuenta_como_fallo()
    {
        var (db, tecnico, inspeccion) = await InspeccionParaFirmarAsync();
        await using var _ = db;
        var controlador = Inspecciones(db, tecnico);

        var sinClave = await controlador.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto { FirmaPngBase64 = PngValido });
        var mala = await controlador.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto { FirmaPngBase64 = PngValido, Password = "mala" });

        Assert.IsType<BadRequestObjectResult>(sinClave.Result);
        var error = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(Assert.IsType<BadRequestObjectResult>(mala.Result).Value);
        Assert.Equal("Contraseña incorrecta", error.Message);
        Assert.Null(error.Errors); // así el cliente distingue la contraseña de lo que falta por responder
        Assert.Equal(1, (await db.Usuarios.AsNoTracking().SingleAsync()).IntentosFallidos);
        Assert.Equal(InspeccionEstados.EnCurso, (await db.Inspecciones.AsNoTracking().SingleAsync()).Estado);
    }

    [Fact]
    public async Task Firmar_con_la_cuenta_bloqueada_es_423()
    {
        var (db, tecnico, inspeccion) = await InspeccionParaFirmarAsync();
        await using var _ = db;
        var bloqueado = await db.Usuarios.SingleAsync();
        bloqueado.BloqueadoHasta = DateTime.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();

        var resultado = await Inspecciones(db, tecnico).FirmarInspeccion(inspeccion.IdInspeccion,
            new FirmarInspeccionDto { FirmaPngBase64 = PngValido, Password = Clave });

        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(resultado.Result).StatusCode);
    }

    [Fact]
    public async Task Firmar_guarda_la_manifestacion_y_la_integridad_detecta_un_cambio_posterior()
    {
        var (db, tecnico, inspeccion) = await InspeccionParaFirmarAsync();
        await using var _ = db;

        var firmado = await Inspecciones(db, tecnico).FirmarInspeccion(inspeccion.IdInspeccion,
            new FirmarInspeccionDto { FirmaPngBase64 = PngValido, Password = Clave });
        var resultado = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(Assert.IsType<OkObjectResult>(firmado.Result).Value).Data!;
        Assert.Equal("Ana Técnica", resultado.FirmaNombre);
        Assert.Equal("Responsable de la ejecución de la inspección", resultado.FirmaSignificado);
        Assert.Equal(FirmaInspeccion.AlgoritmoHmacV1, resultado.FirmaAlgoritmo);

        var auditor = Inspecciones(db, tecnico, "Auditor");
        var integra = await auditor.VerificarIntegridad(inspeccion.IdInspeccion);
        Assert.True(Assert.IsType<ApiResponse<IntegridadInspeccionDto>>(Assert.IsType<OkObjectResult>(integra.Result).Value).Data!.Integra);

        // Alguien cambia la respuesta directamente en la base, después de firmada.
        var respuesta = await db.RespuestasInspeccion.SingleAsync();
        respuesta.Respuesta = "No";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var alterada = await auditor.VerificarIntegridad(inspeccion.IdInspeccion);
        Assert.False(Assert.IsType<ApiResponse<IntegridadInspeccionDto>>(Assert.IsType<OkObjectResult>(alterada.Result).Value).Data!.Integra);
        Assert.Equal(2, Acciones(db).Count(a => a == AuditoriaAcciones.VerificacionIntegridad));
    }

    [Fact]
    public async Task Una_inspeccion_firmada_antes_de_la_Fase_4_se_verifica_con_SHA256()
    {
        var (db, tecnico, inspeccion) = await InspeccionParaFirmarAsync();
        await using var _ = db;
        var legado = await db.Inspecciones.Include(i => i.Respuestas).Include(i => i.Evidencias).SingleAsync();
        var cierre = DateTime.UtcNow;
        legado.Estado = InspeccionEstados.Cerrada;
        legado.FechaCierre = cierre;
        legado.FirmaDigital = PngValido;
        legado.FirmaHash = FirmaInspeccion.CalcularHash(legado, legado.Respuestas, legado.Evidencias, cierre, PngValido);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var integra = await Inspecciones(db, tecnico, "Auditor").VerificarIntegridad(inspeccion.IdInspeccion);

        var dto = Assert.IsType<ApiResponse<IntegridadInspeccionDto>>(Assert.IsType<OkObjectResult>(integra.Result).Value).Data!;
        Assert.True(dto.Integra);
        Assert.Equal(FirmaInspeccion.AlgoritmoSha256V0, dto.Algoritmo);
    }

    [Fact]
    public async Task Con_una_contrasena_temporal_solo_se_puede_usar_api_auth()
    {
        var siguienteLlamado = false;
        var middleware = new CambioPasswordObligatorio(_ => { siguienteLlamado = true; return Task.CompletedTask; });
        HttpContext Peticion(string ruta)
        {
            var http = new DefaultHttpContext();
            http.Request.Path = ruta;
            http.Response.Body = new MemoryStream();
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthService.ClaimDebeCambiarPassword, "1")], "Bearer"));
            return http;
        }

        var equipos = Peticion("/api/equipos");
        await middleware.InvokeAsync(equipos);
        Assert.Equal(StatusCodes.Status403Forbidden, equipos.Response.StatusCode);
        Assert.False(siguienteLlamado);

        await middleware.InvokeAsync(Peticion("/api/auth/cambiar-password"));
        Assert.True(siguienteLlamado);
    }

    [Fact]
    public void La_politica_publica_los_valores_del_plan()
    {
        var controlador = new AuthController(new Fabrica.AuthFalso(), Base(), new Quien(0, ""), Fabrica.Seguridad());

        var politica = Assert.IsType<ApiResponse<PoliticaSeguridadDto>>(Assert.IsType<OkObjectResult>(controlador.GetPolitica().Result).Value).Data!;

        Assert.Equal(10, politica.LongitudMinimaPassword);
        Assert.Equal(5, politica.MaxIntentosFallidos);
        Assert.Equal(15, politica.MinutosInactividad);
    }
}
