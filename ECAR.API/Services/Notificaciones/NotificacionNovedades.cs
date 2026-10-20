using ECAR.API.Configuration;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECAR.API.Services.Notificaciones;

/// <summary>
/// Envía el correo de una novedad y deja constancia en la auditoría: NotificacionEnviada (y la
/// fecha en la novedad) si salió, NotificacionFallida si no, con la causa. Nunca se escribe la
/// contraseña del correo en ningún mensaje.
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

    /// <summary>true si el correo salió. false si la novedad no existe o el envío falló.</summary>
    public async Task<bool> NotificarAsync(long idNovedad, CancellationToken cancellationToken = default)
    {
        var novedad = await context.Novedades
            .Include(n => n.Pregunta)
            .Include(n => n.Respuesta)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Equipo).ThenInclude(e => e.Ubicacion)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Usuario)
            .Include(n => n.Inspeccion).ThenInclude(i => i.Checklist)
            .FirstOrDefaultAsync(n => n.IdNovedad == idNovedad, cancellationToken);
        if (novedad == null)
        {
            logger.LogWarning("No se notificó la novedad {IdNovedad}: no existe", idNovedad);
            return false;
        }

        var o = opciones.Value;
        if (!o.Habilitado)
        {
            return false;
        }

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
                using var mensaje = CorreoNovedad.Construir(novedad, o, configuracion["Cliente:BaseUrl"]);
                resultado = await enviador.EnviarAsync(mensaje, cancellationToken);
                if (resultado == ResultadoEnvio.GuardadoEnCarpeta)
                {
                    error = MensajeSinCredenciales;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No se pudo enviar el correo de la novedad {IdNovedad}", idNovedad);
                error = Recortar(ex.Message);
            }
        }

        if (resultado != ResultadoEnvio.Enviado)
        {
            logger.LogInformation("Novedad {IdNovedad} sin enviar: {Error}", idNovedad, error);
            await context.RegistrarEventoAsync(AuditoriaAcciones.NotificacionFallida, novedad.IdNovedad,
                new { novedad.IdNovedad, Error = error }, cancellationToken: cancellationToken);
            return false;
        }

        var enviadoEn = DateTime.UtcNow;
        var destinatarios = string.Join(", ", o.Destinatarios);
        try
        {
            await RegistrarEnvioAsync(novedad, enviadoEn, destinatarios, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Alguien resolvió o anuló la novedad mientras salía el correo: se relee y se vuelve a
            // anotar la fecha sobre la versión nueva.
            context.ChangeTracker.Clear();
            var actual = await context.Novedades.SingleAsync(n => n.IdNovedad == idNovedad, cancellationToken);
            await RegistrarEnvioAsync(actual, enviadoEn, destinatarios, cancellationToken);
        }

        return true;
    }

    private async Task RegistrarEnvioAsync(Novedad novedad, DateTime enviadoEn,
        string destinatarios, CancellationToken cancellationToken)
    {
        novedad.FechaNotificacion = enviadoEn;
        context.RegistrarEvento(AuditoriaAcciones.NotificacionEnviada, novedad.IdNovedad,
            new { novedad.IdNovedad, Destinatarios = destinatarios });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string Recortar(string texto) => texto.Length <= 300 ? texto : texto[..300];
}

/// <summary>
/// Vacía la cola de notificaciones: cada novedad en su propio ámbito (y su propio DbContext), una
/// detrás de otra. Un fallo no detiene el servicio; la novedad queda sin enviar y se puede reenviar.
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
            await foreach (var idNovedad in cola.Lector.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var ambito = ambitos.CreateAsyncScope();
                    await ambito.ServiceProvider.GetRequiredService<NotificacionNovedades>()
                        .NotificarAsync(idNovedad, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error al procesar la notificación de la novedad {IdNovedad}", idNovedad);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // El API se está deteniendo.
        }
    }
}
