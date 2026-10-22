using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FilaAuditoria = ECAR.Infrastructure.Entities.Auditoria;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 5 de auditoría: GET api/auditoria/registro/{tabla}/{registroId} devuelve el historial
/// completo de un registro, de la creación al último cambio, sin mezclar otros registros.
/// </summary>
public class AuditoriaHistorialTests
{
    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Usuario";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeStorage : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => Task.FromResult("x");
        public Task<Stream> AbrirAsync(string rutaRelativa) => Task.FromResult<Stream>(new MemoryStream());
        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }

    private sealed class ContextoAuditoriaFalso(IMotivoCambio motivoCambio) : IContextoAuditoria
    {
        public long? IdUsuario => null;
        public string Usuario => "Pruebas";
        public string? DireccionIp => null;
        public string? AgenteUsuario => null;
        public string? Motivo => motivoCambio.Motivo;
    }

    private static readonly DateTime Base = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static FilaAuditoria Fila(string tabla, long registroId, string accion, DateTime fecha) => new()
    {
        Tabla = tabla,
        RegistroId = registroId,
        Accion = accion,
        Usuario = "Ana",
        FechaHora = fecha,
        Hash = new string('A', 64)
    };

    private static ECARDbContext NuevoContexto(IContextoAuditoria? contexto = null) =>
        new(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, contexto);

    private static async Task<List<AuditoriaDto>> HistorialAsync(ECARDbContext context, string tabla, long registroId)
    {
        var action = await new AuditoriaController(context).GetHistorialRegistro(tabla, registroId);
        return Assert.IsType<ApiResponse<List<AuditoriaDto>>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
    }

    [Fact]
    public async Task DevuelveSoloEseRegistroEnOrdenCronologico()
    {
        await using var context = NuevoContexto();
        // Se insertan desordenadas, junto con filas de otro registro y de otra tabla con el mismo id.
        var cerrar = Fila("Inspecciones", 10, AuditoriaAcciones.Firmar, Base.AddHours(3));
        var crear = Fila("Inspecciones", 10, AuditoriaAcciones.Crear, Base);
        var modificar = Fila("Inspecciones", 10, AuditoriaAcciones.Modificar, Base.AddHours(1));
        context.Auditoria.AddRange(cerrar, crear, modificar,
            Fila("Inspecciones", 11, AuditoriaAcciones.Crear, Base.AddMinutes(30)),
            Fila("Novedades", 10, AuditoriaAcciones.Crear, Base.AddMinutes(45)));
        await context.SaveChangesAsync();

        var historial = await HistorialAsync(context, "Inspecciones", 10);

        Assert.Equal([crear.IdAuditoria, modificar.IdAuditoria, cerrar.IdAuditoria], historial.Select(a => a.IdAuditoria));
        Assert.All(historial, a => Assert.Equal(("Inspecciones", 10L), (a.Tabla, a.RegistroId)));
    }

    [Fact]
    public async Task ConLaMismaHora_DesempataPorElOrdenDeEscritura()
    {
        await using var context = NuevoContexto();
        var primera = Fila("Equipos", 3, AuditoriaAcciones.Modificar, Base);
        var segunda = Fila("Equipos", 3, AuditoriaAcciones.Desactivar, Base);
        context.Auditoria.AddRange(primera, segunda);
        await context.SaveChangesAsync();

        var historial = await HistorialAsync(context, "Equipos", 3);

        Assert.Equal([primera.IdAuditoria, segunda.IdAuditoria], historial.Select(a => a.IdAuditoria));
    }

    [Fact]
    public async Task UnRegistroSinHistorial_DevuelveLaListaVacia()
    {
        await using var context = NuevoContexto();

        Assert.Empty(await HistorialAsync(context, "Inspecciones", 999));
    }

    [Fact]
    public async Task LosEventosDelSistemaSeConsultanPorUsuario()
    {
        await using var context = NuevoContexto();
        context.Auditoria.AddRange(
            Fila(AuditoriaTablas.Sistema, 4, AuditoriaAcciones.LoginFallido, Base),
            Fila(AuditoriaTablas.Sistema, 4, AuditoriaAcciones.LoginExitoso, Base.AddMinutes(1)),
            Fila(AuditoriaTablas.Sistema, 5, AuditoriaAcciones.LoginExitoso, Base.AddMinutes(2)));
        await context.SaveChangesAsync();

        var historial = await HistorialAsync(context, AuditoriaTablas.Sistema, 4);

        Assert.Equal([AuditoriaAcciones.LoginFallido, AuditoriaAcciones.LoginExitoso], historial.Select(a => a.Accion));
    }

    [Fact]
    public async Task ConUnaTablaQueNoExiste_Devuelve400()
    {
        await using var context = NuevoContexto();

        var action = await new AuditoriaController(context).GetHistorialRegistro("Borrados", 1);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task UnaInspeccionAnulada_MuestraSuHistoriaDesdeLaCreacionHastaLaAnulacion()
    {
        var motivo = new MotivoCambio();
        await using var context = NuevoContexto(new ContextoAuditoriaFalso(motivo));
        var tecnico = new Usuario { Nombre = "Ana", Correo = "ana@ecar.test", PasswordHash = "hash", Activo = true };
        var admin = new Usuario { Nombre = "Admin", Correo = "admin@ecar.test", PasswordHash = "hash", Activo = true };
        var equipo = new Equipo { CodigoInterno = "EQ-001", ActivoFijo = "AF-001", NombreEquipo = "Balanza", Activo = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true };
        context.AddRange(tecnico, admin, equipo, checklist);
        await context.SaveChangesAsync();
        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = tecnico.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var controller = new InspeccionesController(context, new FakeCurrentUser(tecnico.IdUsuario, "Operador"),
            new InspeccionService(context), new FakeStorage(), NullLogger<InspeccionesController>.Instance, motivo);
        await controller.UpdateInspeccion(inspeccion.IdInspeccion, new UpdateInspeccionDto { Observaciones = "Equipo apagado" });
        context.ChangeTracker.Clear();
        motivo.Establecer(null);
        // La anula el Administrador: es el único que puede (22/10/2026).
        var comoAdmin = new InspeccionesController(context, new FakeCurrentUser(admin.IdUsuario, RolesSistema.Administrador),
            new InspeccionService(context), new FakeStorage(), NullLogger<InspeccionesController>.Instance, motivo);
        await comoAdmin.AnularInspeccion(inspeccion.IdInspeccion, new MotivoDto { Motivo = "Se inició sobre el equipo equivocado" });

        var historial = await HistorialAsync(context, "Inspecciones", inspeccion.IdInspeccion);

        Assert.Equal([AuditoriaAcciones.Crear, AuditoriaAcciones.Modificar, AuditoriaAcciones.Anular], historial.Select(a => a.Accion));
        Assert.Contains("\"Estado\":\"EnCurso\"", historial[0].ValorNuevo);
        Assert.Contains("Equipo apagado", historial[1].ValorNuevo);
        Assert.Contains("\"Estado\":\"Anulada\"", historial[2].ValorNuevo);
        Assert.Contains("\"Estado\":\"EnCurso\"", historial[2].ValorAnterior);
        Assert.Equal("Se inició sobre el equipo equivocado", historial[2].Motivo);
    }
}
