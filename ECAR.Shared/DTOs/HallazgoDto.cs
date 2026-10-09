namespace ECAR.Shared.DTOs;

public class HallazgoDto
{
    public long IdHallazgo { get; set; }
    public long IdInspeccion { get; set; }
    public long? IdPregunta { get; set; } // Debe ser long? para permitir valores nulos
    public string Descripcion { get; set; } = string.Empty;
    public string Criticidad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}

public class HallazgoDetalleDto : HallazgoDto
{
    public string? NombreEquipo { get; set; }
    public string? NombreUsuario { get; set; }
}

public class ActualizarEstadoHallazgoDto
{
    public string NuevoEstado { get; set; } = string.Empty;
}