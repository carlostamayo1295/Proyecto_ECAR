using System.Threading.Channels;

namespace ECAR.API.Services.Notificaciones;

/// <summary>
/// Avisa a la mesa de ayuda de las novedades de una inspección: un solo correo por inspección, con
/// todas sus novedades. Encolar no espera al servidor de correo: la firma de la inspección no se
/// demora ni se revierte si el correo falla.
/// </summary>
public interface INotificadorNovedades
{
    void Encolar(long idInspeccion);
}

/// <summary>
/// Cola en memoria que vacía <see cref="EnvioNotificacionesService"/>. Lo que quede en ella si el
/// API se detiene no se pierde del todo: esas novedades siguen con FechaNotificacion en null y el
/// Administrador puede reenviar el correo desde la pantalla de novedades.
/// </summary>
public sealed class ColaNotificaciones : INotificadorNovedades
{
    private readonly Channel<long> _canal = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<long> Lector => _canal.Reader;

    public void Encolar(long idInspeccion) => _canal.Writer.TryWrite(idInspeccion);
}
