namespace ECAR.Shared.DTOs;

/// <summary>
/// Una fila de la auditoría. Las propiedades a partir de IdUsuario son de la Fase 4
/// (PLAN_FASE4_TAREAS §3.1 y §3.3); mientras BE-0 no publique la migración llegan vacías.
/// </summary>
public class AuditoriaDto
{
    public long IdAuditoria { get; set; }
    public string Tabla { get; set; } = string.Empty;
    public long RegistroId { get; set; }

    /// <summary>Una de <see cref="AuditoriaAcciones"/>.</summary>
    public string Accion { get; set; } = string.Empty;

    /// <summary>JSON con los campos antes del cambio (solo los cambiados en "Modificar").</summary>
    public string? ValorAnterior { get; set; }

    /// <summary>JSON con los campos después del cambio.</summary>
    public string? ValorNuevo { get; set; }

    /// <summary>Nombre y correo del usuario tal como eran en ese momento.</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Hora del servidor, en UTC.</summary>
    public DateTime FechaHora { get; set; }

    public long? IdUsuario { get; set; }

    /// <summary>Motivo del cambio, obligatorio en anulaciones, cambios de estado y similares.</summary>
    public string? Motivo { get; set; }

    public string? DireccionIp { get; set; }
    public string? AgenteUsuario { get; set; }

    /// <summary>Huella SHA-256 de la fila, encadenada con la anterior.</summary>
    public string? Hash { get; set; }
}

/// <summary>
/// Filtros de GET /api/auditoria (endpoint 1) y de la exportación (endpoint 21). Todos opcionales.
/// </summary>
public class AuditoriaFiltroDto
{
    /// <summary>Texto libre sobre tabla, acción y usuario (el filtro que ya existía).</summary>
    public string? Search { get; set; }

    public string? Tabla { get; set; }
    public long? RegistroId { get; set; }
    public long? IdUsuario { get; set; }
    public string? Accion { get; set; }

    /// <summary>Desde este día, incluido (fecha local; el servidor la convierte a UTC).</summary>
    public DateTime? Desde { get; set; }

    /// <summary>Hasta este día, incluido.</summary>
    public DateTime? Hasta { get; set; }
}

/// <summary>Resultado de GET /api/auditoria/verificar (endpoint 3).</summary>
public class VerificacionAuditoriaDto
{
    /// <summary>true si la cadena de hashes está completa y ninguna fila fue alterada.</summary>
    public bool Valida { get; set; }

    public int FilasRevisadas { get; set; }

    /// <summary>IdAuditoria de la primera fila cuyo hash no cuadra; null si la cadena es válida.</summary>
    public long? PrimeraFilaRota { get; set; }

    public DateTime VerificadoEn { get; set; }
}
