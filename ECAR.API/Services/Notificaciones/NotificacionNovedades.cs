using ECAR.API.Configuration;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECAR.API.Services.Notificaciones;

/// <summary>
/// Envía el correo de las novedades de una inspección (uno solo, con todas ellas) y deja constancia
/// en la auditoría: NotificacionEnviada (y la fecha en cada novedad) si salió, NotificacionFallida
/// si no, con la causa. Nunca se escribe la contraseña del correo en ningún mensaje.
/// </summary>
public sealed class NotificacionNovedades(
    ECARDbContext context,
    IEnviadorCorreo enviador,
    IOptions<CorreoOptions> opciones,
    IConfiguration configuracion,
    ILogger<NotificacionNovedades> logger)
{
    public const string MensajeSinCredenciales =
        "El correo no está configurado (faltan Correo:Usuario y Correo:Password): el mensaje se guardó en la carpeta de salida";

    public const string MensajeSinDestinatarios = "No hay destinatarios configurados en Correo:Destinatarios";

    /// <summary>true si el correo salió. false si la inspección no existe, no tiene novedades o el envío falló.</summary>
    public async Task<bool> NotificarAsync(long idInspeccion, CancellationToken cancellationToken = default)
    {
        var inspeccion = await context.Inspecciones
            .Include(i => i.Equipo).ThenInclude(e => e.Ubicacion)
            .Include(i => i.Usuario)
            .Include(i => i.Checklist)
            .Include(i => i.Novedades).ThenInclude(n => n.Pregunta)
            .Include(i => i.Novedades).ThenInclude(n => n.Respuesta)
            .FirstOrDefaultAsync(i => i.IdInspeccion == idInspeccion, cancellationToken);
        if (inspeccion == null || inspeccion.Novedades.Count == 0)
        {
            logger.LogWarning("No se notificó la inspección {IdInspeccion}: no existe o no tiene novedades", idInspeccion);
            return false;
        }

        var o = opciones.Value;
        if (!o.Habilitado)
        {
            return false;
        }

        var novedades = inspeccion.Novedades.OrderBy(n => n.IdNovedad).ToList();
        var idsNovedad = novedades.Select(n => n.IdNovedad).ToList();
        string? error = null;
        var resultado = ResultadoEnvio.Desactivado;
        if (o.Destinatarios.All(string.IsNullOrWhiteSpace))
        {
            error = MensajeSinDestinatarios;
        }
        else
        {
            try
            {
                using var mensaje = CorreoNovedad.Construir(inspeccion, novedades, o, configuracion["Cliente:BaseUrl"]);
                resultado = await enviador.EnviarAsync(mensaje, cancellationToken);
                if (resultado == ResultadoEnvio.GuardadoEnCarpeta)
                {
                    error = MensajeSinCredenciales;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No se pudo enviar el correo de la inspección {IdInspeccion}", idInspeccion);
                error = Recortar(ex.Message);
            }
        }

        if (resultado != ResultadoEnvio.Enviado)
        {
            logger.LogInformation("Inspección {IdInspeccion} sin enviar: {Error}", idInspeccion, error);
            await context.RegistrarEventoAsync(AuditoriaAcciones.NotificacionFallida, idInspeccion,
                new { IdInspeccion = idInspeccion, Novedades = idsNovedad, Error = error }, cancellationToken: cancellationToken);
            return false;
        }

        var enviadoEn = DateTime.UtcNow;
        var destinatarios = string.Join(", ", o.Destinatarios);
        try
        {
            await RegistrarEnvioAsync(idInspeccion, novedades, enviadoEn, destinatarios, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Alguien resolvió o anuló una de las novedades mientras salía el correo: se releen y se
            // vuelve a anotar la fecha sobre la versión nueva.
            context.ChangeTracker.Clear();
            var actuales = await context.Novedades.Where(n => idsNovedad.Contains(n.IdNovedad))
                .OrderBy(n => n.IdNovedad).ToListAsync(cancellationToken);
            await RegistrarEnvioAsync(idInspeccion, actuales, enviadoEn, destinatarios, cancellationToken);
        }

        return true;
    }

    private async Task RegistrarEnvioAsync(long idInspeccion, List<Novedad> novedades, DateTime enviadoEn,
        string destinatarios, CancellationToken cancellationToken)
    {
        foreach (var novedad in novedades)
        {
            novedad.FechaNotificacion = enviadoEn;
        }

        context.RegistrarEvento(AuditoriaAcciones.NotificacionEnviada, idInspeccion,
            new { IdInspeccion = idInspeccion, Novedades = novedades.Select(n => n.IdNovedad), Destinatarios = destinatarios });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string Recortar(string texto) => texto.Length <= 300 ? texto : texto[..300];
}

/// <summary>
/// Vacía la cola de notificaciones: cada inspección en su propio ámbito (y su propio DbContext),
/// una detrás de otra. Un fallo no detiene el servicio; sus novedades quedan sin enviar y el correo
/// se puede reenviar.
/// </summary>
public sealed class EnvioNotificacionesService(
    ColaNotificaciones cola,
    IServiceScopeFactory ambitos,
    ILogger<EnvioNotificacionesService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var idInspeccion in cola.Lector.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var ambito = ambitos.CreateAsyncScope();
                    await ambito.ServiceProvider.GetRequiredService<NotificacionNovedades>()
                        .NotificarAsync(idInspeccion, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error al procesar la notificación de la inspección {IdInspeccion}", idInspeccion);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // El API se está deteniendo.
        }
    }
}
