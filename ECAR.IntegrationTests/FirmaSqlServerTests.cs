using ECAR.API.Configuration;
using ECAR.API.Services;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// La huella de la firma se recalcula sobre una inspección leída de SQL Server, que devuelve las
/// fechas sin zona: con el proveedor en memoria este caso no se ve (Parte 11 §11.70).
/// </summary>
public class FirmaSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static readonly VerificadorIntegridad Firma = new(
        Options.Create(new FirmaOptions { ClaveHmac = "clave-hmac-de-las-pruebas-contra-sql-server-32" }), new SinArchivos());

    /// <summary>Estas inspecciones no tienen fotos: el verificador no llega a abrir ningún archivo.</summary>
    private sealed class SinArchivos : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => throw new NotSupportedException();
        public Task<Stream> AbrirAsync(string rutaRelativa) => throw new FileNotFoundException(rutaRelativa);
        public Task EliminarAsync(string rutaRelativa) => throw new NotSupportedException();
    }

    private async Task<long> InspeccionCerradaAsync(Func<Inspeccion, DateTime, Task<string>> sellar, string? algoritmo)
    {
        await using var db = compartida.Base.CrearContexto(new ContextoDePrueba());
        var codigo = Guid.NewGuid().ToString("N")[..8];
        var usuario = new Usuario { Nombre = "Firmante", Correo = codigo + "@pruebas.ecar", PasswordHash = "x" };
        var equipo = new Equipo { CodigoInterno = "F-" + codigo, ActivoFijo = "AF-" + codigo, NombreEquipo = "Equipo" };
        var pregunta = new PreguntaChecklist { Pregunta = "¿Bien?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 1 };
        var checklist = new Checklist { Nombre = "C-" + codigo, Version = "1", Preguntas = [pregunta] };
        db.AddRange(usuario, equipo, checklist);
        await db.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo, IdChecklist = checklist.IdChecklist, IdUsuario = usuario.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Respuestas = [new RespuestaInspeccion { IdPregunta = pregunta.IdPregunta, Respuesta = "Si" }]
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();

        var cierre = DateTime.UtcNow;
        inspeccion.Estado = InspeccionEstados.Cerrada;
        inspeccion.FechaCierre = cierre;
        inspeccion.FirmaDigital = Png;
        inspeccion.FirmaNombre = usuario.Nombre;
        inspeccion.FirmaSignificado = VerificadorIntegridad.SignificadoEjecucion;
        inspeccion.FirmaAlgoritmo = algoritmo;
        inspeccion.FirmaHash = await sellar(inspeccion, cierre);
        await db.SaveChangesAsync();
        return inspeccion.IdInspeccion;
    }

    private async Task<Inspeccion> LeerAsync(long id)
    {
        await using var db = compartida.Base.CrearContexto();
        return await db.Inspecciones.AsNoTracking()
            .Include(i => i.Respuestas).Include(i => i.Evidencias)
            .SingleAsync(i => i.IdInspeccion == id);
    }

    [Fact]
    public async Task Una_firma_HMAC_leida_de_la_base_se_verifica_integra()
    {
        var id = await InspeccionCerradaAsync((i, _) => Firma.CalcularSelloAsync(i), VerificadorIntegridad.AlgoritmoHmacV2);

        var leida = await LeerAsync(id);

        Assert.Equal(DateTimeKind.Unspecified, leida.FechaCierre!.Value.Kind);
        Assert.True(await Firma.VerificarAsync(leida));
    }

    [Fact]
    public async Task Una_firma_SHA256_de_la_Fase_3_leida_de_la_base_se_verifica_integra()
    {
        var id = await InspeccionCerradaAsync((i, cierre) =>
            Task.FromResult(FirmaInspeccion.CalcularHash(i, i.Respuestas, i.Evidencias, cierre, Png)), algoritmo: null);

        Assert.True(await Firma.VerificarAsync(await LeerAsync(id)));
    }

    [Fact]
    public async Task Una_firma_HMACSHA256_v1_de_antes_de_las_novedades_leida_de_la_base_se_verifica_integra()
    {
        var id = await InspeccionCerradaAsync(async (i, _) =>
        {
            var canonico = await Firma.ConstruirContenidoCanonicoAsync(i, VerificadorIntegridad.AlgoritmoHmac);
            return Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("clave-hmac-de-las-pruebas-contra-sql-server-32"),
                System.Text.Encoding.UTF8.GetBytes(canonico))).ToLowerInvariant();
        }, VerificadorIntegridad.AlgoritmoHmac);

        Assert.True(await Firma.VerificarAsync(await LeerAsync(id)));
    }

    [Fact]
    public async Task Alterar_una_respuesta_en_la_base_rompe_la_firma()
    {
        var id = await InspeccionCerradaAsync((i, _) => Firma.CalcularSelloAsync(i), VerificadorIntegridad.AlgoritmoHmacV2);
        await compartida.Base.EjecutarAsync($"UPDATE RespuestasInspeccion SET Respuesta = N'No' WHERE IdInspeccion = {id}");

        Assert.False(await Firma.VerificarAsync(await LeerAsync(id)));
    }
}
