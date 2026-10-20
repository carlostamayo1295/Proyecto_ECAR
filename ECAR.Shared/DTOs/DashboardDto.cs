namespace ECAR.Shared.DTOs;

/// <summary>
/// Indicadores de la portada (SRS §6 "Dashboard"): equipos, inspecciones y novedades. Lo calcula
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

    /// <summary>Inspecciones firmadas desde el inicio, tengan o no novedades pendientes.</summary>
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

    /// <summary>Inspecciones firmadas que esperan a que se resuelvan sus novedades.</summary>
    public int InspeccionesConNovedadesPendientes { get; set; }

    /// <summary>Novedades sin resolver ("hallazgos abiertos" del SRS §6).</summary>
    public int NovedadesPendientes { get; set; }

    /// <summary>Novedades resueltas con firma ("hallazgos cerrados" del SRS §6).</summary>
    public int NovedadesResueltas { get; set; }

    /// <summary>"octubre de 2026": el mes al que se refieren los indicadores del periodo.</summary>
    public string Periodo { get; set; } = string.Empty;

    public DateTime GeneradoEn { get; set; }
}

public class ConteoDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}
