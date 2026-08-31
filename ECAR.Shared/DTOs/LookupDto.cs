namespace ECAR.Shared.DTOs;

/// <summary>
/// DTO liviano para poblar listas desplegables (Id + Nombre).
/// </summary>
public class LookupDto
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
