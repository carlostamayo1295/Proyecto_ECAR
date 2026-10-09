using ECAR.API.Exceptions;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

public class InspeccionServiceTests
{
    private ECARDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ECARDbContext(options);
    }

    [Fact]
    public async Task GuardarRespuestasAsync_InspeccionCerrada_LanzaInspeccionCerradaException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new InspeccionService(context);

        var inspeccion = new Inspeccion
        {
            IdInspeccion = 1,
            IdUsuario = 10,
            Estado = "Cerrada",
            Checklist = new Checklist { IdChecklist = 1, Preguntas = new List<PreguntaChecklist>() }
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var dto = new GuardarRespuestasDto
        {
            Respuestas = new List<RespuestaEjecucionDto>()
        };

        // Act & Assert (Punto 6: Mapea a 409 Conflict)
        await Assert.ThrowsAsync<InspeccionCerradaException>(() =>
            service.GuardarRespuestasAsync(1, dto, usuarioId: 10, esAdmin: false));
    }

    [Fact]
    public async Task GuardarRespuestasAsync_PreguntaNoPerteneceAlChecklist_LanzaArgumentException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new InspeccionService(context);

        var inspeccion = new Inspeccion
        {
            IdInspeccion = 2,
            IdUsuario = 10,
            Estado = InspeccionEstados.EnCurso,
            Checklist = new Checklist
            {
                IdChecklist = 1,
                Preguntas = new List<PreguntaChecklist>
                {
                    new PreguntaChecklist { IdPregunta = 100, Pregunta = "¿Está limpio?", TipoRespuesta = TiposRespuesta.SiNo }
                }
            }
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var dto = new GuardarRespuestasDto
        {
            Respuestas = new List<RespuestaEjecucionDto>
            {
                new RespuestaEjecucionDto { IdPregunta = 999, Respuesta = "Si" }
            }
        };

        // Act & Assert (Punto 3: Pregunta no pertenece al checklist)
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GuardarRespuestasAsync(2, dto, usuarioId: 10, esAdmin: false));
    }

    [Fact]
    public async Task GuardarRespuestasAsync_PreguntaSiNo_RespuestaInvalida_LanzaArgumentException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new InspeccionService(context);

        var inspeccion = new Inspeccion
        {
            IdInspeccion = 3,
            IdUsuario = 10,
            Estado = InspeccionEstados.EnCurso,
            Checklist = new Checklist
            {
                IdChecklist = 1,
                Preguntas = new List<PreguntaChecklist>
                {
                    new PreguntaChecklist { IdPregunta = 1, Pregunta = "¿Nivel de aceite correcto?", TipoRespuesta = TiposRespuesta.SiNo }
                }
            }
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var dto = new GuardarRespuestasDto
        {
            Respuestas = new List<RespuestaEjecucionDto>
            {
                new RespuestaEjecucionDto { IdPregunta = 1, Respuesta = "Tal vez" }
            }
        };

        // Act & Assert (Punto 4: Solo "Si" o "No")
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GuardarRespuestasAsync(3, dto, usuarioId: 10, esAdmin: false));
    }

    [Fact]
    public async Task GuardarRespuestasAsync_PreguntaObligatoriaVacia_SeGuardaComoPendiente()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new InspeccionService(context);

        var inspeccion = new Inspeccion
        {
            IdInspeccion = 4,
            IdUsuario = 10,
            Estado = InspeccionEstados.EnCurso,
            Checklist = new Checklist
            {
                IdChecklist = 1,
                Preguntas = new List<PreguntaChecklist>
                {
                    new PreguntaChecklist { IdPregunta = 1, Pregunta = "Observaciones", TipoRespuesta = TiposRespuesta.Texto, Obligatoria = true }
                }
            }
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var dto = new GuardarRespuestasDto
        {
            Respuestas = new List<RespuestaEjecucionDto>
            {
                new RespuestaEjecucionDto { IdPregunta = 1, Respuesta = "   ", Observacion = "Escrita antes de responder" }
            }
        };

        // Act: el guardado es parcial; la obligatoriedad se exige al firmar, no aquí.
        await service.GuardarRespuestasAsync(4, dto, usuarioId: 10, esAdmin: false);

        // Assert: queda pendiente (sin respuesta) y la observación no se pierde.
        var guardada = context.RespuestasInspeccion.Single(r => r.IdInspeccion == 4 && r.IdPregunta == 1);
        Assert.Null(guardada.Respuesta);
        Assert.Equal("Escrita antes de responder", guardada.Observacion);
    }

    [Fact]
    public async Task ObtenerRespuestasPaginadasAsync_TecnicoConsultaOtrosUsuarios_SoloRetornaLasPropias()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new InspeccionService(context);

        var insp1 = new Inspeccion { IdInspeccion = 10, IdUsuario = 1, Estado = InspeccionEstados.EnCurso };
        var insp2 = new Inspeccion { IdInspeccion = 20, IdUsuario = 2, Estado = InspeccionEstados.EnCurso };

        context.Inspecciones.AddRange(insp1, insp2);
        context.RespuestasInspeccion.AddRange(
            new RespuestaInspeccion { IdRespuesta = 101, IdInspeccion = 10, IdPregunta = 1, Respuesta = "Si" },
            new RespuestaInspeccion { IdRespuesta = 102, IdInspeccion = 20, IdPregunta = 1, Respuesta = "No" }
        );
        await context.SaveChangesAsync();

        // Act (Punto 7: Técnico idUsuario=1 no debe ver respuestas de idUsuario=2)
        var result = await service.ObtenerRespuestasPaginadasAsync(
            pageNumber: 1, pageSize: 10, search: null, idInspeccion: null, usuarioId: 1, esAdmin: false, esAuditor: false);

        // Assert
        Assert.Single(result.Data);
        Assert.Equal(101, result.Data.First().IdRespuesta);
    }

    [Fact]
    public async Task ObtenerRespuestasPaginadasAsync_PageSizeSupera100_LimitaA100()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new InspeccionService(context);

        // Act (Punto 9: Limita pageSize a máximo 100)
        var result = await service.ObtenerRespuestasPaginadasAsync(
            pageNumber: 1, pageSize: 500, search: null, idInspeccion: null, usuarioId: 1, esAdmin: true, esAuditor: false);

        // Assert
        Assert.Equal(100, result.PageSize);
    }
}