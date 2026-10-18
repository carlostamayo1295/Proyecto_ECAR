using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 1 de hallazgos: el modelo no admite borrado físico (ni directo ni en cascada desde la
/// inspección) y una misma pregunta no puede generar dos novedades en la misma inspección.
/// </summary>
public class HallazgosModeloTests
{
    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Técnico";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeStorage : IEvidenciaStorage
    {
        public List<string> Eliminados { get; } = new();

        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) =>
            Task.FromResult($"2026/10/{idInspeccion}/evidencia{extension}");

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]));

        public Task EliminarAsync(string rutaRelativa)
        {
            Eliminados.Add(rutaRelativa);
            return Task.CompletedTask;
        }
    }

    private sealed record Escenario(ECARDbContext Context, Usuario Tecnico, Inspeccion Inspeccion, Hallazgo Hallazgo);

    private static async Task<Escenario> SeedAsync()
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tecnico = new Usuario { Nombre = "Técnico", Correo = "tecnico@ecar.test", PasswordHash = "hash", Activo = true };
        var equipo = new Equipo { CodigoInterno = "EQ-001", ActivoFijo = "AF-001", NombreEquipo = "Balanza", Activo = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true };
        context.AddRange(tecnico, equipo, checklist);
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

        context.Evidencias.Add(new Evidencia
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Archivo = "2026/10/1/a.jpg",
            NombreOriginal = "foto.jpg",
            TipoContenido = "image/jpeg",
            TamanoBytes = 4,
            FechaCarga = DateTime.UtcNow,
            IdUsuarioCarga = tecnico.IdUsuario
        });
        var hallazgo = new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Descripcion = "Tornillo flojo",
            Origen = HallazgoOrigenes.Manual,
            IdUsuarioRegistro = tecnico.IdUsuario
        };
        context.Hallazgos.Add(hallazgo);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Escenario(context, tecnico, inspeccion, hallazgo);
    }

    [Fact]
    public void Ninguna_ruta_borra_inspecciones_hallazgos_ni_evidencias()
    {
        // §3.6: no hay DELETE; como existen GET y PUT sobre {id}, ASP.NET responde 405 a un DELETE.
        foreach (var controlador in new[] { typeof(InspeccionesController), typeof(HallazgosController), typeof(EvidenciasController) })
        {
            var conDelete = controlador.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute), inherit: true).Length > 0)
                .Select(m => m.Name);
            Assert.Empty(conDelete);
        }
    }

    [Fact]
    public async Task Modelo_NingunaRelacionDeHallazgoBorraEnCascada()
    {
        var escenario = await SeedAsync();
        var tipo = escenario.Context.Model.FindEntityType(typeof(Hallazgo))!;

        var relaciones = tipo.GetForeignKeys().ToList();

        Assert.Contains(relaciones, fk => fk.PrincipalEntityType.ClrType == typeof(Inspeccion));
        Assert.Contains(relaciones, fk => fk.PrincipalEntityType.ClrType == typeof(RespuestaInspeccion));
        Assert.All(relaciones, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public async Task Modelo_UnaNovedadPorPreguntaEInspeccion()
    {
        var escenario = await SeedAsync();
        var tipo = escenario.Context.Model.FindEntityType(typeof(Hallazgo))!;

        var indice = tipo.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(Hallazgo.IdInspeccion), nameof(Hallazgo.IdPregunta)]));

        Assert.True(indice.IsUnique);
        Assert.Equal("[IdPregunta] IS NOT NULL", indice.GetFilter());
    }
}
