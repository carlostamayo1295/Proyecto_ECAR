using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using ECAR.API.Configuration;
using ECAR.Infrastructure.Entities;

namespace ECAR.API.Services.Notificaciones;

/// <summary>
/// El correo de las novedades de una inspección para la mesa de ayuda, en texto y en HTML: uno solo
/// por inspección, tenga las novedades que tenga. Lleva lo necesario para abrir el caso sin entrar a
/// ECAR: equipo, ubicación, criticidad, quién y cuándo, y por cada novedad la pregunta, la
/// observación del inspector y su estado, con el enlace a las novedades de la inspección. Qué datos
/// exige ECAR está pendiente de confirmar (SRS v1.1, §5 "Revisar por la notificación qué datos deben
/// adicionar").
/// </summary>
public static class CorreoNovedad
{
    /// <summary>Remitente mientras no haya cuenta de correo configurada (solo para los .eml de la carpeta).</summary>
    public const string RemitentePorDefecto = "notificaciones@ecar.local";

    /// <summary>
    /// Necesita la inspección con el equipo (y su ubicación), el inspector y el checklist, y cada
    /// novedad con su pregunta y su respuesta.
    /// </summary>
    public static MailMessage Construir(Inspeccion inspeccion, IReadOnlyList<Novedad> novedades, CorreoOptions opciones,
        string? urlCliente)
    {
        var equipo = inspeccion.Equipo;
        var ubicacion = equipo.Ubicacion == null ? "Sin ubicación" : $"{equipo.Ubicacion.Planta} - {equipo.Ubicacion.Area}";
        var criticidad = novedades.Select(n => n.Criticidad).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
        var fecha = inspeccion.FechaCierre ?? novedades.Min(n => n.FechaRegistro);
        var enlace = string.IsNullOrWhiteSpace(urlCliente)
            ? null
            : $"{urlCliente.TrimEnd('/')}/novedades?inspeccion={inspeccion.IdInspeccion}";

        var datos = new List<(string Campo, string Valor)>
        {
            ("Inspección", $"#{inspeccion.IdInspeccion} ({inspeccion.Checklist.Nombre} v. {inspeccion.Checklist.Version})"),
            ("Equipo", $"{equipo.CodigoInterno} - {equipo.NombreEquipo}"),
            ("Activo fijo", equipo.ActivoFijo),
            ("Ubicación", ubicacion),
            ("Criticidad", criticidad ?? "Sin criticidad"),
            ("Inspector", inspeccion.Usuario.Nombre),
            ("Fecha", $"{HoraColombia.Texto(fecha)} ({HoraColombia.Zona})"),
            ("Novedades", novedades.Count.ToString())
        };
        var filas = novedades
            .Select(n => (Novedad: $"#{n.IdNovedad}", n.Pregunta.Pregunta, Observacion: n.Respuesta.Observacion ?? "-", n.Estado))
            .ToList();

        // appsettings deja Remitente y Usuario como "" (no null) hasta que se configuren.
        var remitente = !string.IsNullOrWhiteSpace(opciones.Remitente) ? opciones.Remitente
            : !string.IsNullOrWhiteSpace(opciones.Usuario) ? opciones.Usuario
            : RemitentePorDefecto;
        var mensaje = new MailMessage
        {
            From = new MailAddress(remitente, opciones.NombreRemitente),
            Subject = $"[ECAR] Inspección #{inspeccion.IdInspeccion} - {novedades.Count} novedad(es) - " +
                      $"{equipo.CodigoInterno} {equipo.NombreEquipo}",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = Texto(datos, filas, enlace),
            IsBodyHtml = false
        };
        mensaje.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(Html(datos, filas, enlace), Encoding.UTF8,
            MediaTypeNames.Text.Html));

        foreach (var destinatario in opciones.Destinatarios.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            mensaje.To.Add(destinatario.Trim());
        }

        return mensaje;
    }

    private static string Introduccion(int novedades) =>
        novedades == 1
            ? "Se registró una novedad al firmar una inspección."
            : $"Se registraron {novedades} novedades al firmar una inspección.";

    private static string Texto(List<(string Campo, string Valor)> datos,
        List<(string Novedad, string Pregunta, string Observacion, string Estado)> filas, string? enlace)
    {
        var texto = new StringBuilder(Introduccion(filas.Count)).Append("\n\n");
        foreach (var (campo, valor) in datos)
        {
            texto.Append(campo).Append(": ").Append(valor).Append('\n');
        }

        for (var i = 0; i < filas.Count; i++)
        {
            var fila = filas[i];
            texto.Append('\n').Append(i + 1).Append(". Novedad ").Append(fila.Novedad).Append(" (").Append(fila.Estado).Append(")\n")
                .Append("   Pregunta: ").Append(fila.Pregunta).Append('\n')
                .Append("   Observación del inspector: ").Append(fila.Observacion).Append('\n');
        }

        if (enlace != null)
        {
            texto.Append("\nVer las novedades: ").Append(enlace).Append('\n');
        }

        return texto.Append("\nMensaje automático de ECAR. No responda a este correo.\n").ToString();
    }

    private static string Html(List<(string Campo, string Valor)> datos,
        List<(string Novedad, string Pregunta, string Observacion, string Estado)> filas, string? enlace)
    {
        const string Tabla = "<table style=\"border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px\">";
        const string Celda = "<td style=\"padding:4px 12px 4px 0;vertical-align:top;white-space:pre-wrap\">";
        const string Encabezado = "<th style=\"text-align:left;padding:4px 12px 4px 0;vertical-align:top\">";

        var html = new StringBuilder()
            .Append("<p>").Append(WebUtility.HtmlEncode(Introduccion(filas.Count))).Append("</p>")
            .Append(Tabla);
        foreach (var (campo, valor) in datos)
        {
            html.Append("<tr>").Append(Encabezado).Append(WebUtility.HtmlEncode(campo)).Append("</th>")
                .Append(Celda).Append(WebUtility.HtmlEncode(valor)).Append("</td></tr>");
        }

        html.Append("</table><p></p>").Append(Tabla).Append("<tr>");
        foreach (var titulo in new[] { "Novedad", "Pregunta", "Observación del inspector", "Estado" })
        {
            html.Append(Encabezado).Append(WebUtility.HtmlEncode(titulo)).Append("</th>");
        }

        html.Append("</tr>");
        foreach (var fila in filas)
        {
            html.Append("<tr>");
            foreach (var valor in new[] { fila.Novedad, fila.Pregunta, fila.Observacion, fila.Estado })
            {
                html.Append(Celda).Append(WebUtility.HtmlEncode(valor)).Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</table>");
        if (enlace != null)
        {
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(enlace)).Append("\">Ver las novedades en ECAR</a></p>");
        }

        return html.Append("<p style=\"color:#666;font-size:12px\">Mensaje automático de ECAR. No responda a este correo.</p>")
            .ToString();
    }
}
