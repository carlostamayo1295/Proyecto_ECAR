using ECAR.API.Controllers;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECAR.API.Tests;

// Reglas de la Fase 2: versionamiento de checklists, orden de preguntas y código QR de equipos
public class BackendPhaseTwoTests
{
    [Fact]
    public async Task NuevaVersionCopiaPreguntasYDesactivaLaAnterior()
    {
        await using var context = CreateContext();
        var origen = await SeedChecklistAsync(context, "Inspección Preventiva", "1.0", preguntas: 3);
        var controller = new ChecklistsController(context);

        var result = await controller.CreateVersion(origen.IdChecklist, new CreateChecklistVersionDto { Version = "2.0" });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ChecklistDto>>(created.Value);
        Assert.True(response.Success);
        Assert.Equal("2.0", response.Data!.Version);
        Assert.True(response.Data.Activo);
        Assert.Equal(new[] { 1, 2, 3 }, response.Data.Preguntas.Select(p => p.Orden));

        var anterior = await context.Checklists.SingleAsync(c => c.IdChecklist == origen.IdChecklist);
        Assert.False(anterior.Activo);
        Assert.Equal(3, await context.PreguntasChecklist.CountAsync(p => p.IdChecklist == origen.IdChecklist));
    }

    [Fact]
    public async Task NuevaVersionRechazaVersionDuplicada()
    {
        await using var context = CreateContext();
        var origen = await SeedChecklistAsync(context, "Inspección Preventiva", "1.0", preguntas: 1);
        var controller = new ChecklistsController(context);

        var repetida = await controller.CreateVersion(origen.IdChecklist, new CreateChecklistVersionDto { Version = "1.0" });
        Assert.IsType<BadRequestObjectResult>(repetida.Result);

        await controller.CreateVersion(origen.IdChecklist, new CreateChecklistVersionDto { Version = "2.0" });
        var duplicada = await controller.CreateVersion(origen.IdChecklist, new CreateChecklistVersionDto { Version = "2.0" });
        Assert.IsType<ConflictObjectResult>(duplicada.Result);
    }

    [Fact]
    public async Task VersionesDevuelveTodasLasDelMismoNombreConLaActivaPrimero()
    {
        await using var context = CreateContext();
        var origen = await SeedChecklistAsync(context, "Inspección Preventiva", "1.0", preguntas: 2);
        await SeedChecklistAsync(context, "Otro checklist", "1.0", preguntas: 1);
        var controller = new ChecklistsController(context);
        await controller.CreateVersion(origen.IdChecklist, new CreateChecklistVersionDto { Version = "2.0" });

        var result = await controller.GetVersiones(origen.IdChecklist);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var versiones = Assert.IsType<ApiResponse<List<ChecklistVersionDto>>>(ok.Value).Data!;
        Assert.Equal(2, versiones.Count);
        Assert.Equal("2.0", versiones[0].Version);
        Assert.True(versiones[0].Activo);
        Assert.All(versiones, v => Assert.Equal(2, v.TotalPreguntas));
    }

    [Fact]
    public async Task ChecklistConRespuestasNoPermiteReemplazarPreguntas()
    {
        await using var context = CreateContext();
        var checklist = await SeedChecklistAsync(context, "Inspección Preventiva", "1.0", preguntas: 1);
        await SeedRespuestaAsync(context, checklist);
        var controller = new ChecklistsController(context);

        var result = await controller.UpdateChecklist(checklist.IdChecklist, new UpdateChecklistDto
        {
            Preguntas = new List<CreatePreguntaChecklistDto>
            {
                new() { Pregunta = "Nueva", TipoRespuesta = TiposRespuesta.SiNo }
            }
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(1, await context.PreguntasChecklist.CountAsync(p => p.IdChecklist == checklist.IdChecklist));
    }

    [Fact]
    public async Task CrearPreguntaAsignaElSiguienteOrdenDelChecklist()
    {
        await using var context = CreateContext();
        var checklist = await SeedChecklistAsync(context, "Inspección Preventiva", "1.0", preguntas: 2);
        var controller = new PreguntasChecklistController(context);

        var result = await controller.CrearPregunta(new PreguntaChecklist
        {
            IdChecklist = checklist.IdChecklist,
            Pregunta = "¿Tercera?",
            TipoRespuesta = TiposRespuesta.SiNo,
            Orden = 99 // el valor enviado se ignora
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var creada = Assert.IsType<ApiResponse<PreguntaChecklist>>(ok.Value).Data!;
        Assert.Equal(3, creada.Orden);
    }

    [Fact]
    public async Task GenerarQrEsIdempotenteYRegenerarCambiaElToken()
    {
        await using var context = CreateContext();
        var equipo = await SeedEquipoAsync(context);
        var controller = CreateEquiposController(context);

        var primero = Data(await controller.GenerateQr(equipo.IdEquipo));
        var segundo = Data(await controller.GenerateQr(equipo.IdEquipo));
        var regenerado = Data(await controller.RegenerateQr(equipo.IdEquipo));

        Assert.True(primero.EsNuevo);
        Assert.False(segundo.EsNuevo);
        Assert.Equal(primero.Token, segundo.Token);
        Assert.NotEqual(primero.Token, regenerado.Token);
        Assert.Equal(32, regenerado.Token.Length);
        Assert.Equal($"https://app.ecar.test/equipos/qr/{regenerado.Token}", regenerado.UrlConsulta);
    }

    [Fact]
    public async Task ConsultaPublicaPorQrDevuelveFichaYChecklistsActivos()
    {
        await using var context = CreateContext();
        var equipo = await SeedEquipoAsync(context);
        var activo = await SeedChecklistAsync(context, "Inspección Preventiva", "1.0", preguntas: 1);
        var inactivo = await SeedChecklistAsync(context, "Histórico", "1.0", preguntas: 1);
        inactivo.Activo = false;
        await context.SaveChangesAsync();

        var controller = CreateEquiposController(context);
        var token = Data(await controller.GenerateQr(equipo.IdEquipo)).Token;

        var result = await controller.GetByQr(token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var consulta = Assert.IsType<ApiResponse<ConsultaQrDto>>(ok.Value).Data!;
        Assert.Equal(equipo.CodigoInterno, consulta.Equipo.CodigoInterno);
        Assert.Null(consulta.Equipo.QRCode); // el token no se expone en la respuesta pública
        Assert.Single(consulta.ChecklistsActivos);
        Assert.Equal(activo.IdChecklist, consulta.ChecklistsActivos[0].IdChecklist);
    }

    [Fact]
    public async Task ConsultaPublicaPorQrNoEncuentraTokenDesconocidoNiEquipoInactivo()
    {
        await using var context = CreateContext();
        var equipo = await SeedEquipoAsync(context);
        var controller = CreateEquiposController(context);
        var token = Data(await controller.GenerateQr(equipo.IdEquipo)).Token;

        Assert.IsType<NotFoundObjectResult>((await controller.GetByQr("token-inexistente")).Result);

        equipo.Activo = false;
        await context.SaveChangesAsync();
        Assert.IsType<NotFoundObjectResult>((await controller.GetByQr(token)).Result);
    }

    [Fact]
    public async Task ImagenQrSeGeneraEnElServidorComoPng()
    {
        await using var context = CreateContext();
        var equipo = await SeedEquipoAsync(context);
        var controller = CreateEquiposController(context);

        Assert.IsType<NotFoundObjectResult>(await controller.GetQrImage(equipo.IdEquipo));

        await controller.GenerateQr(equipo.IdEquipo);
        var file = Assert.IsType<FileContentResult>(await controller.GetQrImage(equipo.IdEquipo));

        Assert.Equal("image/png", file.ContentType);
        // Firma de un archivo PNG
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, file.FileContents.Take(4));
    }

    // ---- utilidades ----

    private static EquipoQrDto Data(ActionResult<ApiResponse<EquipoQrDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApiResponse<EquipoQrDto>>(ok.Value).Data!;
    }

    private static EquiposController CreateEquiposController(ECARDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cliente:BaseUrl"] = "https://app.ecar.test/" })
            .Build();
        return new EquiposController(context, configuration);
    }

    private static async Task<Checklist> SeedChecklistAsync(ECARDbContext context, string nombre, string version, int preguntas)
    {
        var checklist = new Checklist { Nombre = nombre, Version = version, Activo = true };
        context.Checklists.Add(checklist);
        await context.SaveChangesAsync();

        for (var i = 1; i <= preguntas; i++)
        {
            context.PreguntasChecklist.Add(new PreguntaChecklist
            {
                IdChecklist = checklist.IdChecklist,
                Pregunta = $"Pregunta {i}",
                TipoRespuesta = TiposRespuesta.SiNo,
                Obligatoria = true,
                Orden = i
            });
        }
        await context.SaveChangesAsync();
        return checklist;
    }

    private static async Task SeedRespuestaAsync(ECARDbContext context, Checklist checklist)
    {
        var equipo = await SeedEquipoAsync(context);
        var usuario = new Usuario { Nombre = "Técnico", Correo = "tecnico@ecar.com", PasswordHash = "hash", Activo = true };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var inspeccion = new Inspeccion { IdEquipo = equipo.IdEquipo, IdUsuario = usuario.IdUsuario, FechaInspeccion = DateTime.UtcNow };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var pregunta = await context.PreguntasChecklist.FirstAsync(p => p.IdChecklist == checklist.IdChecklist);
        context.RespuestasInspeccion.Add(new RespuestaInspeccion
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdPregunta = pregunta.IdPregunta,
            Respuesta = "Si"
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Equipo> SeedEquipoAsync(ECARDbContext context)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..6];
        var equipo = new Equipo
        {
            CodigoInterno = $"EQ-{sufijo}",
            ActivoFijo = $"AF-{sufijo}",
            NombreEquipo = "Balanza analítica",
            Criticidad = "Alta",
            Activo = true
        };
        context.Equipos.Add(equipo);
        await context.SaveChangesAsync();
        return equipo;
    }

    private static ECARDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ECARDbContext(options);
    }
}
