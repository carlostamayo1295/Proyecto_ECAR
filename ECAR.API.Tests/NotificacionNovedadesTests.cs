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
/// Correo de cada novedad a la mesa de ayuda: qué lleva, cómo queda constancia de que salió (o de
/// por qué no) y que sin credenciales SMTP el mensaje se guarda como .eml en lugar de enviarse.
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

    private static async Task<(ECARDbContext Db, Novedad Novedad)> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var inspector = new Usuario { Nombre = "Ana Inspectora", Correo = "ana@ecar.test", PasswordHash = "x", Activo = true };
        var equipo = new Equipo
        {
            CodigoInterno = "BAL-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Criticidad = Criticidades.Alta, Activo = true,
            Ubicacion = new Ubicacion { Planta = "Planta 1", Area = "Pesaje" }
        };
        var pregunta = new PreguntaChecklist { Pregunta = "¿Está limpia?", TipoRespuesta = TiposRespuesta.SiNo, Orden = 1 };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Preguntas = [pregunta] };
        db.AddRange(inspector, equipo, checklist);
        await db.SaveChangesAsync();

        var respuesta = new RespuestaInspeccion { IdPregunta = pregunta.IdPregunta, Respuesta = "No", Observacion = "Polvo <b>y</b> grasa" };
        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = inspector.IdUsuario,
            FechaInspeccion = new DateTime(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc), Estado = InspeccionEstados.NovedadesPendientes,
            Respuestas = [respuesta]
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();

        var novedad = new Novedad
        {
            IdInspeccion = inspeccion.IdInspeccion, IdPregunta = pregunta.IdPregunta, IdRespuesta = respuesta.IdRespuesta,
            Descripcion = "¿Está limpia?: Polvo <b>y</b> grasa", Criticidad = Criticidades.Alta,
            FechaRegistro = new DateTime(2026, 10, 19, 15, 30, 0, DateTimeKind.Utc), IdUsuarioRegistro = inspector.IdUsuario
        };
        db.Novedades.Add(novedad);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, novedad);
    }

    private static NotificacionNovedades Servicio(ECARDbContext db, IEnviadorCorreo enviador, CorreoOptions? opciones = null) =>
        new(db, enviador, Options.Create(opciones ?? Opciones()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Cliente:BaseUrl"] = "https://ecar.test/" }).Build(),
            NullLogger<NotificacionNovedades>.Instance);

    [Fact]
    public async Task El_correo_lleva_los_datos_de_la_novedad_y_el_enlace()
    {
        var (db, novedad) = await SeedAsync();
        var completa = await db.Novedades
            .Include(n => n.Pregunta).Include(n => n.Respuesta)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Equipo).ThenInclude(e => e.Ubicacion)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Usuario)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Checklist)
            .SingleAsync();

        using var mensaje = CorreoNovedad.Construir(completa, Opciones(), "https://ecar.test/");

        Assert.Equal($"[ECAR] Novedad #{novedad.IdNovedad} - BAL-1 Balanza", mensaje.Subject);
        Assert.Equal(Destinatario, mensaje.To.Single().Address);
        Assert.Equal("ecar@ecar.test", mensaje.From!.Address);
        foreach (var dato in new[] { "BAL-1 - Balanza", "AF-1", "Planta 1 - Pesaje", "Alta", "¿Está limpia?", "Polvo <b>y</b> grasa",
                     "Ana Inspectora", "Diario v. 1.0", "19/10/2026 10:30", $"https://ecar.test/novedades/{novedad.IdNovedad}" })
        {
            Assert.Contains(dato, mensaje.Body);
        }

        // En la versión HTML el texto del inspector va escapado.
        using var html = new StreamReader(mensaje.AlternateViews.Single().ContentStream);
        var cuerpo = await html.ReadToEndAsync();
        Assert.Contains("Polvo &lt;b&gt;y&lt;/b&gt; grasa", cuerpo);
        Assert.DoesNotContain("<b>y</b>", cuerpo);
    }

    [Fact]
    public async Task Sin_cuenta_configurada_el_remitente_es_el_de_por_defecto()
    {
        var (db, _) = await SeedAsync();
        var completa = await db.Novedades
            .Include(n => n.Pregunta).Include(n => n.Respuesta)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Equipo).ThenInclude(e => e.Ubicacion)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Usuario)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Checklist)
            .SingleAsync();

        // Así quedan Remitente y Usuario en appsettings.json: cadenas vacías, no null.
        using var mensaje = CorreoNovedad.Construir(completa, Opciones(o => { o.Usuario = ""; o.Remitente = " "; }), null);

        Assert.Equal(CorreoNovedad.RemitentePorDefecto, mensaje.From!.Address);
        Assert.DoesNotContain("/novedades/", mensaje.Body);
    }

    [Fact]
    public async Task Si_el_correo_sale_se_anota_la_fecha_y_queda_en_la_auditoria()
    {
        var (db, novedad) = await SeedAsync();
        var enviador = new EnviadorFalso(_ => ResultadoEnvio.Enviado);

        var enviado = await Servicio(db, enviador).NotificarAsync(novedad.IdNovedad);

        Assert.True(enviado);
        Assert.Single(enviador.Enviados);
        Assert.NotNull((await db.Novedades.AsNoTracking().SingleAsync()).FechaNotificacion);
        var evento = await db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AuditoriaAcciones.NotificacionEnviada);
        Assert.Equal(novedad.IdNovedad, evento.RegistroId);
        Assert.Contains(Destinatario, evento.ValorNuevo);
    }

    [Fact]
    public async Task Sin_credenciales_el_mensaje_no_sale_y_la_novedad_queda_sin_enviar()
    {
        var (db, novedad) = await SeedAsync();
        var enviador = new EnviadorFalso(_ => ResultadoEnvio.GuardadoEnCarpeta);

        var enviado = await Servicio(db, enviador).NotificarAsync(novedad.IdNovedad);

        Assert.False(enviado);
        Assert.Null((await db.Novedades.AsNoTracking().SingleAsync()).FechaNotificacion);
        var evento = await db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AuditoriaAcciones.NotificacionFallida);
        Assert.Contains("Correo:Usuario", evento.ValorNuevo);
    }

    [Fact]
    public async Task Si_el_servidor_falla_no_se_lanza_la_excepcion_y_queda_el_motivo()
    {
        var (db, novedad) = await SeedAsync();
        var enviador = new EnviadorFalso(_ => throw new SmtpException("Servidor no disponible"));

        var enviado = await Servicio(db, enviador).NotificarAsync(novedad.IdNovedad);
        var sinDestinatarios = await Servicio(db, enviador, Opciones(o => o.Destinatarios = [])).NotificarAsync(novedad.IdNovedad);
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
        var (db, novedad) = await SeedAsync();
        var enviador = new EnviadorFalso(_ => ResultadoEnvio.Enviado);

        var enviado = await Servicio(db, enviador, Opciones(o => o.Habilitado = false)).NotificarAsync(novedad.IdNovedad);

        Assert.False(enviado);
        Assert.Empty(enviador.Enviados);
        Assert.False(await db.Auditoria.AnyAsync(a => a.Accion == AuditoriaAcciones.NotificacionFallida));
    }
}
