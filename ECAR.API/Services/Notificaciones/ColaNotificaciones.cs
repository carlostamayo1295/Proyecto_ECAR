using System.Threading.Channels;

namespace ECAR.API.Services.Notificaciones;

/// <summary>
/// Avisa a la mesa de ayuda de cada novedad nueva. Encolar no espera al servidor de correo: la
/// firma de la inspección no se demora ni se revierte si el correo falla.
/// </summary>
public interface INotificadorNovedades
{
    void Encolar(IEnumerable<long> idsNovedad);
}

/// <summary>
/// Cola en memoria que vacía <see cref="EnvioNotificacionesService"/>. Lo que quede en ella si el
/// API se detiene no se pierde del todo: esas novedades siguen con FechaNotificacion en null y el
/// Administrador puede reenviarlas desde la pantalla de novedades.
/// </summary>
public sealed class ColaNotificaciones : INotificadorNovedades
{
    private readonly Channel<long> _canal = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<long> Lector => _canal.Reader;

    public void Encolar(IEnumerable<long> idsNovedad)
    {
        foreach (var id in idsNovedad)
        {
            _canal.Writer.TryWrite(id);
        }
    }
}
