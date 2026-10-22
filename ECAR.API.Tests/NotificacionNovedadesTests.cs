using System.Net.Mail;
using ECAR.API.Configuration;
using ECAR.API.Services.Notificaciones;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Correo de las novedades a la mesa de ayuda, uno solo por inspección: qué lleva, cómo queda
/// constancia de que salió (o de por qué no) y que sin credenciales SMTP el mensaje se guarda como
/// .eml en lugar de enviarse.
/// </summary>
public class NotificacionNovedadesTests
{
    private const string Destinatario = "mesa.ayuda@ecar.test";

    private sealed class EnviadorFalso(Func<MailMessage, ResultadoEnvio> enviar) : IEnviadorCorreo
    {
        public List<(string Asunto, string Para)> Enviados { get; } = new();

        public Task<ResultadoEnvio> EnviarAsync(MailMessage mensaje, CancellationToken cancellationToken = default)
        {
            Enviados.Add((mensaje.Subject, mensaje.To.ToString()));
            return Task.FromResult(enviar(mensaje));
        }
    }

    private sealed class Entorno(string raiz) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = raiz;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "ECAR.API.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = raiz;
        public string EnvironmentName { get; set; } = "Testing";
    }

    private static CorreoOptions Opciones(Action<CorreoOptions>? ajustar = null)
    {
        var opciones = new CorreoOptions { Usuario = "ecar@ecar.test", Password = "solo-para-pruebas", Destinatarios = [Destinatario] };
        ajustar?.Invoke(opciones);
        return opciones;
    }

    private sealed record Escenario(ECARDbContext Db, Inspeccion Inspeccion, List<Novedad> Novedades);

    /// <summary>Una inspección firmada con dos novedades (dos respuestas «No»).</summary>
    private static async Task<Escenario> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var inspector = new Usuario { Nombre = "Ana Inspectora", Correo = "ana@ecar.test", PasswordHash = "x", Activo = true };
        var equipo = new Equipo
        {
            CodigoInterno = "BAL-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Criticidad = Criticidades.Alta, Activo = true,
            Ubicacion = new Ubicacion { Planta = "Planta 1", Area = "Pesaje" }
        };
        var limpia = new PreguntaChecklist { Pregunta = "¿Está limpia?", TipoRespuesta = TiposRespuesta.SiNo, Orden = 1 };
        var nivelada = new PreguntaChecklist { Pregunta = "¿Está nivelada?", TipoRespuesta = TiposRespuesta.SiNo, Orden = 2 };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Preguntas = [limpia, nivelada] };
        db.AddRange(inspector, equipo, checklist);
        await db.SaveChangesAsync();

        var sucia = new RespuestaInspeccion { IdPregunta = limpia.IdPregunta, Respuesta = "No", Observacion = "Polvo <b>y</b> grasa" };
        var torcida = new RespuestaInspeccion { IdPregunta = nivelada.IdPregunta, Respuesta = "No", Observacion = "Burbuja corrida" };
        var firmada = new DateTime(2026, 10, 19, 15, 30, 0, DateTimeKind.Utc);
        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = inspector.IdUsuario,
            FechaInspeccion = new DateTime(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc), FechaCierre = firmada,
            Estado = InspeccionEstados.NovedadesPendientes, Respuestas = [sucia, torcida]
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();

        var novedades = new List<Novedad>
        {
            new()
            {
                IdInspeccion = inspeccion.IdInspeccion, IdPregunta = limpia.IdPregunta, IdRespuesta = sucia.IdRespuesta,
                Descripcion = "¿Está limpia?: Polvo <b>y</b> grasa", Criticidad = Criticidades.Alta,
                FechaRegistro = firmada, IdUsuarioRegistro = inspector.IdUsuario
            },
            new()
            {
                IdInspeccion = inspeccion.IdInspeccion, IdPregunta = nivelada.IdPregunta, IdRespuesta = torcida.IdRespuesta,
                Descripcion = "¿Está nivelada?: Burbuja corrida", Criticidad = Criticidades.Alta,
                FechaRegistro = firmada, IdUsuarioRegistro = inspector.IdUsuario
            }
        };
        db.Novedades.AddRange(novedades);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Escenario(db, inspeccion, novedades);
    }

    /// <summary>La inspección con lo que necesita el correo, como la carga NotificacionNovedades.</summary>
    private static Task<Inspeccion> CompletaAsync(Escenario e) =>
        e.Db.Inspecciones
            .Include(i => i.Equipo).ThenInclude(eq => eq.Ubicacion)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Include(i => i.Novedades).ThenInclude(n => n.Pregunta)
            .Include(i => i.Novedades).ThenInclude(n => n.Respuesta)
            .SingleAsync(i => i.IdInspeccion == e.Inspeccion.IdInspeccion);

    private static NotificacionNovedades Servicio(ECARDbContext db, IEnviadorCorreo enviador, CorreoOptions? opciones = null) =>
        new(db, enviador, Options.Create(opciones ?? Opciones()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Cliente:BaseUrl"] = "https://ecar.test/" }).Build(),
            NullLogger<NotificacionNovedades>.Instance);

    [Fact]
    public async Task El_correo_es_uno_por_inspeccion_con_todas_sus_novedades_y_el_enlace()
    {
        var e = await SeedAsync();
        var inspeccion = await CompletaAsync(e);
        var id = inspeccion.IdInspeccion;

        using var mensaje = CorreoNovedad.Construir(inspeccion, inspeccion.Novedades.OrderBy(n => n.IdNovedad).ToList(),
            Opciones(), "https://ecar.test/");

        Assert.Equal($"[ECAR] Inspección #{id} - 2 novedad(es) - BAL-1 Balanza", mensaje.Subject);
        Assert.Equal(Destinatario, mensaje.To.Single().Address);
        Assert.Equal("ecar@ecar.test", mensaje.From!.Address);
        foreach (var dato in new[] { "Se registraron 2 novedades", $"#{id} (Diario v. 1.0)", "BAL-1 - Balanza", "AF-1",
                     "Planta 1 - Pesaje", "Alta", "Ana Inspectora", "19/10/2026 10:30",
                     $"Novedad #{e.Novedades[0].IdNovedad} (Pendiente)", "¿Está limpia?", "Polvo <b>y</b> grasa",
                     $"Novedad #{e.Novedades[1].IdNovedad} (Pendiente)", "¿Está nivelada?", "Burbuja corrida",
                     $"https://ecar.test/novedades?inspeccion={id}" })
        {
            Assert.Contains(dato, mensaje.Body);
        }

        // En la versión HTML el texto del inspector va escapado.
        using var html = new StreamReader(mensaje.AlternateViews.Single().ContentStream);
        var cuerpo = await html.ReadToEndAsync();
        Assert.Contains("Polvo &lt;b&gt;y&lt;/b&gt; grasa", cuerpo);
        Assert.Contains("Burbuja corrida", cuerpo);
        Assert.DoesNotContain("<b>y</b>", cuerpo);
    }

    [Fact]
    public async Task Sin_cuenta_configurada_el_remitente_es_el_de_por_defecto()
    {
        var e = await SeedAsync();
        var inspeccion = await CompletaAsync(e);

        // Así quedan Remitente y Usuario en appsettings.json: cadenas vacías, no null.
        using var mensaje = CorreoNovedad.Construir(inspeccion, inspeccion.Novedades.ToList(),
            Opciones(o => { o.Usuario = ""; o.Remitente = " "; }), null);

        Assert.Equal(CorreoNovedad.RemitentePorDefecto, mensaje.From!.Address);
        Assert.DoesNotContain("/novedades", mensaje.Body);
    }

    [Fact]
    public async Task Si_el_correo_sale_es_uno_solo_se_anota_la_fecha_en_todas_y_queda_en_la_auditoria()
    {
        var e = await SeedAsync();
        var enviador = new EnviadorFalso(_ => ResultadoEnvio.Enviado);

        var enviado = await Servicio(e.Db, enviador).NotificarAsync(e.Inspeccion.IdInspeccion);

        Assert.True(enviado);
        Assert.Single(enviador.Enviados);
        Assert.All(await e.Db.Novedades.AsNoTracking().ToListAsync(), n => Assert.NotNull(n.FechaNotificacion));
        var evento = await e.Db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AuditoriaAcciones.NotificacionEnviada);
        Assert.Equal(e.Inspeccion.IdInspeccion, evento.RegistroId);
        Assert.Contains(Destinatario, evento.ValorNuevo);
        Assert.Contains($"[{e.Novedades[0].IdNovedad},{e.Novedades[1].IdNovedad}]", evento.ValorNuevo);
    }

    [Fact]
    public async Task Sin_credenciales_el_mensaje_no_sale_y_las_novedades_quedan_sin_enviar()
    {
        var e = await SeedAsync();
        var enviador = new EnviadorFalso(_ => ResultadoEnvio.GuardadoEnCarpeta);

        var enviado = await Servicio(e.Db, enviador).NotificarAsync(e.Inspeccion.IdInspeccion);

        Assert.False(enviado);
        Assert.Single(enviador.Enviados);
        Assert.All(await e.Db.Novedades.AsNoTracking().ToListAsync(), n => Assert.Null(n.FechaNotificacion));
        var evento = await e.Db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AuditoriaAcciones.NotificacionFallida);
        Assert.Equal(e.Inspeccion.IdInspeccion, evento.RegistroId);
        Assert.Contains("Correo:Usuario", evento.ValorNuevo);
    }

    [Fact]
    public async Task Una_inspeccion_sin_novedades_no_envia_nada()
    {
        var e = await SeedAsync();
        var sinNovedades = new Inspeccion
        {
            IdEquipo = e.Inspeccion.IdEquipo, IdChecklist = e.Inspeccion.IdChecklist, IdUsuario = e.Inspeccion.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, Estado = InspeccionEstados.Cerrada
        };
        e.Db.Inspecciones.Add(sinNovedades);
        await e.Db.SaveChangesAsync();
        var enviador = new EnviadorFalso(_ => ResultadoEnvio.Enviado);

        var enviado = await Servicio(e.Db, enviador).NotificarAsync(sinNovedades.IdInspeccion);

        Assert.False(enviado);
        Assert.Empty(enviador.Enviados);
    }

    [Fact]
    public async Task Si_el_servidor_falla_no_se_lanza_la_excepcion_y_queda_el_motivo()
    {
        var e = await SeedAsync();
        var db = e.Db;
        var enviador = new EnviadorFalso(_ => throw new SmtpException("Servidor no disponible"));

        var enviado = await Servicio(db, enviador).NotificarAsync(e.Inspeccion.IdInspeccion);
        var sinDestinatarios = await Servicio(db, enviador, Opciones(o => o.Destinatarios = [])).NotificarAsync(e.Inspeccion.IdInspeccion);
        var inexistente = await Servicio(db, enviador).NotificarAsync(999);

        Assert.False(enviado);
        Assert.False(sinDestinatarios);
        Assert.False(inexistente);
        var errores = await db.Auditoria.AsNoTracking().Where(a => a.Accion == AuditoriaAcciones.NotificacionFallida)
            .Select(a => a.ValorNuevo!).ToListAsync();
        Assert.Contains(errores, e => e.Contains("Servidor no disponible"));
        Assert.Contains(errores, e => e.Contains("Correo:Destinatarios"));
        // La contraseña del correo nunca llega a la auditoría.
        Assert.DoesNotContain(await db.Auditoria.AsNoTracking().Select(a => a.ValorNuevo).ToListAsync(),
            v => v != null && v.Contains("solo-para-pruebas"));
    }

    [Fact]
    public async Task Sin_credenciales_el_enviador_guarda_un_eml_en_la_carpeta_de_salida()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "ecar-correos-" + Guid.NewGuid().ToString("N"));
        try
        {
            var opciones = Opciones(o =>
            {
                o.Usuario = null;
                o.Password = null;
                o.CarpetaSalida = carpeta;
            });
            var enviador = new EnviadorCorreoSmtp(Options.Create(opciones), new Entorno(Path.GetTempPath()));
            using var mensaje = new MailMessage("notificaciones@ecar.local", Destinatario, "[ECAR] Novedad #7", "Prueba");

            var resultado = await enviador.EnviarAsync(mensaje);

            Assert.Equal(ResultadoEnvio.GuardadoEnCarpeta, resultado);
            var eml = Assert.Single(Directory.GetFiles(carpeta, "*.eml"));
            Assert.Contains("Novedad #7", await File.ReadAllTextAsync(eml));
        }
        finally
        {
            if (Directory.Exists(carpeta))
            {
                Directory.Delete(carpeta, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Con_las_notificaciones_apagadas_no_se_envia_ni_se_registra_nada()
    {
        var e = await SeedAsync();
        var enviador = new EnviadorFalso(_ => ResultadoEnvio.Enviado);

        var enviado = await Servicio(e.Db, enviador, Opciones(o => o.Habilitado = false)).NotificarAsync(e.Inspeccion.IdInspeccion);

        Assert.False(enviado);
        Assert.Empty(enviador.Enviados);
        Assert.False(await e.Db.Auditoria.AnyAsync(a => a.Accion == AuditoriaAcciones.NotificacionFallida));
    }
}
