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

    /// <summary>
    /// Equipos activos con al menos una inspección firmada en el periodo (30/10/2026): la parte de los equipos
    /// que cambia con el periodo; el total, la criticidad y el área son el inventario actual.
    /// </summary>
    public int EquiposInspeccionados { get; set; }

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

    /// <summary>
    /// «Total general» (30/10/2026): todo el historial, sin el límite de un año. Desde es el primer día del mes
    /// de la primera inspección (de las plantas elegidas) y Hasta, hoy.
    /// </summary>
    public bool Total { get; set; }
}

/// <summary>Cómo se agrupa la tendencia: por semana (de lunes a domingo) o por mes.</summary>
public static class TendenciaAgrupaciones
{
    public const string Semana = "Semana";
    public const string Mes = "Mes";
}

/// <summary>
/// Tendencia del tablero y filtros efectivos usados para calcularla. Desde el 30/10/2026 sigue el periodo
/// del tablero: por semana hasta 92 días y por mes en un rango más largo o en el total general.
/// </summary>
public class DashboardTendenciaDto
{
    /// <summary>Cantidad de puntos (semanas o meses) de Datos.</summary>
    public int Semanas { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public List<string> Plantas { get; set; } = new();
    /// <summary><see cref="TendenciaAgrupaciones.Semana"/> o <see cref="TendenciaAgrupaciones.Mes"/>.</summary>
    public string Agrupacion { get; set; } = TendenciaAgrupaciones.Semana;
    public bool Total { get; set; }
    public List<DashboardSemanaDto> Datos { get; set; } = new();
}

/// <summary>
/// Conteos de un tramo de la tendencia en hora de Colombia: una semana (de lunes a domingo) o un mes. El primero
/// y el último se recortan al periodo, así que pueden ser más cortos.
/// </summary>
public class DashboardSemanaDto
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public int InspeccionesFirmadas { get; set; }
    public int NovedadesRegistradas { get; set; }
    public int NovedadesResueltas { get; set; }
}
