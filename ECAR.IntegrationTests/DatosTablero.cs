using ECAR.API.Services;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;

namespace ECAR.IntegrationTests;

/// <summary>
/// Siembra equipos, inspecciones y novedades para las pruebas del tablero (B-08). Cada instancia
/// usa una planta propia, así que las pruebas de una clase pueden compartir la base: el tablero
/// cuenta todo lo de la base y la planta es lo que las separa. Las fechas se dan en hora de
/// Colombia y se guardan en UTC, como hace el API.
/// </summary>
internal sealed class DatosTablero(BaseDePruebas baseDePruebas)
{
    private Usuario? _usuario;
    private Checklist? _checklist;

    public string Planta { get; } = "Planta " + Guid.NewGuid().ToString("N")[..10];

    /// <summary>Hora de Colombia → UTC (Colombia está en UTC−5 todo el año).</summary>
    public static DateTime Utc(DateTime colombia) => DateTime.SpecifyKind(colombia - HoraColombia.Desfase, DateTimeKind.Utc);

    public async Task<Equipo> EquipoAsync(string area, string? criticidad, bool activo = true)
    {
        var codigo = "TB-" + Guid.NewGuid().ToString("N")[..12];
        await using var db = baseDePruebas.CrearContexto();
        var ubicacion = db.Ubicaciones.FirstOrDefault(u => u.Planta == Planta && u.Area == area)
            ?? new Ubicacion { Planta = Planta, Area = area };
        var equipo = new Equipo
        {
            CodigoInterno = codigo,
            ActivoFijo = "AF-" + codigo,
            NombreEquipo = "Equipo " + codigo,
            Criticidad = criticidad,
            Activo = activo,
            Ubicacion = ubicacion
        };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();
        return equipo;
    }

    /// <summary>Una inspección con el estado y las fechas dadas (UTC).</summary>
    public async Task<long> InspeccionAsync(Equipo equipo, string estado, DateTime fechaInspeccionUtc, DateTime? fechaCierreUtc = null)
    {
        await using var db = baseDePruebas.CrearContexto();
        if (_usuario == null)
        {
            _usuario = new Usuario { Nombre = "Inspector " + Planta, Correo = Guid.NewGuid().ToString("N") + "@ecar.test", PasswordHash = "hash" };
            _checklist = new Checklist { Nombre = "Checklist " + Planta, Version = "1.0" };
            db.AddRange(_usuario, _checklist);
            await db.SaveChangesAsync();
        }

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = _usuario.IdUsuario,
            IdChecklist = _checklist!.IdChecklist,
            Estado = estado,
            FechaInspeccion = fechaInspeccionUtc,
            FechaCierre = fechaCierreUtc
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();
        return inspeccion.IdInspeccion;
    }

    /// <summary>
    /// Una novedad de la inspección. La base admite una sola novedad por pregunta y por respuesta,
    /// así que cada una lleva su propia pregunta "No".
    /// </summary>
    public async Task NovedadAsync(long idInspeccion, string estado, DateTime fechaRegistroUtc, DateTime? fechaResolucionUtc = null)
    {
        await using var db = baseDePruebas.CrearContexto();
        var pregunta = new PreguntaChecklist
        {
            IdChecklist = _checklist!.IdChecklist,
            Pregunta = "¿Punto " + Guid.NewGuid().ToString("N")[..6] + " bien?",
            TipoRespuesta = TiposRespuesta.SiNo,
            Obligatoria = true,
            Orden = 1
        };
        var respuesta = new RespuestaInspeccion { IdInspeccion = idInspeccion, Pregunta = pregunta, Respuesta = "No", Observacion = "Falla" };
        db.RespuestasInspeccion.Add(respuesta);
        await db.SaveChangesAsync();

        db.Novedades.Add(new Novedad
        {
            IdInspeccion = idInspeccion,
            IdPregunta = pregunta.IdPregunta,
            IdRespuesta = respuesta.IdRespuesta,
            Descripcion = pregunta.Pregunta + ": Falla",
            Estado = estado,
            FechaRegistro = fechaRegistroUtc,
            IdUsuarioRegistro = _usuario!.IdUsuario,
            FechaResolucion = fechaResolucionUtc,
            IdUsuarioResolucion = fechaResolucionUtc.HasValue ? _usuario.IdUsuario : null,
            Solucion = fechaResolucionUtc.HasValue ? "Se corrigió la falla" : null
        });
        await db.SaveChangesAsync();
    }
}
