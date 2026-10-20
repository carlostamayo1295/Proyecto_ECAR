using System.Net;
using System.Net.Mail;
using ECAR.API.Configuration;
using Microsoft.Extensions.Options;

namespace ECAR.API.Services.Notificaciones;

public enum ResultadoEnvio
{
    /// <summary>El servidor SMTP aceptó el mensaje.</summary>
    Enviado,

    /// <summary>Sin credenciales SMTP: el mensaje se guardó como .eml en la carpeta de salida y no salió.</summary>
    GuardadoEnCarpeta,

    /// <summary>Las notificaciones están apagadas (Correo:Habilitado = false).</summary>
    Desactivado
}

public interface IEnviadorCorreo
{
    /// <summary>Remitente y destinatarios ya configurados; lanza una excepción si el envío falla.</summary>
    Task<ResultadoEnvio> EnviarAsync(MailMessage mensaje, CancellationToken cancellationToken = default);
}

/// <summary>Envío por SMTP (STARTTLS) o, sin credenciales, a una carpeta de .eml.</summary>
public sealed class EnviadorCorreoSmtp(IOptions<CorreoOptions> opciones, IWebHostEnvironment entorno) : IEnviadorCorreo
{
    public async Task<ResultadoEnvio> EnviarAsync(MailMessage mensaje, CancellationToken cancellationToken = default)
    {
        var o = opciones.Value;
        if (!o.Habilitado)
        {
            return ResultadoEnvio.Desactivado;
        }

        using var smtp = new SmtpClient();
        if (o.TieneCredenciales)
        {
            smtp.Host = o.Servidor;
            smtp.Port = o.Puerto;
            smtp.EnableSsl = o.UsarSsl;
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(o.Usuario, o.Password);
            smtp.Timeout = o.SegundosEspera * 1000;
            await smtp.SendMailAsync(mensaje, cancellationToken);
            return ResultadoEnvio.Enviado;
        }

        var carpeta = Path.IsPathRooted(o.CarpetaSalida)
            ? o.CarpetaSalida
            : Path.Combine(entorno.ContentRootPath, o.CarpetaSalida);
        Directory.CreateDirectory(carpeta);
        smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
        smtp.PickupDirectoryLocation = carpeta;
        await smtp.SendMailAsync(mensaje, cancellationToken);
        return ResultadoEnvio.GuardadoEnCarpeta;
    }
}
