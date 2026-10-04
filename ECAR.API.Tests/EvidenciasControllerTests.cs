using System.Security.Claims;
using ECAR.API.Configuration;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

public class EvidenciasControllerTests
{
    private sealed class FakeCurrentUser(long idUsuario, string nombre, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => nombre;
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeStorage : IEvidenciaStorage
    {
        public byte[]? Guardado { get; private set; }
        public string? Eliminado { get; private set; }
        public string Ruta { get; set; } = "2026/10/1/evidencia.jpg";

        public async Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Guardado = buffer.ToArray();
            Ruta = $"2026/10/{idInspeccion}/evidencia{extension}";
            return Ruta;
        }

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream(Guardado ?? [0xFF, 0xD8, 0xFF, 0x00]));

        public Task EliminarAsync(string rutaRelativa)
        {
            Eliminado = rutaRelativa;
            return Task.CompletedTask;
        }
    }

    private sealed record Escenario(
        ECARDbContext Context,
        Usuario Tecnico,
        Usuario OtroTecnico,
        Inspeccion Inspeccion);

    private static ECARDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Escenario> SeedAsync(ECARDbContext context, string estado = InspeccionEstados.EnCurso)
    {
        var tecnico = new Usuario
        {
            Nombre = "Técnico Uno",
            Correo = "tecnico1@ecar.test",
            PasswordHash = "hash"
        };
        var otroTecnico = new Usuario
        {
            Nombre = "Técnico Dos",
            Correo = "tecnico2@ecar.test",
            PasswordHash = "hash"
        };
        var equipo = new Equipo
        {
            CodigoInterno = "EQ-001",
            ActivoFijo = "AF-001",
            NombreEquipo = "Balanza"
        };
        var checklist = new Checklist { Nombre = "General", Version = "1.0" };
        context.AddRange(tecnico, otroTecnico, equipo, checklist);
        await context.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = tecnico.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = estado
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new(context, tecnico, otroTecnico, inspeccion);
    }

    private static EvidenciasController CreateController(
        ECARDbContext context,
        Usuario usuario,
        FakeStorage storage,
        string role = "Técnico",
        int tamanoMaximoMb = 5)
    {
        var currentUser = new FakeCurrentUser(usuario.IdUsuario, usuario.Nombre, role);
        var controller = new EvidenciasController(
            context,
            currentUser,
            storage,
            Options.Create(new EvidenciasOptions
            {
                RutaBase = "EvidenciasPruebas",
                TamanoMaximoMB = tamanoMaximoMb,
                TiposPermitidos = "image/jpeg,image/png"
            }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, role)], "Pruebas"))
            }
        };
        return controller;
    }

    private static FormFile CreateFile(byte[] contenido, string nombre = "foto.jpg", string tipo = "image/jpeg") =>
        new(new MemoryStream(contenido), 0, contenido.Length, "archivo", nombre)
        {
            Headers = new HeaderDictionary(),
            ContentType = tipo
        };

    [Fact]
    public async Task Crear_ImagenJpegValida_GuardaArchivoYMetadatos()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var storage = new FakeStorage();
        var contenido = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01 };

        var action = await CreateController(context, escenario.Tecnico, storage)
            .CreateEvidencia(escenario.Inspeccion.IdInspeccion, CreateFile(contenido));

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<ApiResponse<EvidenciaDto>>(created.Value);
        Assert.Equal("image/jpeg", response.Data!.TipoContenido);
        Assert.Equal(contenido, storage.Guardado);
        Assert.Single(await context.Evidencias.ToListAsync());
    }

    [Fact]
    public async Task Crear_PdfRenombradoComoJpg_Devuelve400()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var storage = new FakeStorage();

        var action = await CreateController(context, escenario.Tecnico, storage)
            .CreateEvidencia(escenario.Inspeccion.IdInspeccion,
                CreateFile([0x25, 0x50, 0x44, 0x46], "documento.jpg"));

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Null(storage.Guardado);
    }

    [Fact]
    public async Task Crear_EnInspeccionCerrada_Devuelve409()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context, InspeccionEstados.Cerrada);

        var action = await CreateController(context, escenario.Tecnico, new FakeStorage())
            .CreateEvidencia(escenario.Inspeccion.IdInspeccion,
                CreateFile([0x89, 0x50, 0x4E, 0x47], "foto.png", "image/png"));

        Assert.IsType<ConflictObjectResult>(action.Result);
    }

    [Fact]
    public async Task Crear_TecnicoAjeno_Devuelve403()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);

        var action = await CreateController(context, escenario.OtroTecnico, new FakeStorage())
            .CreateEvidencia(escenario.Inspeccion.IdInspeccion,
                CreateFile([0xFF, 0xD8, 0xFF, 0x00]));

        Assert.IsType<ForbidResult>(action.Result);
    }

    [Fact]
    public async Task GetArchivo_Propio_DevuelveImagen()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var storage = new FakeStorage();
        var evidencia = new Evidencia
        {
            IdInspeccion = escenario.Inspeccion.IdInspeccion,
            Archivo = storage.Ruta,
            NombreOriginal = "foto.jpg",
            TipoContenido = "image/jpeg",
            TamanoBytes = 4,
            IdUsuarioCarga = escenario.Tecnico.IdUsuario
        };
        context.Evidencias.Add(evidencia);
        await context.SaveChangesAsync();

        var result = await CreateController(context, escenario.Tecnico, storage)
            .GetArchivo(evidencia.IdEvidencia);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
    }

    [Fact]
    public async Task Eliminar_EnCurso_EliminaFilaYArchivo()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var storage = new FakeStorage();
        var evidencia = new Evidencia
        {
            IdInspeccion = escenario.Inspeccion.IdInspeccion,
            Archivo = storage.Ruta,
            NombreOriginal = "foto.jpg",
            TipoContenido = "image/jpeg",
            TamanoBytes = 4,
            IdUsuarioCarga = escenario.Tecnico.IdUsuario
        };
        context.Evidencias.Add(evidencia);
        await context.SaveChangesAsync();

        var action = await CreateController(context, escenario.Tecnico, storage)
            .DeleteEvidencia(evidencia.IdEvidencia);

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.Empty(await context.Evidencias.ToListAsync());
        Assert.Equal(storage.Ruta, storage.Eliminado);
    }
}
