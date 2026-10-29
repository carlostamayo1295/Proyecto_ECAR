namespace ECAR.Shared.DTOs;

/// <summary>
/// Indicadores de la portada (SRS §6 "Dashboard"): equipos, inspecciones y novedades. Lo calcula
/// el servidor en un solo viaje; las fechas del periodo son las de Colombia (UTC−5).
/// </summary>
public class DashboardDto
{
    /// <summary>Periodo y planta que el servidor aplicó realmente a los indicadores.</summary>
    public DashboardFiltroAplicadoDto Filtros { get; set; } = new();

    /// <summary>Equipos activos.</summary>
    public int TotalEquipos { get; set; }

    /// <summary>Equipos activos por criticidad (Alta, Media, Baja y "Sin criticidad").</summary>
    public List<ConteoDto> EquiposPorCriticidad { get; set; } = new();

    /// <summary>Equipos activos por "Planta - Área", de más a menos.</summary>
    public List<ConteoDto> EquiposPorArea { get; set; } = new();

    /// <summary>Inspecciones firmadas desde el inicio, tengan o no novedades pendientes.</summary>
    public int InspeccionesRealizadas { get; set; }

    /// <summary>
    /// Inspecciones firmadas en el periodo aplicado. El nombre se conserva por compatibilidad con
    /// el cliente anterior a los filtros de la Fase 5.
    /// </summary>
    public int InspeccionesRealizadasMes { get; set; }

    /// <summary>Inspecciones iniciadas y todavía sin firmar.</summary>
    public int InspeccionesEnCurso { get; set; }

    /// <summary>Inspecciones firmadas que esperan a que se resuelvan sus novedades.</summary>
    public int InspeccionesConNovedadesPendientes { get; set; }

    /// <summary>Novedades sin resolver ("hallazgos abiertos" del SRS §6).</summary>
    public int NovedadesPendientes { get; set; }

    /// <summary>Novedades resueltas con firma ("hallazgos cerrados" del SRS §6).</summary>
    public int NovedadesResueltas { get; set; }

    /// <summary>Descripción legible del periodo aplicado.</summary>
    public string Periodo { get; set; } = string.Empty;

    public DateTime GeneradoEn { get; set; }
}

public class ConteoDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}

/// <summary>
/// Filtros efectivos del tablero. Las fechas son días de Colombia e incluyen ambos extremos.
/// </summary>
public class DashboardFiltroAplicadoDto
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    /// <summary>Plantas aplicadas; vacía, todas (desde el 29/10/2026 se pueden elegir varias).</summary>
    public List<string> Plantas { get; set; } = new();
}

/// <summary>Tendencia del tablero y filtros efectivos usados para calcularla.</summary>
public class DashboardTendenciaDto
{
    public int Semanas { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public List<string> Plantas { get; set; } = new();
    public List<DashboardSemanaDto> Datos { get; set; } = new();
}

/// <summary>Conteos de una semana colombiana, desde el lunes hasta el domingo.</summary>
public class DashboardSemanaDto
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public int InspeccionesFirmadas { get; set; }
    public int NovedadesRegistradas { get; set; }
    public int NovedadesResueltas { get; set; }
}
