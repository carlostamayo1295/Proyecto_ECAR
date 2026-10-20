using System.Reflection;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Lo que se ajustó al integrar las ramas de BE-1 y BE-3 en integracion-fase4, frente al contrato
/// de PLAN_FASE4_TAREAS §3.4 y §3.7.
/// </summary>
public class IntegracionFase4Tests
{
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private sealed class Usuario1(long id, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => id;
        public string Nombre => "Usuario de pruebas";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role);
    }

    private sealed class Contexto : IContextoAuditoria
    {
        public long? IdUsuario => 1;
        public string Usuario => "Pruebas";
        public string? DireccionIp => null;
        public string? AgenteUsuario => null;
        public string? Motivo => null;
    }

    private static ECARDbContext Base() => new(
        new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Contexto());

    private static async Task<(Usuario Tecnico, Inspeccion Inspeccion)> InspeccionFirmadaAsync(ECARDbContext db)
    {
        var tecnico = new Usuario { Nombre = "Ana", Correo = "ana@ecar.test", PasswordHash = FirmaPruebas.PasswordHash, Activo = true };
        tecnico.UsuarioRoles.Add(new UsuarioRol { Rol = new Rol { Nombre = "Técnico" } });
        var equipo = new Equipo { CodigoInterno = "E-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza" };
        var checklist = new Checklist { Nombre = "Diario", Version = "1" };
        db.AddRange(tecnico, equipo, checklist);
        await db.SaveChangesAsync();
        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow, Estado = InspeccionEstados.Cerrada, FirmaDigital = Png
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();
        return (tecnico, inspeccion);
    }

    [Fact]
    public async Task Editar_un_usuario_no_fija_contrasenas_ni_cambia_el_correo_de_quien_firmo()
    {
        await using var db = Base();
        var (tecnico, _) = await InspeccionFirmadaAsync(db);
        var controlador = Fabrica.Usuarios(db);

        var conPassword = await controlador.UpdateUsuario(tecnico.IdUsuario, new UpdateUsuarioDto { Password = "Otra-clave-2026" });
        var otroCorreo = await controlador.UpdateUsuario(tecnico.IdUsuario, new UpdateUsuarioDto { Correo = "otra@ecar.test" });
        var mismoCorreo = await controlador.UpdateUsuario(tecnico.IdUsuario, new UpdateUsuarioDto { Correo = "ana@ecar.test", Nombre = "Ana T." });

        Assert.IsType<BadRequestObjectResult>(conPassword.Result);
        Assert.IsType<BadRequestObjectResult>(otroCorreo.Result);
        Assert.IsType<OkObjectResult>(mismoCorreo.Result);
        Assert.Equal(FirmaPruebas.PasswordHash, (await db.Usuarios.AsNoTracking().SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task Restablecer_no_da_contrasena_local_a_una_cuenta_solo_de_Active_Directory()
    {
        await using var db = Base();
        var usuarioAd = new Usuario { Nombre = "Luis", Correo = "luis@ecar.test", UsuarioAD = "ECAR\\luis", PasswordHash = string.Empty, Activo = true };
        db.Usuarios.Add(usuarioAd);
        await db.SaveChangesAsync();

        var resultado = await Fabrica.Usuarios(db).RestablecerPassword(usuarioAd.IdUsuario,
            new MotivoDto { Motivo = "Olvidó la contraseña del directorio" }, new MotivoCambio());

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.Equal(string.Empty, (await db.Usuarios.AsNoTracking().SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task Cerrar_sesion_queda_en_la_auditoria()
    {
        await using var db = Base();

        var resultado = await new AuthController(FirmaPruebas.CrearAuthService(db))
            .Logout(db, new Usuario1(7, "Técnico"), porInactividad: true);

        Assert.IsType<OkObjectResult>(resultado.Result);
        var evento = await db.Auditoria.SingleAsync();
        Assert.Equal(AuditoriaAcciones.CierreSesion, evento.Accion);
        Assert.Contains("PorInactividad", evento.ValorNuevo);
    }

    [Fact]
    public void Verificar_la_integridad_es_solo_de_Administrador_y_Auditor()
    {
        var metodo = typeof(InspeccionesController).GetMethod(nameof(InspeccionesController.VerificarIntegridad))!;

        Assert.Equal("Administrador,Auditor", metodo.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
    }

    [Fact]
    public async Task La_huella_no_incluye_las_evidencias_retiradas()
    {
        await using var db = Base();
        var (tecnico, inspeccion) = await InspeccionFirmadaAsync(db);
        inspeccion.FechaCierre = DateTime.UtcNow;
        var verificador = FirmaPruebas.CrearVerificador(new FakeFoto());
        var sinFotos = await verificador.CalcularSelloAsync(inspeccion);

        inspeccion.Evidencias.Add(new Evidencia
        {
            IdEvidencia = 99, Archivo = "f.png", NombreOriginal = "f.png", TipoContenido = "image/png",
            IdUsuarioCarga = tecnico.IdUsuario, Retirada = true, MotivoRetiro = "Foto equivocada del equipo"
        });

        Assert.Equal(sinFotos, await verificador.CalcularSelloAsync(inspeccion));
        inspeccion.Evidencias.Single().Retirada = false;
        Assert.NotEqual(sinFotos, await verificador.CalcularSelloAsync(inspeccion));
    }

    [Fact]
    public async Task El_responsable_de_un_hallazgo_debe_ser_Tecnico_o_Administrador()
    {
        await using var db = Base();
        var (tecnico, inspeccion) = await InspeccionFirmadaAsync(db);
        var auditor = new Usuario { Nombre = "Diana", Correo = "diana@ecar.test", PasswordHash = "x", Activo = true };
        auditor.UsuarioRoles.Add(new UsuarioRol { Rol = new Rol { Nombre = "Auditor" } });
        db.Usuarios.Add(auditor);
        await db.SaveChangesAsync();
        var controlador = new HallazgosController(db, new Usuario1(tecnico.IdUsuario, "Técnico"), new MotivoCambio());

        var aAuditor = await controlador.CreateHallazgo(new CreateHallazgoDto
        {
            IdInspeccion = inspeccion.IdInspeccion, Descripcion = "Etiqueta de calibración vencida", IdUsuarioResponsable = auditor.IdUsuario
        });
        var aTecnico = await controlador.CreateHallazgo(new CreateHallazgoDto
        {
            IdInspeccion = inspeccion.IdInspeccion, Descripcion = "Etiqueta de calibración vencida", IdUsuarioResponsable = tecnico.IdUsuario
        });

        Assert.IsType<BadRequestObjectResult>(aAuditor.Result);
        Assert.IsType<CreatedAtActionResult>(aTecnico.Result);
    }

    private sealed class FakeFoto : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => Task.FromResult("f.png");
        public Task<Stream> AbrirAsync(string rutaRelativa) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }
}
