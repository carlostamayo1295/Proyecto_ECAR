namespace ECAR.Shared.DTOs;

public class EquipoQrDto
{
    public long IdEquipo { get; set; }

    // Token opaco guardado en Equipos.QRCode; la imagen se genera bajo demanda.
    public string Token { get; set; } = string.Empty;

    // URL que codifica el QR: {ClienteBaseUrl}/equipos/qr/{token}
    public string UrlConsulta { get; set; } = string.Empty;

    // true cuando el token se generó (o regeneró) en esta llamada
    public bool EsNuevo { get; set; }
}
