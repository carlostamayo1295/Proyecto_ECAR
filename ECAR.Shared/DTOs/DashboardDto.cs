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

/// <summary>
/// Filtros del tablero (B-01), los mismos para la pantalla y para el PDF (B-03). Sin filtros el
/// tablero es el de siempre. Planta, área y criticidad acotan los equipos y, por el equipo, sus
/// inspecciones y novedades; las fechas son días de Colombia (incluidos los dos extremos) y acotan
/// las inspecciones por su fecha y las novedades por la de registro, no el inventario ni el mes.
/// </summary>
public class DashboardFiltroDto
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public string? Planta { get; set; }
    public string? Area { get; set; }

    /// <summary>Alta, Media o Baja.</summary>
    public string? Criticidad { get; set; }
}

public class ConteoDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}
