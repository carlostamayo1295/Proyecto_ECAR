using System.ComponentModel.DataAnnotations;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// BE-1, Fase 4 (PLAN_FASE4_TAREAS §3.5 y endpoints 4 a 9): hallazgos automáticos al firmar,
/// ciclo de vida con transiciones validadas, anulación con motivo y sin borrado.
/// </summary>
public class HallazgosFase4Tests
{
    private const string PngValido =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const string MotivoValido = "Seguimiento acordado con mantenimiento";

    private sealed class Usuario_(long id, string nombre, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => id;
        public string Nombre => nombre;
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SinArchivos : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => Task.FromResult("x" + extension);
        public Task<Stream> AbrirAsync(string rutaRelativa) => Task.FromResult<Stream>(new MemoryStream());
        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }

    private sealed record Escenario(ECARDbContext Db, Usuario Tecnico, Usuario OtroTecnico, Usuario Admin, Equipo Equipo,
        Checklist Checklist, PreguntaChecklist P1, PreguntaChecklist P2, PreguntaChecklist P3);

    private static async Task<Escenario> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rolTecnico = new Rol { Nombre = "Técnico" };
        var rolAdmin = new Rol { Nombre = "Administrador" };
        var tecnico = new Usuario { Nombre = "Ana Técnica", Correo = "ana@ecar.test", PasswordHash = "x" };
        var otro = new Usuario { Nombre = "Beto Técnico", Correo = "beto@ecar.test", PasswordHash = "x" };
        var admin = new Usuario { Nombre = "Carla Admin", Correo = "carla@ecar.test", PasswordHash = "x" };
        tecnico.UsuarioRoles.Add(new UsuarioRol { Rol = rolTecnico });
        otro.UsuarioRoles.Add(new UsuarioRol { Rol = rolTecnico });
        admin.UsuarioRoles.Add(new UsuarioRol { Rol = rolAdmin });
        var ubicacion = new Ubicacion { Planta = "Planta Principal", Area = "Producción" };
        var equipo = new Equipo { CodigoInterno = "EQ-7", ActivoFijo = "AF-7", NombreEquipo = "Autoclave", Criticidad = "Alta", Ubicacion = ubicacion };
        var p1 = new PreguntaChecklist { Pregunta = "¿Sello en buen estado?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 1 };
        var p2 = new PreguntaChecklist { Pregunta = "¿Manómetro calibrado?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 2 };
        var p3 = new PreguntaChecklist { Pregunta = "¿Puerta cierra?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 3 };
        var checklist = new Checklist { Nombre = "Autoclave", Version = "1.0", Preguntas = [p1, p2, p3] };
        db.AddRange(tecnico, otro, admin, equipo, checklist);
        await db.SaveChangesAsync();
        return new Escenario(db, tecnico, otro, admin, equipo, checklist, p1, p2, p3);
    }

    private static async Task<Inspeccion> InspeccionAsync(Escenario e, string estado = InspeccionEstados.EnCurso,
        params (PreguntaChecklist Pregunta, string Respuesta, string? Observacion)[] respuestas)
    {
        var inspeccion = new Inspeccion
        {
            IdEquipo = e.Equipo.IdEquipo, IdUsuario = e.Tecnico.IdUsuario, IdChecklist = e.Checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow, Estado = estado
        };
        foreach (var (pregunta, respuesta, observacion) in respuestas)
        {
            inspeccion.Respuestas.Add(new RespuestaInspeccion { IdPregunta = pregunta.IdPregunta, Respuesta = respuesta, Observacion = observacion });
        }

        e.Db.Inspecciones.Add(inspeccion);
        await e.Db.SaveChangesAsync();
        e.Db.ChangeTracker.Clear();
        return inspeccion;
    }

    private static async Task<Hallazgo> HallazgoAsync(Escenario e, string estado, string origen = HallazgoOrigenes.Manual)
    {
        var inspeccion = await InspeccionAsync(e);
        var hallazgo = new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion, Descripcion = "Fuga de vapor", Criticidad = "Media",
            Estado = estado, Origen = origen, IdUsuarioRegistro = e.Tecnico.IdUsuario
        };
        e.Db.Hallazgos.Add(hallazgo);
        await e.Db.SaveChangesAsync();
        e.Db.ChangeTracker.Clear();
        return hallazgo;
    }

    private static HallazgosController Controlador(Escenario e, Usuario quien, params string[] roles) =>
        new(e.Db, new Usuario_(quien.IdUsuario, quien.Nombre, roles), new MotivoCambio());

    private static T Ok<T>(ActionResult<ApiResponse<T>> resultado) =>
        Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;

    [Fact]
    public async Task Firmar_con_dos_No_crea_dos_hallazgos_de_origen_Novedad_en_la_misma_operacion()
    {
        var e = await SeedAsync();
        var inspeccion = await InspeccionAsync(e, InspeccionEstados.EnCurso,
            (e.P1, "No", "Sello agrietado"), (e.P2, "Si", null), (e.P3, "No", "La puerta no ajusta"));
        var controlador = Fabrica.Inspecciones(e.Db, new Usuario_(e.Tecnico.IdUsuario, e.Tecnico.Nombre, "Técnico"), new SinArchivos());

        var resultado = await controlador.FirmarInspeccion(inspeccion.IdInspeccion,
            new FirmarInspeccionDto { Password = Fabrica.ClaveFirma, FirmaPngBase64 = PngValido });

        Assert.IsType<OkObjectResult>(resultado.Result);
        var hallazgos = await e.Db.Hallazgos.AsNoTracking().OrderBy(h => h.IdPregunta).ToListAsync();
        Assert.Equal(2, hallazgos.Count);
        Assert.All(hallazgos, h =>
        {
            Assert.Equal(HallazgoOrigenes.Novedad, h.Origen);
            Assert.Equal(HallazgoEstados.Abierto, h.Estado);
            Assert.Equal("Alta", h.Criticidad); // la del equipo
            Assert.Equal(e.Tecnico.IdUsuario, h.IdUsuarioRegistro);
        });
        Assert.Equal("¿Sello en buen estado?: Sello agrietado", hallazgos[0].Descripcion);
        Assert.Equal(e.P3.IdPregunta, hallazgos[1].IdPregunta);
    }

    [Fact]
    public async Task Firmar_una_inspeccion_conforme_no_crea_hallazgos()
    {
        var e = await SeedAsync();
        var inspeccion = await InspeccionAsync(e, InspeccionEstados.EnCurso, (e.P1, "Si", null), (e.P2, "Si", null), (e.P3, "Si", null));

        await Fabrica.Inspecciones(e.Db, new Usuario_(e.Tecnico.IdUsuario, e.Tecnico.Nombre, "Técnico"), new SinArchivos())
            .FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto { Password = Fabrica.ClaveFirma, FirmaPngBase64 = PngValido });

        Assert.Empty(await e.Db.Hallazgos.ToListAsync());
    }

    [Fact]
    public async Task El_detalle_trae_la_pregunta_y_la_observacion_de_origen()
    {
        var e = await SeedAsync();
        var inspeccion = await InspeccionAsync(e, InspeccionEstados.EnCurso, (e.P1, "No", "Sello agrietado"), (e.P2, "Si", null), (e.P3, "Si", null));
        await Fabrica.Inspecciones(e.Db, new Usuario_(e.Tecnico.IdUsuario, e.Tecnico.Nombre, "Técnico"), new SinArchivos())
            .FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto { Password = Fabrica.ClaveFirma, FirmaPngBase64 = PngValido });
        var id = (await e.Db.Hallazgos.SingleAsync()).IdHallazgo;

        var detalle = Ok(await Controlador(e, e.Admin, "Auditor").GetHallazgo(id));

        Assert.Equal("¿Sello en buen estado?", detalle.Pregunta);
        Assert.Equal("Sello agrietado", detalle.ObservacionRespuesta);
        Assert.Equal(InspeccionEstados.Cerrada, detalle.EstadoInspeccion);
        Assert.Equal("Ana Técnica", detalle.NombreInspector);
        Assert.Equal("Planta Principal - Producción", detalle.Area);
        Assert.Equal("Ana Técnica", detalle.NombreUsuarioRegistro);
    }

    [Fact]
    public async Task Abierto_a_Cerrado_sin_pasar_por_EnProceso_es_409()
    {
        var e = await SeedAsync();
        var h = await HallazgoAsync(e, HallazgoEstados.Abierto);

        var resultado = await Controlador(e, e.Tecnico, "Técnico").CambiarEstado(h.IdHallazgo,
            new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.Cerrado, AccionCorrectiva = "Cambio de sello", Motivo = MotivoValido });

        Assert.IsType<ConflictObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task Cerrar_sin_accion_correctiva_es_400_y_con_ella_registra_quien_y_cuando()
    {
        var e = await SeedAsync();
        var h = await HallazgoAsync(e, HallazgoEstados.EnProceso);
        var controlador = Controlador(e, e.Tecnico, "Técnico");

        var sinAccion = await controlador.CambiarEstado(h.IdHallazgo,
            new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.Cerrado, Motivo = MotivoValido });
        Assert.IsType<BadRequestObjectResult>(sinAccion.Result);

        var cerrado = Ok(await controlador.CambiarEstado(h.IdHallazgo,
            new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.Cerrado, AccionCorrectiva = "Se cambió el sello", Motivo = MotivoValido }));
        Assert.Equal(HallazgoEstados.Cerrado, cerrado.Estado);
        Assert.Equal("Se cambió el sello", cerrado.AccionCorrectiva);
        Assert.Equal("Ana Técnica", cerrado.NombreUsuarioCierre);
        Assert.NotNull(cerrado.FechaCierre);
    }

    [Fact]
    public async Task Cambiar_de_estado_sin_motivo_es_400()
    {
        var e = await SeedAsync();
        var h = await HallazgoAsync(e, HallazgoEstados.Abierto);

        var resultado = await Controlador(e, e.Tecnico, "Técnico").CambiarEstado(h.IdHallazgo,
            new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.EnProceso, Motivo = "corto" });

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.Equal(HallazgoEstados.Abierto, (await e.Db.Hallazgos.AsNoTracking().SingleAsync()).Estado);
    }

    [Fact]
    public async Task Reabrir_es_solo_del_Administrador()
    {
        var e = await SeedAsync();
        var h = await HallazgoAsync(e, HallazgoEstados.Cerrado);
        var dto = new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.Abierto, Motivo = "La fuga volvió a aparecer" };

        Assert.IsType<ForbidResult>((await Controlador(e, e.Tecnico, "Técnico").CambiarEstado(h.IdHallazgo, dto)).Result);
        var reabierto = Ok(await Controlador(e, e.Admin, "Administrador").CambiarEstado(h.IdHallazgo, dto));
        Assert.Equal(HallazgoEstados.Abierto, reabierto.Estado);
        Assert.Null(reabierto.FechaCierre);
    }

    [Fact]
    public async Task Un_tecnico_ajeno_no_gestiona_el_hallazgo()
    {
        var e = await SeedAsync();
        var h = await HallazgoAsync(e, HallazgoEstados.Abierto);

        var resultado = await Controlador(e, e.OtroTecnico, "Técnico").CambiarEstado(h.IdHallazgo,
            new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.EnProceso, Motivo = MotivoValido });

        Assert.IsType<ForbidResult>(resultado.Result);
    }

    [Fact]
    public async Task Anular_guarda_el_motivo_y_un_hallazgo_cerrado_no_se_anula()
    {
        var e = await SeedAsync();
        var abierto = await HallazgoAsync(e, HallazgoEstados.Abierto);
        var cerrado = await HallazgoAsync(e, HallazgoEstados.Cerrado);
        var admin = Controlador(e, e.Admin, "Administrador");

        var anulado = Ok(await admin.AnularHallazgo(abierto.IdHallazgo, new MotivoDto { Motivo = "Registrado dos veces" }));
        Assert.Equal(HallazgoEstados.Anulado, anulado.Estado);
        Assert.Equal("Registrado dos veces", anulado.MotivoAnulacion);
        Assert.IsType<ConflictObjectResult>((await admin.AnularHallazgo(cerrado.IdHallazgo,
            new MotivoDto { Motivo = "Registrado dos veces" })).Result);
        Assert.Equal(2, await e.Db.Hallazgos.CountAsync()); // nada se borra
    }

    [Fact]
    public void Anular_sin_motivo_no_pasa_la_validacion_del_contrato()
    {
        static bool Valido(MotivoDto dto) => Validator.TryValidateObject(dto, new ValidationContext(dto), [], true);
        Assert.False(Valido(new MotivoDto { Motivo = "corto" }));
        Assert.False(Valido(new MotivoDto()));
        Assert.True(Valido(new MotivoDto { Motivo = "Registrado dos veces" }));
    }

    [Fact]
    public async Task Editar_no_cambia_la_descripcion_de_una_Novedad_ni_un_hallazgo_cerrado()
    {
        var e = await SeedAsync();
        var novedad = await HallazgoAsync(e, HallazgoEstados.Abierto, HallazgoOrigenes.Novedad);
        var cerrado = await HallazgoAsync(e, HallazgoEstados.Cerrado);
        var controlador = Controlador(e, e.Tecnico, "Técnico");

        var cambiaDescripcion = await controlador.UpdateHallazgo(novedad.IdHallazgo,
            new UpdateHallazgoDto { Descripcion = "Otra cosa", Motivo = MotivoValido });
        var editaCerrado = await controlador.UpdateHallazgo(cerrado.IdHallazgo,
            new UpdateHallazgoDto { Criticidad = "Baja", Motivo = MotivoValido });

        Assert.IsType<BadRequestObjectResult>(cambiaDescripcion.Result);
        Assert.IsType<ConflictObjectResult>(editaCerrado.Result);
    }

    [Fact]
    public async Task Editar_asigna_responsable_valido_y_rechaza_uno_sin_rol()
    {
        var e = await SeedAsync();
        var h = await HallazgoAsync(e, HallazgoEstados.Abierto);
        var sinRol = new Usuario { Nombre = "Sin rol", Correo = "sinrol@ecar.test", PasswordHash = "x" };
        e.Db.Usuarios.Add(sinRol);
        await e.Db.SaveChangesAsync();
        var controlador = Controlador(e, e.Admin, "Administrador");

        var invalido = await controlador.UpdateHallazgo(h.IdHallazgo,
            new UpdateHallazgoDto { IdUsuarioResponsable = sinRol.IdUsuario, Motivo = MotivoValido });
        var valido = Ok(await controlador.UpdateHallazgo(h.IdHallazgo, new UpdateHallazgoDto
        {
            IdUsuarioResponsable = e.OtroTecnico.IdUsuario,
            FechaCompromiso = DateTime.Today.AddDays(5),
            Criticidad = "Baja",
            Motivo = MotivoValido
        }));

        Assert.IsType<BadRequestObjectResult>(invalido.Result);
        Assert.Equal("Beto Técnico", valido.NombreResponsable);
        Assert.Equal("Baja", valido.Criticidad);
        Assert.Equal(DateTime.Today.AddDays(5), valido.FechaCompromiso!.Value.Date);
    }

    [Fact]
    public async Task Alta_manual_queda_como_Manual_y_rechaza_inspecciones_anuladas()
    {
        var e = await SeedAsync();
        var enCurso = await InspeccionAsync(e);
        var anulada = await InspeccionAsync(e, InspeccionEstados.Anulada);
        var controlador = Controlador(e, e.Tecnico, "Técnico");

        var creado = await controlador.CreateHallazgo(new CreateHallazgoDto { IdInspeccion = enCurso.IdInspeccion, Descripcion = "Ruido anormal" });
        var rechazado = await controlador.CreateHallazgo(new CreateHallazgoDto { IdInspeccion = anulada.IdInspeccion, Descripcion = "Ruido" });

        var dto = Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsType<CreatedAtActionResult>(creado.Result).Value).Data!;
        Assert.Equal(HallazgoOrigenes.Manual, dto.Origen);
        Assert.Equal("Alta", dto.Criticidad);
        Assert.Equal("Ana Técnica", dto.NombreUsuarioRegistro);
        Assert.IsType<BadRequestObjectResult>(rechazado.Result);
    }

    [Fact]
    public async Task El_listado_filtra_por_estado_equipo_y_texto()
    {
        var e = await SeedAsync();
        await HallazgoAsync(e, HallazgoEstados.Abierto);
        await HallazgoAsync(e, HallazgoEstados.Cerrado);
        var controlador = Controlador(e, e.Admin, "Auditor");

        var abiertos = Ok(await controlador.GetHallazgos(estado: HallazgoEstados.Abierto));
        var porEquipo = Ok(await controlador.GetHallazgos(idEquipo: e.Equipo.IdEquipo));
        var porTexto = Ok(await controlador.GetHallazgos(search: "Autoclave"));
        var hoy = DateTime.Today;
        var porFecha = Ok(await controlador.GetHallazgos(desde: hoy.AddDays(-1), hasta: hoy.AddDays(1)));
        var futuro = Ok(await controlador.GetHallazgos(desde: hoy.AddDays(10), hasta: hoy.AddDays(11)));

        Assert.Equal(1, abiertos.TotalCount);
        Assert.Equal(2, porEquipo.TotalCount);
        Assert.Equal(2, porTexto.TotalCount);
        Assert.Equal(2, porFecha.TotalCount);
        Assert.Equal(0, futuro.TotalCount);
        Assert.All(porEquipo.Data, h => Assert.Equal("Planta Principal - Producción", h.Area));
    }

    [Fact]
    public void Hallazgos_NoTieneDelete() =>
        Assert.DoesNotContain(typeof(HallazgosController).GetMethods(), m =>
            m.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Length > 0);
}
