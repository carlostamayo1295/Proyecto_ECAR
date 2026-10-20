using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.API.Services.Notificaciones;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Novedades: firmar una inspección con respuestas "No" crea una novedad pendiente por cada una,
/// deja la inspección "NovedadesPendientes" con resultado "Pendiente" y avisa por correo; cada
/// novedad se resuelve con firma (lo que se hizo, la firma dibujada y la contraseña) por cualquier
/// Técnico o Administrador, y al resolverse la última la inspección queda Cerrada y "ConNovedad".
/// La atomicidad real contra SQL Server está en ECAR.IntegrationTests (NovedadesSqlServerTests).
/// </summary>
public class NovedadesTests
{
    private const string FirmaPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const string Solucion = "Se limpió el plato y se niveló la balanza";
    private const string Motivo = "Registrada por error: era otro equipo";

    private sealed class Usuario1(long id, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => id;
        public string Nombre => "Usuario de pruebas";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class Almacen : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => Task.FromResult($"{idInspeccion}/f{extension}");
        public Task<Stream> AbrirAsync(string rutaRelativa) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }

    private sealed class NotificadorFalso : INotificadorNovedades
    {
        public List<long> Encolados { get; } = new();
        public void Encolar(IEnumerable<long> idsNovedad) => Encolados.AddRange(idsNovedad);
    }

    /// <summary>Ejecuta <see cref="AlGuardarNovedades"/> justo antes de guardar novedades nuevas.</summary>
    private sealed class InterceptorDeNovedades : SaveChangesInterceptor
    {
        public Action? AlGuardarNovedades { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Novedad>().Any(n => n.State == EntityState.Added))
            {
                AlGuardarNovedades?.Invoke();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record Escenario(
        ECARDbContext Db,
        InMemoryDatabaseRoot Raiz,
        string Nombre,
        InterceptorDeNovedades Interceptor,
        NotificadorFalso Notificador,
        Usuario Inspector,
        Usuario OtroTecnico,
        Usuario Admin,
        Equipo Equipo,
        PreguntaChecklist Limpieza,
        PreguntaChecklist Nivel,
        PreguntaChecklist Etiqueta,
        PreguntaChecklist Lectura);

    private static ECARDbContext Contexto(InMemoryDatabaseRoot raiz, string nombre, IInterceptor? interceptor = null)
    {
        var opciones = new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(nombre, raiz);
        if (interceptor != null)
        {
            opciones.AddInterceptors(interceptor);
        }

        return new ECARDbContext(opciones.Options);
    }

    /// <summary>Balanza de criticidad Alta y un checklist con tres preguntas Sí/No y una de texto.</summary>
    private static async Task<Escenario> SeedAsync()
    {
        var raiz = new InMemoryDatabaseRoot();
        var nombre = Guid.NewGuid().ToString();
        var interceptor = new InterceptorDeNovedades();
        var db = Contexto(raiz, nombre, interceptor);

        Usuario Nuevo(string nombreUsuario, string correo, string rol)
        {
            var usuario = new Usuario { Nombre = nombreUsuario, Correo = correo, PasswordHash = FirmaPruebas.PasswordHash, Activo = true };
            usuario.UsuarioRoles.Add(new UsuarioRol { Rol = new Rol { Nombre = rol } });
            return usuario;
        }

        var inspector = Nuevo("Ana Inspectora", "ana@ecar.test", "Técnico");
        var otro = Nuevo("Luis Técnico", "luis@ecar.test", "Técnico");
        var admin = Nuevo("Carla Admin", "carla@ecar.test", "Administrador");
        var equipo = new Equipo
        {
            CodigoInterno = "BAL-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Criticidad = Criticidades.Alta, Activo = true,
            Ubicacion = new Ubicacion { Planta = "Planta 1", Area = "Pesaje" }
        };
        var limpieza = new PreguntaChecklist { Pregunta = "¿Está limpia?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 1 };
        var nivel = new PreguntaChecklist { Pregunta = "¿Está nivelada?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 2 };
        var etiqueta = new PreguntaChecklist { Pregunta = "¿Tiene etiqueta?", TipoRespuesta = TiposRespuesta.SiNo, Orden = 3 };
        var lectura = new PreguntaChecklist { Pregunta = "Lectura", TipoRespuesta = TiposRespuesta.Texto, Orden = 4 };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true, Preguntas = [limpieza, nivel, etiqueta, lectura] };

        db.AddRange(inspector, otro, admin, equipo, checklist);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Escenario(db, raiz, nombre, interceptor, new NotificadorFalso(), inspector, otro, admin, equipo,
            limpieza, nivel, etiqueta, lectura);
    }

    private static RespuestaInspeccion Si(PreguntaChecklist pregunta) => new() { IdPregunta = pregunta.IdPregunta, Respuesta = "Si" };

    private static RespuestaInspeccion No(PreguntaChecklist pregunta, string observacion) =>
        new() { IdPregunta = pregunta.IdPregunta, Respuesta = "No", Observacion = observacion };

    private static async Task<Inspeccion> EnCursoAsync(Escenario e, params RespuestaInspeccion[] respuestas)
    {
        var inspeccion = new Inspeccion
        {
            IdEquipo = e.Equipo.IdEquipo,
            IdChecklist = e.Limpieza.IdChecklist,
            IdUsuario = e.Inspector.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Respuestas = respuestas
        };
        e.Db.Inspecciones.Add(inspeccion);
        await e.Db.SaveChangesAsync();
        e.Db.ChangeTracker.Clear();
        return inspeccion;
    }

    private static InspeccionesController Inspecciones(Escenario e, Usuario usuario, string rol = "Técnico") =>
        new(e.Db, new Usuario1(usuario.IdUsuario, rol), new InspeccionService(e.Db), new Almacen(),
            NullLogger<InspeccionesController>.Instance, new MotivoCambio(), e.Notificador);

    private static NovedadesController Novedades(Escenario e, Usuario usuario, string rol = "Técnico") =>
        new(e.Db, new Usuario1(usuario.IdUsuario, rol), new MotivoCambio(), e.Notificador);

    private static async Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> FirmarAsync(Escenario e, long idInspeccion)
    {
        var resultado = await Inspecciones(e, e.Inspector).FirmarConPassword(e.Db, idInspeccion,
            new FirmarInspeccionDto { FirmaPngBase64 = FirmaPng }, new Almacen());
        e.Db.ChangeTracker.Clear();
        return resultado;
    }

    /// <summary>Inspección firmada con un "No" en limpieza y otro en nivel: dos novedades pendientes.</summary>
    private static async Task<(Inspeccion Inspeccion, List<Novedad> Novedades)> FirmadaConDosNovedadesAsync(Escenario e)
    {
        var inspeccion = await EnCursoAsync(e, No(e.Limpieza, "Polvo en el plato"), No(e.Nivel, " Burbuja corrida "), Si(e.Etiqueta));
        Assert.IsType<OkObjectResult>((await FirmarAsync(e, inspeccion.IdInspeccion)).Result);
        var novedades = await e.Db.Novedades.AsNoTracking().OrderBy(n => n.IdPregunta).ToListAsync();
        return (inspeccion, novedades);
    }

    private static async Task<ActionResult<ApiResponse<NovedadDto>>> ResolverAsync(Escenario e, Usuario usuario, long idNovedad,
        string? password = FirmaPruebas.Password, string solucion = Solucion, string firma = FirmaPng, string rol = "Técnico")
    {
        var resultado = await Novedades(e, usuario, rol).ResolverNovedad(idNovedad,
            new ResolverNovedadDto { Solucion = solucion, FirmaPngBase64 = firma, Password = password },
            FirmaPruebas.CrearAuthService(e.Db), FirmaPruebas.CrearVerificador(new Almacen()));
        e.Db.ChangeTracker.Clear();
        return resultado;
    }

    private static ApiResponse<T> Respuesta<T>(IConvertToActionResult accion) =>
        Assert.IsType<ApiResponse<T>>(Assert.IsAssignableFrom<ObjectResult>(accion.Convert()).Value);

    private static Task<Inspeccion> InspeccionAsync(Escenario e, long id) =>
        e.Db.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == id);

    // ------------------------------------------------------------------ al firmar la inspección

    [Fact]
    public async Task Firmar_con_dos_No_crea_dos_novedades_pendientes_y_la_inspeccion_queda_pendiente()
    {
        var e = await SeedAsync();
        var inspeccion = await EnCursoAsync(e, No(e.Limpieza, "Polvo en el plato"), No(e.Nivel, " Burbuja corrida "), Si(e.Etiqueta));

        var accion = await FirmarAsync(e, inspeccion.IdInspeccion);

        var respuesta = Respuesta<InspeccionResultadoDto>(accion);
        Assert.Contains("2 novedad(es)", respuesta.Message);
        var firmada = await InspeccionAsync(e, inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.NovedadesPendientes, firmada.Estado);
        Assert.Equal(InspeccionResultados.Pendiente, firmada.Resultado);
        Assert.Equal(VerificadorIntegridad.AlgoritmoHmacV2, firmada.FirmaAlgoritmo);
        Assert.NotNull(firmada.FechaCierre);

        var novedades = await e.Db.Novedades.AsNoTracking().OrderBy(n => n.IdPregunta).ToListAsync();
        var respuestas = await e.Db.RespuestasInspeccion.AsNoTracking().ToDictionaryAsync(r => r.IdPregunta, r => r.IdRespuesta);
        Assert.Equal(["¿Está limpia?: Polvo en el plato", "¿Está nivelada?: Burbuja corrida"], novedades.Select(n => n.Descripcion));
        Assert.All(novedades, n =>
        {
            Assert.Equal(NovedadEstados.Pendiente, n.Estado);
            Assert.Equal(Criticidades.Alta, n.Criticidad);
            Assert.Equal(e.Inspector.IdUsuario, n.IdUsuarioRegistro);
            Assert.Equal(respuestas[n.IdPregunta], n.IdRespuesta);
            Assert.Equal(firmada.FechaCierre, n.FechaRegistro);
            Assert.Null(n.FechaNotificacion);
        });

        // Un correo por novedad, en cola después de guardar.
        Assert.Equal(novedades.Select(n => n.IdNovedad), e.Notificador.Encolados);
    }

    [Fact]
    public async Task Firmar_sin_No_cierra_la_inspeccion_conforme_y_no_envia_correos()
    {
        var e = await SeedAsync();
        var inspeccion = await EnCursoAsync(e, Si(e.Limpieza), Si(e.Nivel),
            new RespuestaInspeccion { IdPregunta = e.Lectura.IdPregunta, Respuesta = "No" });

        var accion = await FirmarAsync(e, inspeccion.IdInspeccion);

        Assert.IsType<OkObjectResult>(accion.Result);
        var firmada = await InspeccionAsync(e, inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.Cerrada, firmada.Estado);
        Assert.Equal(InspeccionResultados.Conforme, firmada.Resultado);
        // Un "No" escrito en una pregunta de texto no es novedad.
        Assert.False(await e.Db.Novedades.AnyAsync());
        Assert.Empty(e.Notificador.Encolados);
    }

    [Fact]
    public async Task Firmar_dos_veces_la_segunda_da_409_y_no_duplica_ni_reenvia()
    {
        var e = await SeedAsync();
        var (inspeccion, _) = await FirmadaConDosNovedadesAsync(e);

        var segunda = await FirmarAsync(e, inspeccion.IdInspeccion);

        Assert.IsType<ConflictObjectResult>(segunda.Result);
        Assert.Equal(2, await e.Db.Novedades.CountAsync());
        Assert.Equal(2, e.Notificador.Encolados.Count);
    }

    [Fact]
    public async Task Si_falla_guardar_las_novedades_la_inspeccion_no_queda_firmada_ni_sale_correo()
    {
        var e = await SeedAsync();
        var inspeccion = await EnCursoAsync(e, No(e.Limpieza, "Polvo"), Si(e.Nivel));
        e.Interceptor.AlGuardarNovedades = () => throw new InvalidOperationException("Fallo simulado");

        await Assert.ThrowsAsync<InvalidOperationException>(() => FirmarAsync(e, inspeccion.IdInspeccion));

        await using var lectura = Contexto(e.Raiz, e.Nombre);
        var guardada = await lectura.Inspecciones.SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.FirmaDigital);
        Assert.False(await lectura.Novedades.AnyAsync());
        Assert.Empty(e.Notificador.Encolados);
    }

    [Fact]
    public async Task Con_novedades_pendientes_la_inspeccion_ya_no_admite_cambios()
    {
        var e = await SeedAsync();
        var (inspeccion, _) = await FirmadaConDosNovedadesAsync(e);
        var controlador = Inspecciones(e, e.Inspector);

        var editar = await controlador.UpdateInspeccion(inspeccion.IdInspeccion, new UpdateInspeccionDto { Observaciones = "Cambio" });
        var anular = await controlador.AnularInspeccion(inspeccion.IdInspeccion, new MotivoDto { Motivo = Motivo });
        var resultado = await controlador.GetResultado(inspeccion.IdInspeccion);

        Assert.IsType<ConflictObjectResult>(editar.Result);
        Assert.IsType<ConflictObjectResult>(anular.Result);
        var dto = Respuesta<InspeccionResultadoDto>(resultado).Data!;
        Assert.Equal(2, dto.NovedadesPendientes);
        Assert.Equal(InspeccionResultados.Pendiente, dto.Resultado);
    }

    // ------------------------------------------------------------------ resolver

    [Fact]
    public async Task Otro_tecnico_la_resuelve_con_su_firma_y_la_inspeccion_espera_a_la_otra()
    {
        var e = await SeedAsync();
        var (inspeccion, novedades) = await FirmadaConDosNovedadesAsync(e);

        var accion = await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad, solucion: "  " + Solucion + "  ");

        var respuesta = Respuesta<NovedadDto>(accion);
        Assert.True(respuesta.Success);
        Assert.Contains("1 novedad(es) pendiente(s)", respuesta.Message);
        var resuelta = await e.Db.Novedades.AsNoTracking().SingleAsync(n => n.IdNovedad == novedades[0].IdNovedad);
        Assert.Equal(NovedadEstados.Resuelta, resuelta.Estado);
        Assert.Equal(Solucion, resuelta.Solucion);
        Assert.Equal(e.OtroTecnico.IdUsuario, resuelta.IdUsuarioResolucion);
        Assert.Equal("Luis Técnico", resuelta.FirmaNombre);
        Assert.Equal(VerificadorIntegridad.SignificadoResolucion, resuelta.FirmaSignificado);
        Assert.Equal(VerificadorIntegridad.AlgoritmoNovedad, resuelta.FirmaAlgoritmo);
        Assert.Equal(FirmaPng, resuelta.FirmaDigital);
        Assert.Equal(64, resuelta.FirmaHash!.Length);
        Assert.NotNull(resuelta.FechaResolucion);

        var firmada = await InspeccionAsync(e, inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.NovedadesPendientes, firmada.Estado);
        Assert.Equal(InspeccionResultados.Pendiente, firmada.Resultado);
        Assert.Contains(await e.Db.Auditoria.AsNoTracking().ToListAsync(),
            a => a.Tabla == "Novedades" && a.RegistroId == resuelta.IdNovedad && a.Accion == AuditoriaAcciones.Resolver);
    }

    [Fact]
    public async Task Al_resolver_la_ultima_la_inspeccion_queda_cerrada_con_novedad_y_sus_sellos_siguen_integros()
    {
        var e = await SeedAsync();
        var (inspeccion, novedades) = await FirmadaConDosNovedadesAsync(e);

        await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad);
        var ultima = await ResolverAsync(e, e.Admin, novedades[1].IdNovedad, rol: "Administrador");

        Assert.Contains($"La inspección #{inspeccion.IdInspeccion} quedó cerrada", Respuesta<NovedadDto>(ultima).Message);
        var cerrada = await InspeccionAsync(e, inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.Cerrada, cerrada.Estado);
        Assert.Equal(InspeccionResultados.ConNovedad, cerrada.Resultado);
        Assert.Contains(await e.Db.Auditoria.AsNoTracking().ToListAsync(),
            a => a.Tabla == "Inspecciones" && a.RegistroId == inspeccion.IdInspeccion && a.Accion == AuditoriaAcciones.Cerrar);

        // El resultado cambió después de firmar, pero no forma parte del sello v2.
        var integridadInspeccion = await Inspecciones(e, e.Admin, "Auditor")
            .VerificarIntegridad(inspeccion.IdInspeccion, FirmaPruebas.CrearVerificador(new Almacen()));
        Assert.True(Respuesta<IntegridadInspeccionDto>(integridadInspeccion).Data!.Integra);
        foreach (var novedad in novedades)
        {
            var integridad = await Novedades(e, e.Admin, "Auditor")
                .VerificarIntegridad(novedad.IdNovedad, FirmaPruebas.CrearVerificador(new Almacen()));
            Assert.True(Respuesta<IntegridadInspeccionDto>(integridad).Data!.Integra);
        }
    }

    [Fact]
    public async Task Con_la_contrasena_erronea_responde_400_sin_lista_y_no_la_resuelve()
    {
        var e = await SeedAsync();
        var (_, novedades) = await FirmadaConDosNovedadesAsync(e);

        var mala = await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad, password: "NoEsLaClave1");
        var vacia = await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad, password: "");

        // Sin Errors: el diálogo de firma del cliente se queda abierto y no se pierde la firma.
        var respuesta = Respuesta<NovedadDto>(mala);
        Assert.IsType<BadRequestObjectResult>(mala.Result);
        Assert.Equal(NovedadesController.MensajePasswordIncorrecta, respuesta.Message);
        Assert.Null(respuesta.Errors);
        Assert.Equal(NovedadesController.MensajePasswordRequerida, Respuesta<NovedadDto>(vacia).Message);
        Assert.Equal(NovedadEstados.Pendiente, (await e.Db.Novedades.AsNoTracking().SingleAsync(n => n.IdNovedad == novedades[0].IdNovedad)).Estado);
    }

    [Fact]
    public async Task Una_cuenta_bloqueada_responde_423()
    {
        var e = await SeedAsync();
        var (_, novedades) = await FirmadaConDosNovedadesAsync(e);
        var otro = await e.Db.Usuarios.SingleAsync(u => u.IdUsuario == e.OtroTecnico.IdUsuario);
        otro.BloqueadoHasta = DateTime.UtcNow.AddMinutes(10);
        await e.Db.SaveChangesAsync();
        e.Db.ChangeTracker.Clear();

        var accion = await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad);

        Assert.Equal(423, Assert.IsType<ObjectResult>(accion.Result).StatusCode);
    }

    [Fact]
    public async Task Sin_lo_que_se_hizo_o_con_una_firma_que_no_es_PNG_responde_400_con_la_lista()
    {
        var e = await SeedAsync();
        var (_, novedades) = await FirmadaConDosNovedadesAsync(e);

        var accion = await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad, solucion: "   corto  ", firma: "no-es-png");

        var respuesta = Respuesta<NovedadDto>(accion);
        Assert.IsType<BadRequestObjectResult>(accion.Result);
        Assert.Equal(2, respuesta.Errors!.Count);
        Assert.Contains(NovedadesController.MensajeSolucion, respuesta.Errors);
        Assert.Equal(NovedadEstados.Pendiente, (await e.Db.Novedades.AsNoTracking().SingleAsync(n => n.IdNovedad == novedades[0].IdNovedad)).Estado);
    }

    [Fact]
    public async Task Una_resuelta_no_se_vuelve_a_resolver_ni_se_anula()
    {
        var e = await SeedAsync();
        var (_, novedades) = await FirmadaConDosNovedadesAsync(e);
        await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad);

        var otraVez = await ResolverAsync(e, e.Inspector, novedades[0].IdNovedad);
        var anular = await Novedades(e, e.Admin, "Administrador").AnularNovedad(novedades[0].IdNovedad, new MotivoDto { Motivo = Motivo });
        var inexistente = await ResolverAsync(e, e.Inspector, 999);

        Assert.IsType<ConflictObjectResult>(otraVez.Result);
        Assert.IsType<ConflictObjectResult>(anular.Result);
        Assert.IsType<NotFoundObjectResult>(inexistente.Result);
        Assert.Equal("Luis Técnico", (await e.Db.Novedades.AsNoTracking().SingleAsync(n => n.IdNovedad == novedades[0].IdNovedad)).FirmaNombre);
    }

    // ------------------------------------------------------------------ anular

    [Fact]
    public async Task El_administrador_anula_con_motivo_y_si_era_la_ultima_cierra_la_inspeccion()
    {
        var e = await SeedAsync();
        var (inspeccion, novedades) = await FirmadaConDosNovedadesAsync(e);
        await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad);

        var sinMotivo = await Novedades(e, e.Admin, "Administrador").AnularNovedad(novedades[1].IdNovedad, new MotivoDto { Motivo = "corto" });
        var anular = await Novedades(e, e.Admin, "Administrador").AnularNovedad(novedades[1].IdNovedad, new MotivoDto { Motivo = Motivo });

        Assert.IsType<BadRequestObjectResult>(sinMotivo.Result);
        Assert.IsType<OkObjectResult>(anular.Result);
        var anulada = await e.Db.Novedades.AsNoTracking().SingleAsync(n => n.IdNovedad == novedades[1].IdNovedad);
        Assert.Equal(NovedadEstados.Anulada, anulada.Estado);
        Assert.Equal(Motivo, anulada.MotivoAnulacion);
        Assert.Equal(e.Admin.IdUsuario, anulada.IdUsuarioAnulacion);
        Assert.Equal(InspeccionEstados.Cerrada, (await InspeccionAsync(e, inspeccion.IdInspeccion)).Estado);
        Assert.Contains(await e.Db.Auditoria.AsNoTracking().ToListAsync(),
            a => a.Tabla == "Novedades" && a.RegistroId == anulada.IdNovedad && a.Accion == AuditoriaAcciones.Anular);
    }

    // ------------------------------------------------------------------ integridad

    [Fact]
    public async Task Cambiar_lo_que_se_hizo_despues_de_firmar_rompe_el_sello()
    {
        var e = await SeedAsync();
        var (_, novedades) = await FirmadaConDosNovedadesAsync(e);
        await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad);
        var id = novedades[0].IdNovedad;

        var guardada = await e.Db.Novedades.SingleAsync(n => n.IdNovedad == id);
        guardada.Solucion = "Otra cosa que no se firmó";
        await e.Db.SaveChangesAsync();
        e.Db.ChangeTracker.Clear();

        var alterada = await Novedades(e, e.Admin, "Auditor").VerificarIntegridad(id, FirmaPruebas.CrearVerificador(new Almacen()));
        var pendiente = await Novedades(e, e.Admin, "Auditor").VerificarIntegridad(novedades[1].IdNovedad, FirmaPruebas.CrearVerificador(new Almacen()));
        var otraClave = await Novedades(e, e.Admin, "Auditor").VerificarIntegridad(id,
            FirmaPruebas.CrearVerificador(new Almacen(), "otra-clave-de-prueba-con-mas-de-32-caracteres"));

        Assert.False(Respuesta<IntegridadInspeccionDto>(alterada).Data!.Integra);
        Assert.IsType<ConflictObjectResult>(pendiente.Result);
        Assert.False(Respuesta<IntegridadInspeccionDto>(otraClave).Data!.Integra);
    }

    [Fact]
    public async Task Una_inspeccion_sellada_con_v1_se_sigue_verificando_y_en_v2_el_resultado_no_cuenta()
    {
        var e = await SeedAsync();
        var inspeccion = await EnCursoAsync(e, Si(e.Limpieza), Si(e.Nivel));
        await FirmarAsync(e, inspeccion.IdInspeccion);
        var verificador = FirmaPruebas.CrearVerificador(new Almacen());
        var firmada = await e.Db.Inspecciones.Include(i => i.Respuestas).Include(i => i.Evidencias)
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);

        // v2: cambiar el resultado no rompe el sello; cambiar una respuesta sí.
        firmada.Resultado = InspeccionResultados.ConNovedad;
        Assert.True(await verificador.VerificarAsync(firmada));
        firmada.Respuestas.First().Observacion = "agregada después";
        Assert.False(await verificador.VerificarAsync(firmada));
        firmada.Respuestas.First().Observacion = null;

        // v1 (firmas anteriores a las novedades): el resultado sí está sellado.
        firmada.Resultado = InspeccionResultados.Conforme;
        firmada.FirmaAlgoritmo = VerificadorIntegridad.AlgoritmoHmac;
        var canonico = await verificador.ConstruirContenidoCanonicoAsync(firmada, VerificadorIntegridad.AlgoritmoHmac);
        firmada.FirmaHash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(FirmaPruebas.ClaveHmac), Encoding.UTF8.GetBytes(canonico))).ToLowerInvariant();
        Assert.True(await verificador.VerificarAsync(firmada));
        firmada.Resultado = InspeccionResultados.ConNovedad;
        Assert.False(await verificador.VerificarAsync(firmada));
    }

    // ------------------------------------------------------------------ consulta

    [Fact]
    public async Task El_listado_filtra_por_estado_inspeccion_criticidad_y_busqueda()
    {
        var e = await SeedAsync();
        var (primera, novedades) = await FirmadaConDosNovedadesAsync(e);
        var segunda = await EnCursoAsync(e, No(e.Limpieza, "Mancha"), Si(e.Nivel));
        await FirmarAsync(e, segunda.IdInspeccion);
        await ResolverAsync(e, e.OtroTecnico, novedades[0].IdNovedad);
        var controlador = Novedades(e, e.Inspector);

        async Task<PagedResultDto<NovedadDto>> Listar(NovedadFiltroDto filtro) =>
            Respuesta<PagedResultDto<NovedadDto>>(await controlador.GetNovedades(filtro)).Data!;

        Assert.Equal(3, (await Listar(new NovedadFiltroDto())).TotalCount);
        Assert.Equal(2, (await Listar(new NovedadFiltroDto { Estado = NovedadEstados.Pendiente })).TotalCount);
        Assert.Equal(2, (await Listar(new NovedadFiltroDto { IdInspeccion = primera.IdInspeccion })).TotalCount);
        Assert.Equal(3, (await Listar(new NovedadFiltroDto { Criticidad = Criticidades.Alta })).TotalCount);
        Assert.Equal(0, (await Listar(new NovedadFiltroDto { Criticidad = Criticidades.Baja })).TotalCount);
        Assert.Single((await Listar(new NovedadFiltroDto { Search = "Mancha" })).Data);
        Assert.Equal(3, (await Listar(new NovedadFiltroDto { Desde = DateTime.UtcNow.AddHours(-5).Date, Hasta = DateTime.UtcNow.AddHours(-5).Date })).TotalCount);
        var fila = (await Listar(new NovedadFiltroDto { Estado = NovedadEstados.Resuelta })).Data.Single();
        Assert.Equal("Luis Técnico", fila.NombreUsuarioResolucion);
        Assert.Equal("Ana Inspectora", fila.NombreInspector);
        Assert.Equal("Planta 1 - Pesaje", fila.Area);
        Assert.Equal("¿Está limpia?", fila.Pregunta);

        Assert.IsType<BadRequestObjectResult>((await controlador.GetNovedades(new NovedadFiltroDto { Estado = "Abierto" })).Result);
    }

    [Fact]
    public async Task El_detalle_trae_la_observacion_la_inspeccion_y_la_resolucion_firmada()
    {
        var e = await SeedAsync();
        var (inspeccion, novedades) = await FirmadaConDosNovedadesAsync(e);
        await ResolverAsync(e, e.OtroTecnico, novedades[1].IdNovedad);

        var detalle = Respuesta<NovedadDetalleDto>(await Novedades(e, e.Admin, "Auditor").GetNovedad(novedades[1].IdNovedad)).Data!;
        var inexistente = await Novedades(e, e.Admin, "Auditor").GetNovedad(999);

        Assert.Equal(" Burbuja corrida ", detalle.ObservacionRespuesta);
        Assert.Equal(InspeccionEstados.NovedadesPendientes, detalle.EstadoInspeccion);
        Assert.Equal(inspeccion.IdInspeccion, detalle.IdInspeccion);
        Assert.Equal("Diario v. 1.0", detalle.NombreChecklist);
        Assert.Equal(Solucion, detalle.Solucion);
        Assert.Equal(FirmaPng, detalle.FirmaPngBase64);
        Assert.Equal("Luis Técnico", detalle.FirmaNombre);
        Assert.NotNull(detalle.FirmaHash);
        Assert.IsType<NotFoundObjectResult>(inexistente.Result);
    }

    [Fact]
    public async Task Reenviar_el_correo_lo_vuelve_a_poner_en_cola()
    {
        var e = await SeedAsync();
        var (_, novedades) = await FirmadaConDosNovedadesAsync(e);
        e.Notificador.Encolados.Clear();

        var reenviar = await Novedades(e, e.Admin, "Administrador").Notificar(novedades[0].IdNovedad);
        var inexistente = await Novedades(e, e.Admin, "Administrador").Notificar(999);

        Assert.IsType<OkObjectResult>(reenviar.Result);
        Assert.IsType<NotFoundObjectResult>(inexistente.Result);
        Assert.Equal([novedades[0].IdNovedad], e.Notificador.Encolados);
    }

    // ------------------------------------------------------------------ modelo y permisos

    [Fact]
    public void Ninguna_ruta_borra_ni_edita_inspecciones_novedades_ni_evidencias()
    {
        // No hay DELETE: como existen GET sobre las mismas rutas, ASP.NET responde 405.
        foreach (var controlador in new[] { typeof(InspeccionesController), typeof(NovedadesController), typeof(EvidenciasController) })
        {
            Assert.DoesNotContain(controlador.GetMethods(), m => m.GetCustomAttributes<HttpDeleteAttribute>(inherit: true).Any());
        }

        // Una novedad solo la crea la firma: no hay alta manual ni edición.
        var metodos = typeof(NovedadesController).GetMethods();
        Assert.DoesNotContain(metodos, m => m.GetCustomAttributes<HttpPutAttribute>(inherit: true).Any());
        Assert.DoesNotContain(metodos, m => m.GetCustomAttributes<HttpPostAttribute>(inherit: true)
            .Any(a => string.IsNullOrEmpty(a.Template)));
    }

    [Theory]
    [InlineData(nameof(NovedadesController.ResolverNovedad), "Administrador,Técnico")]
    [InlineData(nameof(NovedadesController.AnularNovedad), "Administrador")]
    [InlineData(nameof(NovedadesController.VerificarIntegridad), "Administrador,Auditor")]
    [InlineData(nameof(NovedadesController.Notificar), "Administrador")]
    public void Cada_accion_exige_su_rol(string accion, string roles)
    {
        var metodo = typeof(NovedadesController).GetMethod(accion)!;

        Assert.Equal(roles, metodo.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("Administrador,Técnico,Auditor", typeof(NovedadesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public async Task Modelo_ninguna_relacion_borra_en_cascada_y_una_novedad_por_respuesta()
    {
        var e = await SeedAsync();
        var tipo = e.Db.Model.FindEntityType(typeof(Novedad))!;

        Assert.All(tipo.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(tipo.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(Novedad.IdInspeccion), nameof(Novedad.IdPregunta)]));
        Assert.Contains(tipo.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(Novedad.IdRespuesta)]));
    }
}
