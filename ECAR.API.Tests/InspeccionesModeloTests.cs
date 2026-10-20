using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 1 de la anulación de inspecciones: Anulada es un estado válido y ninguna relación borra
/// en cascada una inspección ni sus datos (respuestas, evidencias, preguntas, novedades, auditoría).
/// </summary>
public class InspeccionesModeloTests
{
    private static ECARDbContext CreateContext() => new(new DbContextOptionsBuilder<ECARDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Theory]
    [InlineData(InspeccionEstados.EnCurso)]
    [InlineData(InspeccionEstados.NovedadesPendientes)]
    [InlineData(InspeccionEstados.Cerrada)]
    [InlineData(InspeccionEstados.Anulada)]
    public void EsValido_IncluyeLosCuatroEstados(string estado)
    {
        Assert.True(InspeccionEstados.EsValido(estado));
    }

    [Fact]
    public void EsValido_RechazaUnEstadoDesconocido()
    {
        Assert.False(InspeccionEstados.EsValido("Borrada"));
    }

    [Theory]
    [InlineData(typeof(Inspeccion))]
    [InlineData(typeof(RespuestaInspeccion))]
    [InlineData(typeof(Evidencia))]
    [InlineData(typeof(PreguntaChecklist))]
    [InlineData(typeof(Novedad))]
    [InlineData(typeof(Auditoria))]
    public void Modelo_NingunaRelacionBorraEnCascada(Type entidad)
    {
        using var context = CreateContext();
        var tipo = context.Model.FindEntityType(entidad)!;

        var enCascada = tipo.GetForeignKeys()
            .Where(fk => fk.DeleteBehavior != DeleteBehavior.Restrict)
            .Select(fk => $"{entidad.Name} → {fk.PrincipalEntityType.ClrType.Name} ({fk.DeleteBehavior})")
            .ToList();

        Assert.Empty(enCascada);
    }
}
