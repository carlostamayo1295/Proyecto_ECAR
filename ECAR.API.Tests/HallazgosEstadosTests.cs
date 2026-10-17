using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 6 de hallazgos: la matriz de <see cref="TransicionesHallazgo"/> aplicada por
/// POST {id}/estado, POST {id}/anular y PUT {id}. Transición inválida: 409; cerrar sin acción
/// correctiva o anular/reabrir sin motivo: 400; anular o reabrir sin ser Administrador: 403.
/// </summary>
public class HallazgosEstadosTests
{
    private const string MotivoValido = "Se registró sobre el equipo equivocado";

    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Usuario";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Como ContextoAuditoriaHttp: el motivo de la auditoría es el que fijó el controlador.</summary>
    private sealed class ContextoAuditoriaFalso(IMotivoCambio motivoCambio) : IContextoAuditoria
    {
        public long? IdUsuario => null;
        public string Usuario => "Pruebas";
        public string? DireccionIp => null;
        public string? AgenteUsuario => null;
        public string? Motivo => motivoCambio.Motivo;
    }

    private sealed class InterceptorDeConcurrencia : SaveChangesInterceptor
    {
        public bool Fallar { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Fallar && eventData.Context!.ChangeTracker.Entries<Hallazgo>().Any(e => e.State == EntityState.Modified)
                ? throw new DbUpdateConcurrencyException("Otra petición cambió el hallazgo")
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private sealed record Escenario(ECARDbContext Context, MotivoCambio Motivo, InterceptorDeConcurrencia Interceptor,
        Usuario Admin, Usuario Tecnico, Hallazgo Hallazgo);

    private static async Task<Escenario> SeedAsync(string estado)
    {
        var motivo = new MotivoCambio();
        var interceptor = new InterceptorDeConcurrencia();
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options, new ContextoAuditoriaFalso(motivo));

        var admin = new Usuario { Nombre = "Admin", Correo = "admin@ecar.test", PasswordHash = "hash", Activo = true };
        var tecnico = new Usuario { Nombre = "Técnico", Correo = "tecnico@ecar.test", PasswordHash = "hash", Activo = true };
        var equipo = new Equipo { CodigoInterno = "EQ-001", ActivoFijo = "AF-001", NombreEquipo = "Balanza", Activo = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true };
        context.AddRange(admin, tecnico, equipo, checklist);
        await context.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = tecnico.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.Cerrada
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var hallazgo = new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Descripcion = "Fuga de aceite",
            Estado = estado,
            Origen = HallazgoOrigenes.Manual,
            IdUsuarioRegistro = tecnico.IdUsuario
        };
        if (estado == HallazgoEstados.Cerrado)
        {
            hallazgo.AccionCorrectiva = "Se cambió el empaque";
            hallazgo.FechaCierre = DateTime.UtcNow.AddDays(-1);
            hallazgo.IdUsuarioCierre = tecnico.IdUsuario;
        }

        context.Hallazgos.Add(hallazgo);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Escenario(context, motivo, interceptor, admin, tecnico, hallazgo);
    }

    private static HallazgosController Como(Escenario e, string rol) =>
        new(e.Context,
            rol == "Administrador"
                ? new FakeCurrentUser(e.Admin.IdUsuario, "Administrador")
                : new FakeCurrentUser(e.Tecnico.IdUsuario, rol),
            e.Motivo);

    private static Task<ActionResult<ApiResponse<HallazgoDto>>> CambiarAsync(Escenario e, string destino,
        string? accionCorrectiva = null, string? motivo = null, string rol = "Técnico") =>
        Como(e, rol).CambiarEstado(e.Hallazgo.IdHallazgo,
            new CambiarEstadoHallazgoDto { Estado = destino, AccionCorrectiva = accionCorrectiva, Motivo = motivo });

    private static Task<ActionResult<ApiResponse<HallazgoDto>>> AnularAsync(Escenario e, string motivo, string rol = "Administrador") =>
        Como(e, rol).AnularHallazgo(e.Hallazgo.IdHallazgo, new MotivoDto { Motivo = motivo });

    private static Task<Hallazgo> GuardadoAsync(Escenario e) =>
        e.Context.Hallazgos.AsNoTracking().SingleAsync(h => h.IdHallazgo == e.Hallazgo.IdHallazgo);

    private static Task<Auditoria> UltimaAuditoriaAsync(Escenario e) =>
        e.Context.Auditoria.AsNoTracking()
            .Where(a => a.Tabla == "Hallazgos" && a.RegistroId == e.Hallazgo.IdHallazgo)
            .OrderByDescending(a => a.IdAuditoria)
            .FirstAsync();

    private static string Mensaje(IActionResult? resultado) =>
        Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsAssignableFrom<ObjectResult>(resultado).Value).Message;

    // ------------------------------------------------------------------ la matriz

    public static TheoryData<string, string, bool> TodasLasCombinaciones()
    {
        var permitidas = new HashSet<(string, string)>
        {
            (HallazgoEstados.Abierto, HallazgoEstados.EnProceso),
            (HallazgoEstados.Abierto, HallazgoEstados.Cerrado),
            (HallazgoEstados.Abierto, HallazgoEstados.Anulado),
            (HallazgoEstados.EnProceso, HallazgoEstados.Cerrado),
            (HallazgoEstados.EnProceso, HallazgoEstados.Anulado),
            (HallazgoEstados.Cerrado, HallazgoEstados.Abierto)
        };
        var datos = new TheoryData<string, string, bool>();
        foreach (var desde in HallazgoEstados.Todos)
        {
            foreach (var hacia in HallazgoEstados.Todos)
            {
                datos.Add(desde, hacia, permitidas.Contains((desde, hacia)));
            }
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(TodasLasCombinaciones))]
    public void Matriz_SoloPermiteLasSeisTransicionesAcordadas(string desde, string hacia, bool permitida)
    {
        Assert.Equal(permitida, TransicionesHallazgo.Buscar(desde, hacia) != null);
    }

    [Fact]
    public void Matriz_RequisitosDeCadaTransicion()
    {
        Assert.Equal(new TransicionesHallazgo.Regla(false, true, false),
            TransicionesHallazgo.Buscar(HallazgoEstados.Abierto, HallazgoEstados.Cerrado));
        Assert.Equal(new TransicionesHallazgo.Regla(false, true, false),
            TransicionesHallazgo.Buscar(HallazgoEstados.EnProceso, HallazgoEstados.Cerrado));
        Assert.Equal(new TransicionesHallazgo.Regla(true, false, true),
            TransicionesHallazgo.Buscar(HallazgoEstados.Abierto, HallazgoEstados.Anulado));
        Assert.Equal(new TransicionesHallazgo.Regla(true, false, true),
            TransicionesHallazgo.Buscar(HallazgoEstados.EnProceso, HallazgoEstados.Anulado));
        Assert.Equal(new TransicionesHallazgo.Regla(true, false, true),
            TransicionesHallazgo.Buscar(HallazgoEstados.Cerrado, HallazgoEstados.Abierto));
        Assert.Equal(new TransicionesHallazgo.Regla(false, false, false),
            TransicionesHallazgo.Buscar(HallazgoEstados.Abierto, HallazgoEstados.EnProceso));
    }

    // ------------------------------------------------------------------ transiciones válidas

    [Fact]
    public async Task Abierto_AEnProceso_LoHaceUnTecnico()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var action = await CambiarAsync(e, HallazgoEstados.EnProceso);

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.Equal(HallazgoEstados.EnProceso, (await GuardadoAsync(e)).Estado);
    }

    [Theory]
    [InlineData(HallazgoEstados.Abierto)]
    [InlineData(HallazgoEstados.EnProceso)]
    public async Task Cerrar_ConAccionCorrectiva_GuardaElCierre(string desde)
    {
        var e = await SeedAsync(desde);

        var action = await CambiarAsync(e, HallazgoEstados.Cerrado, accionCorrectiva: "  Se cambió el empaque  ");

        var dto = Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal("Técnico", dto.NombreUsuarioCierre);
        var guardado = await GuardadoAsync(e);
        Assert.Equal(HallazgoEstados.Cerrado, guardado.Estado);
        Assert.Equal("Se cambió el empaque", guardado.AccionCorrectiva);
        Assert.NotNull(guardado.FechaCierre);
        Assert.Equal(e.Tecnico.IdUsuario, guardado.IdUsuarioCierre);
        Assert.Equal(AuditoriaAcciones.Cerrar, (await UltimaAuditoriaAsync(e)).Accion);
    }

    [Fact]
    public async Task Reabrir_ElAdministradorConMotivo_LimpiaElCierreYConservaLaAccion()
    {
        var e = await SeedAsync(HallazgoEstados.Cerrado);

        var action = await CambiarAsync(e, HallazgoEstados.Abierto, motivo: "La fuga volvió a aparecer", rol: "Administrador");

        Assert.IsType<OkObjectResult>(action.Result);
        var guardado = await GuardadoAsync(e);
        Assert.Equal(HallazgoEstados.Abierto, guardado.Estado);
        Assert.Null(guardado.FechaCierre);
        Assert.Null(guardado.IdUsuarioCierre);
        Assert.Equal("Se cambió el empaque", guardado.AccionCorrectiva);
        var auditoria = await UltimaAuditoriaAsync(e);
        Assert.Equal(AuditoriaAcciones.Reabrir, auditoria.Accion);
        Assert.Equal("La fuga volvió a aparecer", auditoria.Motivo);
    }

    [Theory]
    [InlineData(HallazgoEstados.Abierto)]
    [InlineData(HallazgoEstados.EnProceso)]
    public async Task Anular_ElAdministradorConMotivo_GuardaElMotivo(string desde)
    {
        var e = await SeedAsync(desde);

        var action = await AnularAsync(e, MotivoValido);

        var dto = Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(MotivoValido, dto.MotivoAnulacion);
        var guardado = await GuardadoAsync(e);
        Assert.Equal(HallazgoEstados.Anulado, guardado.Estado);
        Assert.Equal(MotivoValido, guardado.MotivoAnulacion);
        var auditoria = await UltimaAuditoriaAsync(e);
        Assert.Equal(AuditoriaAcciones.Anular, auditoria.Accion);
        Assert.Equal(MotivoValido, auditoria.Motivo);
    }

    [Fact]
    public async Task UnMotivoOpcional_LlegaALaAuditoria()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        await CambiarAsync(e, HallazgoEstados.EnProceso, motivo: "Se pidió el repuesto");

        Assert.Equal("Se pidió el repuesto", (await UltimaAuditoriaAsync(e)).Motivo);
    }

    // ------------------------------------------------------------------ 409: transiciones inválidas

    [Theory]
    [InlineData(HallazgoEstados.Abierto, HallazgoEstados.Abierto)]
    [InlineData(HallazgoEstados.EnProceso, HallazgoEstados.Abierto)]
    [InlineData(HallazgoEstados.EnProceso, HallazgoEstados.EnProceso)]
    [InlineData(HallazgoEstados.Cerrado, HallazgoEstados.EnProceso)]
    [InlineData(HallazgoEstados.Cerrado, HallazgoEstados.Cerrado)]
    [InlineData(HallazgoEstados.Anulado, HallazgoEstados.Abierto)]
    [InlineData(HallazgoEstados.Anulado, HallazgoEstados.EnProceso)]
    [InlineData(HallazgoEstados.Anulado, HallazgoEstados.Cerrado)]
    public async Task TransicionInvalida_Devuelve409YNoCambiaNada(string desde, string hacia)
    {
        var e = await SeedAsync(desde);

        // Como Administrador y con todos los datos: el rechazo es solo por la matriz.
        var action = await CambiarAsync(e, hacia, accionCorrectiva: "Acción", motivo: MotivoValido, rol: "Administrador");

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(desde, (await GuardadoAsync(e)).Estado);
    }

    [Theory]
    [InlineData(HallazgoEstados.Cerrado)]
    [InlineData(HallazgoEstados.Anulado)]
    public async Task Anular_UnHallazgoCerradoOAnulado_Devuelve409(string desde)
    {
        var e = await SeedAsync(desde);

        var action = await AnularAsync(e, MotivoValido);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(desde, (await GuardadoAsync(e)).Estado);
    }

    [Fact]
    public async Task CambioSimultaneo_Devuelve409()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);
        e.Interceptor.Fallar = true;

        var action = await CambiarAsync(e, HallazgoEstados.EnProceso);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeConcurrencia, Mensaje(action.Result));
    }

    // ------------------------------------------------------------------ 400 y 403

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cerrar_SinAccionCorrectiva_Devuelve400YNoCierra(string? accion)
    {
        var e = await SeedAsync(HallazgoEstados.EnProceso);

        var action = await CambiarAsync(e, HallazgoEstados.Cerrado, accionCorrectiva: accion);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeAccionCorrectivaRequerida, Mensaje(action.Result));
        var guardado = await GuardadoAsync(e);
        Assert.Equal(HallazgoEstados.EnProceso, guardado.Estado);
        Assert.Null(guardado.FechaCierre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Corto")]
    public async Task Anular_SinMotivoValido_Devuelve400YNoAnula(string motivo)
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var action = await AnularAsync(e, motivo);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeMotivoRequerido, Mensaje(action.Result));
        var guardado = await GuardadoAsync(e);
        Assert.Equal(HallazgoEstados.Abierto, guardado.Estado);
        Assert.Null(guardado.MotivoAnulacion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Corto")]
    public async Task Reabrir_SinMotivoValido_Devuelve400(string? motivo)
    {
        var e = await SeedAsync(HallazgoEstados.Cerrado);

        var action = await CambiarAsync(e, HallazgoEstados.Abierto, motivo: motivo, rol: "Administrador");

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(HallazgoEstados.Cerrado, (await GuardadoAsync(e)).Estado);
    }

    [Fact]
    public async Task Anular_UnTecnico_Devuelve403()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var action = await AnularAsync(e, MotivoValido, rol: "Técnico");

        Assert.IsType<ForbidResult>(action.Result);
        Assert.Equal(HallazgoEstados.Abierto, (await GuardadoAsync(e)).Estado);
    }

    [Fact]
    public async Task Reabrir_UnTecnico_Devuelve403()
    {
        var e = await SeedAsync(HallazgoEstados.Cerrado);

        var action = await CambiarAsync(e, HallazgoEstados.Abierto, motivo: MotivoValido, rol: "Técnico");

        Assert.IsType<ForbidResult>(action.Result);
        Assert.Equal(HallazgoEstados.Cerrado, (await GuardadoAsync(e)).Estado);
    }

    [Fact]
    public async Task CambiarEstado_AUnEstadoQueNoExiste_Devuelve400()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var action = await CambiarAsync(e, "Pendiente");

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task CambiarEstado_AAnulado_PideUsarElEndpointDeAnular()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var action = await CambiarAsync(e, HallazgoEstados.Anulado, motivo: MotivoValido, rol: "Administrador");

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeAnularPorEstado, Mensaje(action.Result));
        Assert.Equal(HallazgoEstados.Abierto, (await GuardadoAsync(e)).Estado);
    }

    [Fact]
    public async Task CambiarEstado_HallazgoInexistente_Devuelve404()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var action = await Como(e, "Técnico").CambiarEstado(999, new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.EnProceso });

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    // ------------------------------------------------------------------ PUT

    [Fact]
    public async Task Put_ConUnEstadoDistinto_Devuelve400YNoLoCambia()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var action = await Como(e, "Técnico").UpdateHallazgo(e.Hallazgo.IdHallazgo,
            new UpdateHallazgoDto { Descripcion = "Otra", Estado = HallazgoEstados.Cerrado });

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeEstadoPorPut, Mensaje(action.Result));
        var guardado = await GuardadoAsync(e);
        Assert.Equal(HallazgoEstados.Abierto, guardado.Estado);
        Assert.Equal("Fuga de aceite", guardado.Descripcion);
    }

    [Fact]
    public async Task Put_ConElMismoEstado_EditaLaDescripcion()
    {
        var e = await SeedAsync(HallazgoEstados.EnProceso);

        var action = await Como(e, "Técnico").UpdateHallazgo(e.Hallazgo.IdHallazgo,
            new UpdateHallazgoDto { Descripcion = "Fuga de aceite en la base", Estado = HallazgoEstados.EnProceso });

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.Equal("Fuga de aceite en la base", (await GuardadoAsync(e)).Descripcion);
    }

    [Theory]
    [InlineData(HallazgoEstados.Cerrado)]
    [InlineData(HallazgoEstados.Anulado)]
    public async Task Put_SobreUnHallazgoCerradoOAnulado_Devuelve409(string estado)
    {
        var e = await SeedAsync(estado);

        var action = await Como(e, "Administrador").UpdateHallazgo(e.Hallazgo.IdHallazgo,
            new UpdateHallazgoDto { Descripcion = "Otra", Criticidad = "Alta" });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeEstadoFinal, Mensaje(action.Result));
        Assert.Equal("Fuga de aceite", (await GuardadoAsync(e)).Descripcion);
    }

    [Fact]
    public async Task Modelo_RowVersionEsTokenDeConcurrencia()
    {
        var e = await SeedAsync(HallazgoEstados.Abierto);

        var propiedad = e.Context.Model.FindEntityType(typeof(Hallazgo))!.FindProperty(nameof(Hallazgo.RowVersion))!;

        Assert.True(propiedad.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, propiedad.ValueGenerated);
        // El valor inicial de la entidad nunca viaja a SQL Server: la columna la genera la base.
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore, propiedad.GetBeforeSaveBehavior());
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore, propiedad.GetAfterSaveBehavior());
    }
}
