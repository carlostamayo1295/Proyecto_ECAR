using ECAR.Shared;

namespace ECAR.API.Services;

/// <summary>
/// Máquina de estados de un hallazgo. Solo existen las transiciones de esta tabla; cualquier
/// otra, incluida la de un estado a sí mismo, es inválida y se responde con 409.
/// </summary>
/// <remarks>
/// <code>
/// Desde \ Hacia | Abierto              | EnProceso | Cerrado                 | Anulado
/// Abierto       | —                    | Sí        | Sí, acción correctiva   | Solo Admin, motivo
/// EnProceso     | —                    | —         | Sí, acción correctiva   | Solo Admin, motivo
/// Cerrado       | Solo Admin, motivo   | —         | —                       | —
/// Anulado       | — (estado final)     | —         | —                       | —
/// </code>
/// Cerrado → Abierto es reabrir. "Sí" vale para Administrador y Técnico.
/// </remarks>
public static class TransicionesHallazgo
{
    /// <summary>Requisitos de una transición permitida.</summary>
    public sealed record Regla(bool SoloAdministrador, bool ExigeAccionCorrectiva, bool ExigeMotivo);

    private static readonly Dictionary<(string Desde, string Hacia), Regla> Permitidas = new()
    {
        [(HallazgoEstados.Abierto, HallazgoEstados.EnProceso)] = new(false, false, false),
        [(HallazgoEstados.Abierto, HallazgoEstados.Cerrado)] = new(false, true, false),
        [(HallazgoEstados.Abierto, HallazgoEstados.Anulado)] = new(true, false, true),
        [(HallazgoEstados.EnProceso, HallazgoEstados.Cerrado)] = new(false, true, false),
        [(HallazgoEstados.EnProceso, HallazgoEstados.Anulado)] = new(true, false, true),
        [(HallazgoEstados.Cerrado, HallazgoEstados.Abierto)] = new(true, false, true)
    };

    /// <summary>La regla de la transición, o null si no está permitida.</summary>
    public static Regla? Buscar(string desde, string hacia) =>
        Permitidas.GetValueOrDefault((desde, hacia));

    /// <summary>Cerrado y Anulado: el hallazgo ya no se edita ni se reasigna.</summary>
    public static bool EsFinal(string estado) =>
        estado is HallazgoEstados.Cerrado or HallazgoEstados.Anulado;
}
