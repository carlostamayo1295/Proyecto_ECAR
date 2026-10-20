namespace ECAR.Shared.DTOs;

/// <summary>
/// GET /api/inspecciones/{id}/integridad (endpoint 13): el servidor recalcula la huella de la
/// firma con el algoritmo con que se firmó y la compara con la guardada (Parte 11 §11.70).
/// </summary>
public class IntegridadInspeccionDto
{
    /// <summary>true si la inspección no cambió desde que se firmó.</summary>
    public bool Integra { get; set; }

    /// <summary>"SHA256-v0" (cerradas antes de la Fase 4) o "HMACSHA256-v1".</summary>
    public string Algoritmo { get; set; } = string.Empty;

    public DateTime VerificadoEn { get; set; }
}
