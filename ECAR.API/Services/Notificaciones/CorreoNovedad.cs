using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using ECAR.API.Configuration;
using ECAR.Infrastructure.Entities;

namespace ECAR.API.Services.Notificaciones;

/// <summary>
/// El correo de una novedad para la mesa de ayuda, en texto y en HTML. Lleva lo necesario para
/// abrir el caso sin entrar a ECAR: equipo, ubicación, criticidad, la pregunta y la observación
/// del inspector, quién y cuándo, y el enlace a la novedad. Qué datos exige ECAR está pendiente
/// de confirmar (SRS v1.1, §5 "Revisar por la notificación qué datos deben adicionar").
/// </summary>
public static class CorreoNovedad
{
    /// <summary>Remitente mientras no haya cuenta de correo configurada (solo para los .eml de la carpeta).</summary>
    public const string RemitentePorDefecto = "notificaciones@ecar.local";

    /// <summary>
    /// Necesita la novedad con su pregunta, su respuesta y su inspección, y de esta el equipo
    /// (con su ubicación), el inspector y el checklist.
    /// </summary>
    public static MailMessage Construir(Novedad novedad, CorreoOptions opciones, string? urlCliente)
    {
        var inspeccion = novedad.Inspeccion;
        var equipo = inspeccion.Equipo;
        var ubicacion = equipo.Ubicacion == null ? "Sin ubicación" : $"{equipo.Ubicacion.Planta} - {equipo.Ubicacion.Area}";
        var enlace = string.IsNullOrWhiteSpace(urlCliente) ? null : $"{urlCliente.TrimEnd('/')}/novedades/{novedad.IdNovedad}";

        var datos = new List<(string Campo, string Valor)>
        {
            ("Novedad", $"#{novedad.IdNovedad}"),
            ("Equipo", $"{equipo.CodigoInterno} - {equipo.NombreEquipo}"),
            ("Activo fijo", equipo.ActivoFijo),
            ("Ubicación", ubicacion),
            ("Criticidad", string.IsNullOrWhiteSpace(novedad.Criticidad) ? "Sin criticidad" : novedad.Criticidad),
            ("Inspección", $"#{inspeccion.IdInspeccion} ({inspeccion.Checklist.Nombre} v. {inspeccion.Checklist.Version})"),
            ("Pregunta", novedad.Pregunta.Pregunta),
            ("Observación del inspector", novedad.Respuesta.Observacion ?? "-"),
            ("Inspector", inspeccion.Usuario.Nombre),
            ("Fecha", $"{HoraColombia.Texto(novedad.FechaRegistro)} ({HoraColombia.Zona})")
        };

        // appsettings deja Remitente y Usuario como "" (no null) hasta que se configuren.
        var remitente = !string.IsNullOrWhiteSpace(opciones.Remitente) ? opciones.Remitente
            : !string.IsNullOrWhiteSpace(opciones.Usuario) ? opciones.Usuario
            : RemitentePorDefecto;
        var mensaje = new MailMessage
        {
            From = new MailAddress(remitente, opciones.NombreRemitente),
            Subject = $"[ECAR] Novedad #{novedad.IdNovedad} - {equipo.CodigoInterno} {equipo.NombreEquipo}",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = Texto(datos, enlace),
            IsBodyHtml = false
        };
        mensaje.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(Html(datos, enlace), Encoding.UTF8,
            MediaTypeNames.Text.Html));

        foreach (var destinatario in opciones.Destinatarios.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            mensaje.To.Add(destinatario.Trim());
        }

        return mensaje;
    }

    private static string Texto(List<(string Campo, string Valor)> datos, string? enlace)
    {
        var texto = new StringBuilder("Se registró una novedad al firmar una inspección.\n\n");
        foreach (var (campo, valor) in datos)
        {
            texto.Append(campo).Append(": ").Append(valor).Append('\n');
        }

        if (enlace != null)
        {
            texto.Append("\nVer la novedad: ").Append(enlace).Append('\n');
        }

        return texto.Append("\nMensaje automático de ECAR. No responda a este correo.\n").ToString();
    }

    private static string Html(List<(string Campo, string Valor)> datos, string? enlace)
    {
        var html = new StringBuilder()
            .Append("<p>Se registró una novedad al firmar una inspección.</p>")
            .Append("<table style=\"border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px\">");
        foreach (var (campo, valor) in datos)
        {
            html.Append("<tr><th style=\"text-align:left;padding:4px 12px 4px 0;vertical-align:top\">")
                .Append(WebUtility.HtmlEncode(campo))
                .Append("</th><td style=\"padding:4px 0;white-space:pre-wrap\">")
                .Append(WebUtility.HtmlEncode(valor))
                .Append("</td></tr>");
        }

        html.Append("</table>");
        if (enlace != null)
        {
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(enlace)).Append("\">Ver la novedad en ECAR</a></p>");
        }

        return html.Append("<p style=\"color:#666;font-size:12px\">Mensaje automático de ECAR. No responda a este correo.</p>")
            .ToString();
    }
}
