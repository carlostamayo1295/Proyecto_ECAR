namespace ECAR.API.Configuration;

/// <summary>
/// Correo de las novedades a la mesa de ayuda (SRS v1.1, regla 4), uno por inspección. Con <see cref="Usuario"/> y
/// <see cref="Password"/> el mensaje sale por SMTP; sin ellos no sale: se guarda como .eml en
/// <see cref="CarpetaSalida"/> para revisarlo y la novedad queda "sin enviar", de modo que el
/// Administrador puede reenviarla cuando el correo esté configurado. La contraseña va en
/// user-secrets (Correo:Password) o en la variable Correo__Password, nunca en appsettings.
/// </summary>
public class CorreoOptions
{
    public const string SectionName = "Correo";

    /// <summary>false apaga las notificaciones del todo: no se envía ni se guarda nada.</summary>
    public bool Habilitado { get; set; } = true;

    public string Servidor { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;

    /// <summary>STARTTLS en el puerto 587.</summary>
    public bool UsarSsl { get; set; } = true;

    public string? Usuario { get; set; }
    public string? Password { get; set; }

    /// <summary>Dirección del remitente; si se deja vacía, la de <see cref="Usuario"/>.</summary>
    public string? Remitente { get; set; }

    public string NombreRemitente { get; set; } = "ECAR - Inspecciones";

    /// <summary>A quién se avisa de las novedades de cada inspección (la mesa de ayuda).</summary>
    public List<string> Destinatarios { get; set; } = [];

    /// <summary>Dónde se guardan los .eml mientras no haya credenciales (relativa a la carpeta del API).</summary>
    public string CarpetaSalida { get; set; } = "CorreosSalida";

    public int SegundosEspera { get; set; } = 30;

    public bool TieneCredenciales => !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Password);
}
