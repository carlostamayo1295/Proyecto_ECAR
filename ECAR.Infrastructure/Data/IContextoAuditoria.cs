namespace ECAR.Infrastructure.Data;

/// <summary>
/// Quién, desde dónde y por qué se hace un cambio. ECARDbContext lo lee al escribir cada fila de
/// auditoría (PLAN_FASE4_TAREAS §3.3). En el API lo implementa ContextoAuditoriaHttp con el
/// usuario del token, la IP y el agente de la petición y el motivo que fijó el controlador.
/// </summary>
public interface IContextoAuditoria
{
    /// <summary>Id del usuario autenticado; null en procesos del sistema y peticiones anónimas.</summary>
    long? IdUsuario { get; }

    /// <summary>Nombre y correo tal como son en ese momento; "Sistema" o "Anónimo" si no hay usuario.</summary>
    string Usuario { get; }

    string? DireccionIp { get; }

    string? AgenteUsuario { get; }

    /// <summary>Motivo del cambio (IMotivoCambio); se copia en todas las filas de ese guardado.</summary>
    string? Motivo { get; }
}
