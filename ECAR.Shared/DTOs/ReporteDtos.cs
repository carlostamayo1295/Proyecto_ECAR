namespace ECAR.Shared.DTOs;

/// <summary>
/// Reportes de la Fase 4 (SRS §6 y §7; PLAN_FASE4_TAREAS §3.9). Cada reporte es un único
/// endpoint GET /api/reportes/{tipo}?…&amp;formato=json|pdf|xlsx: la pantalla muestra la vista
/// previa con json y descarga con pdf o xlsx, así que la vista previa y el archivo no pueden
/// discrepar.
/// </summary>
public static class ReporteTipos
{
    public const string HistorialEquipo = "historial-equipo";
    public const string HistorialArea = "historial-area";
    public const string HistorialTecnico = "historial-tecnico";
    public const string EquiposNovedades = "equipos-novedades";
    public const string Cumplimiento = "cumplimiento";
    public const string InspeccionesFechas = "inspecciones-fechas";
}

public static class ReporteFormatos
{
    public const string Json = "json";
    public const string Pdf = "pdf";
    public const string Excel = "xlsx";
}

/// <summary>
/// Filtros de los reportes. Cada reporte usa los suyos (ver la tabla de endpoints 15–20);
/// las fechas son obligatorias en todos y el rango máximo es de un año.
/// </summary>
public class ReporteFiltroDto
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public string? Planta { get; set; }
    public string? Area { get; set; }
    public long? IdEquipo { get; set; }
    public long? IdUsuario { get; set; }
    public string? Estado { get; set; }
    public string? Criticidad { get; set; }
}

/// <summary>Reporte "Historial por Equipo" (SRS §7).</summary>
public class HistorialEquipoFilaDto
{
    public long IdInspeccion { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string ActivoFijo { get; set; } = string.Empty;
    public string NombreEquipo { get; set; } = string.Empty;
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Area { get; set; }
    public DateTime FechaInspeccion { get; set; }
    public string? Resultado { get; set; }
    public string? Observaciones { get; set; }
    public string Inspector { get; set; } = string.Empty;
}

/// <summary>Reporte "Historial por Área" (SRS §7).</summary>
public class HistorialAreaFilaDto
{
    public long IdInspeccion { get; set; }
    public string Planta { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Equipo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string? Resultado { get; set; }
    public string Responsable { get; set; } = string.Empty;
}

/// <summary>Reporte "Historial por Técnico" (SRS §7).</summary>
public class HistorialTecnicoFilaDto
{
    public long IdInspeccion { get; set; }
    public string Tecnico { get; set; } = string.Empty;
    public string Equipo { get; set; } = string.Empty;
    public DateTime FechaInspeccion { get; set; }
    public string? Resultado { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Reporte "Equipos con Novedades" (SRS §7): una fila por hallazgo.</summary>
public class EquipoNovedadFilaDto
{
    public long IdHallazgo { get; set; }
    public string Equipo { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string Hallazgo { get; set; } = string.Empty;
    public string? Criticidad { get; set; }
    public DateTime Fecha { get; set; }
    public string? Responsable { get; set; }
    public string Estado { get; set; } = string.Empty;
}

/// <summary>
/// Reporte "Cumplimiento de Inspecciones" (SRS §7). Los programados salen de
/// Equipos.FrecuenciaInspeccion, pendiente de confirmar con ECAR.
/// </summary>
public class CumplimientoFilaDto
{
    /// <summary>Etiqueta del periodo, p. ej. "2026-10" o "Semana 41".</summary>
    public string Periodo { get; set; } = string.Empty;
    public string Planta { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public int EquiposProgramados { get; set; }
    public int EquiposInspeccionados { get; set; }

    /// <summary>0–100; null si en ese periodo no había ningún equipo programado.</summary>
    public decimal? PorcentajeCumplimiento { get; set; }
}

/// <summary>Reporte "Inspecciones por Fechas" (SRS §7): totales del rango y desglose por área.</summary>
public class InspeccionesPorFechasDto
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public int TotalInspecciones { get; set; }

    /// <summary>"Inspecciones satisfactorias" del SRS: resultado Conforme.</summary>
    public int Satisfactorias { get; set; }

    public int ConNovedad { get; set; }
    public List<InspeccionesPorFechasFilaDto> PorArea { get; set; } = new();
}

public class InspeccionesPorFechasFilaDto
{
    public string Planta { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public int TotalInspecciones { get; set; }
    public int Satisfactorias { get; set; }
    public int ConNovedad { get; set; }
}
