namespace ECAR.Shared.DTOs;

/// <summary>
/// Indicadores de la portada (SRS §6 "Dashboard"): equipos, inspecciones y hallazgos. Lo calcula
/// el servidor en un solo viaje; las fechas del periodo son las de Colombia (UTC−5).
/// </summary>
public class DashboardDto
{
    /// <summary>Equipos activos.</summary>
    public int TotalEquipos { get; set; }

    /// <summary>Equipos activos por criticidad (Alta, Media, Baja y "Sin criticidad").</summary>
    public List<ConteoDto> EquiposPorCriticidad { get; set; } = new();

    /// <summary>Equipos activos por "Planta - Área", de más a menos.</summary>
    public List<ConteoDto> EquiposPorArea { get; set; } = new();

    /// <summary>Inspecciones firmadas desde el inicio.</summary>
    public int InspeccionesRealizadas { get; set; }

    /// <summary>Inspecciones firmadas en el mes en curso.</summary>
    public int InspeccionesRealizadasMes { get; set; }

    /// <summary>Inspecciones iniciadas y todavía sin firmar.</summary>
    public int InspeccionesEnCurso { get; set; }

    /// <summary>
    /// Equipos activos con frecuencia de inspección que aún no tienen una inspección firmada en su
    /// periodo vigente: el mes en curso (o el trimestre, si son trimestrales). Es la misma regla
    /// del reporte de cumplimiento, pendiente de confirmar con ECAR.
    /// </summary>
    public int InspeccionesPendientes { get; set; }

    /// <summary>Equipos activos con frecuencia de inspección (los que entran en el cumplimiento).</summary>
    public int EquiposProgramados { get; set; }

    /// <summary>Hallazgos sin cerrar: Abierto más EnProceso.</summary>
    public int HallazgosAbiertos { get; set; }

    /// <summary>De los abiertos, los que ya están en proceso.</summary>
    public int HallazgosEnProceso { get; set; }

    public int HallazgosCerrados { get; set; }

    /// <summary>"octubre de 2026": el mes al que se refieren los indicadores del periodo.</summary>
    public string Periodo { get; set; } = string.Empty;

    public DateTime GeneradoEn { get; set; }
}

public class ConteoDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}
