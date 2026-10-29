using ECAR.API.Configuration;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// Novedades contra SQL Server: lo que el proveedor en memoria no comprueba. La firma y sus
/// novedades se guardan en una sola transacción, el índice único impide duplicar una novedad, la
/// FK impide borrar una inspección que tiene novedades y, si dos personas resuelven a la vez las dos
/// últimas novedades de una inspección, el bloqueo de la inspección hace que quede cerrada.
/// </summary>
public class NovedadesSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private BaseDePruebas Base => compartida.Base;

    private static readonly VerificadorIntegridad Firma = new(
        Options.Create(new FirmaOptions { ClaveHmac = "clave-hmac-de-las-pruebas-contra-sql-server-32" }), new SinArchivos());

    private sealed class SinArchivos : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) => throw new NotSupportedException();
        public Task<Stream> AbrirAsync(string rutaRelativa) => throw new FileNotFoundException(rutaRelativa);
        public Task EliminarAsync(string rutaRelativa) => throw new NotSupportedException();
    }

    /// <summary>La contraseña ya se prueba en ECAR.API.Tests; aquí interesa la base.</summary>
    private sealed class ContrasenaCorrecta : IAuthService
    {
        public Task<LoginResponseDto?> LoginAsync(LoginDto loginDto) => throw new NotSupportedException();
        public Task<bool> ValidateTokenAsync(string token) => throw new NotSupportedException();
        public Task<bool> VerificarPasswordAsync(long idUsuario, string? password, string operacion) => Task.FromResult(true);
        public Task<List<string>> CambiarPasswordAsync(long idUsuario, CambiarPasswordDto cambio) => throw new NotSupportedException();
    }

    private sealed class Usuario1(long id) : ICurrentUser
    {
        public long IdUsuario => id;
        public string Nombre => "Operador";
        public IReadOnlyCollection<string> Roles => ["Operador"];
        public bool IsInRole(string role) => role == "Operador";
    }

    private sealed record Datos(long IdInspeccion, long IdUsuario, List<(long IdPregunta, long IdRespuesta)> Respuestas);

    /// <summary>Una inspección en curso con <paramref name="noes"/> respuestas "No".</summary>
    private async Task<Datos> InspeccionConNoesAsync(int noes = 1)
    {
        var codigo = "IT-" + Guid.NewGuid().ToString("N")[..10];
        await using var db = Base.CrearContexto();

        var usuario = new Usuario { Nombre = "Inspector", Correo = codigo + "@ecar.test", PasswordHash = "hash", Activo = true };
        var equipo = new Equipo { CodigoInterno = codigo, ActivoFijo = "AF-" + codigo, NombreEquipo = "Equipo " + codigo };
        var preguntas = Enumerable.Range(1, noes)
            .Select(n => new PreguntaChecklist { Pregunta = $"¿Punto {n} bien?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = n })
            .ToList();
        var checklist = new Checklist { Nombre = "Checklist " + codigo, Version = "1.0", Activo = true, Preguntas = preguntas };
        db.AddRange(usuario, equipo, checklist);
        await db.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = usuario.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Respuestas = preguntas.Select(p => new RespuestaInspeccion { IdPregunta = p.IdPregunta, Respuesta = "No", Observacion = "Falla", PresentaNovedad = true }).ToList()
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();

        return new Datos(inspeccion.IdInspeccion, usuario.IdUsuario,
            inspeccion.Respuestas.Select(r => (r.IdPregunta, r.IdRespuesta)).ToList());
    }

    private static Novedad Novedad(Datos datos, int indice = 0) => new()
    {
        IdInspeccion = datos.IdInspeccion,
        IdPregunta = datos.Respuestas[indice].IdPregunta,
        IdRespuesta = datos.Respuestas[indice].IdRespuesta,
        Descripcion = "¿Punto bien?: Falla",
        FechaRegistro = DateTime.UtcNow,
        IdUsuarioRegistro = datos.IdUsuario
    };

    [Fact]
    public async Task Si_una_novedad_falla_la_firma_entera_se_revierte()
    {
        var datos = await InspeccionConNoesAsync();

        // Lo mismo que hace la firma (firmar y crear las novedades en un SaveChanges), pero con la
        // novedad repetida: el índice único rechaza la segunda.
        await using (var db = Base.CrearContexto())
        {
            var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            inspeccion.Estado = InspeccionEstados.NovedadesPendientes;
            inspeccion.FechaCierre = DateTime.UtcNow;
            db.Novedades.AddRange(Novedad(datos), Novedad(datos));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var lectura = Base.CrearContexto();
        var guardada = await lectura.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.FechaCierre);
        Assert.False(await lectura.Novedades.AnyAsync(n => n.IdInspeccion == datos.IdInspeccion));
        Assert.False(await lectura.Auditoria.AnyAsync(a => a.Tabla == "Inspecciones"
            && a.RegistroId == datos.IdInspeccion && a.Accion == AuditoriaAcciones.Firmar));
    }

    [Fact]
    public async Task La_base_no_admite_dos_novedades_de_la_misma_respuesta()
    {
        var datos = await InspeccionConNoesAsync();
        await using (var db = Base.CrearContexto())
        {
            db.Novedades.Add(Novedad(datos));
            await db.SaveChangesAsync();
        }

        await using var otra = Base.CrearContexto();
        otra.Novedades.Add(Novedad(datos));
        await Assert.ThrowsAsync<DbUpdateException>(() => otra.SaveChangesAsync());
    }

    [Fact]
    public async Task Una_inspeccion_con_novedades_no_se_puede_borrar()
    {
        var datos = await InspeccionConNoesAsync();
        await using (var db = Base.CrearContexto())
        {
            db.Novedades.Add(Novedad(datos));
            await db.SaveChangesAsync();
        }

        await using (var db = Base.CrearContexto())
        {
            var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            db.Inspecciones.Remove(inspeccion);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var lectura = Base.CrearContexto();
        Assert.True(await lectura.Novedades.AnyAsync(n => n.IdInspeccion == datos.IdInspeccion));
    }

    [Fact]
    public async Task Dos_personas_resuelven_a_la_vez_las_dos_ultimas_y_la_inspeccion_queda_cerrada()
    {
        // Sin el bloqueo de la inspección, cada una vería la otra novedad pendiente y ninguna la cerraría.
        for (var intento = 0; intento < 5; intento++)
        {
            var datos = await InspeccionConNoesAsync(noes: 2);
            List<long> ids;
            await using (var db = Base.CrearContexto())
            {
                var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
                inspeccion.Estado = InspeccionEstados.NovedadesPendientes;
                inspeccion.Resultado = InspeccionResultados.Pendiente;
                inspeccion.FechaCierre = DateTime.UtcNow;
                inspeccion.FirmaHash = new string('a', 64);
                var novedades = new[] { Novedad(datos, 0), Novedad(datos, 1) };
                db.Novedades.AddRange(novedades);
                await db.SaveChangesAsync();
                ids = novedades.Select(n => n.IdNovedad).ToList();
            }

            var resultados = await Task.WhenAll(ids.Select(async id =>
            {
                await using var db = Base.CrearContexto(new ContextoDePrueba { IdUsuario = datos.IdUsuario });
                var controlador = new NovedadesController(db, new Usuario1(datos.IdUsuario), new MotivoCambio());
                return await controlador.ResolverNovedad(id,
                    new ResolverNovedadDto { Solucion = "Se ajustó y se probó de nuevo", FirmaPngBase64 = Png, Password = "x" },
                    new ContrasenaCorrecta(), Firma);
            }));

            Assert.All(resultados, r => Assert.IsType<OkObjectResult>(r.Result));
            await using var lectura = Base.CrearContexto();
            var cerrada = await lectura.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            Assert.Equal(InspeccionEstados.Cerrada, cerrada.Estado);
            Assert.Equal(InspeccionResultados.Conforme, cerrada.Resultado);
        }
    }

    [Fact]
    public async Task El_sello_de_una_resolucion_leida_de_la_base_se_verifica_integro()
    {
        var datos = await InspeccionConNoesAsync();
        long id;
        await using (var db = Base.CrearContexto(new ContextoDePrueba { IdUsuario = datos.IdUsuario }))
        {
            var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            inspeccion.Estado = InspeccionEstados.NovedadesPendientes;
            inspeccion.FirmaHash = new string('b', 64);
            var novedad = Novedad(datos);
            db.Novedades.Add(novedad);
            await db.SaveChangesAsync();
            id = novedad.IdNovedad;

            var resultado = await new NovedadesController(db, new Usuario1(datos.IdUsuario), new MotivoCambio())
                .ResolverNovedad(id, new ResolverNovedadDto { Solucion = "Se cambió la pieza dañada", FirmaPngBase64 = Png, Password = "x" },
                    new ContrasenaCorrecta(), Firma);
            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        await using var lectura = Base.CrearContexto();
        var leida = await lectura.Novedades.AsNoTracking().Include(n => n.Inspeccion).SingleAsync(n => n.IdNovedad == id);
        Assert.Equal(DateTimeKind.Unspecified, leida.FechaResolucion!.Value.Kind);
        Assert.True(Firma.VerificarNovedad(leida));

        await Base.EjecutarAsync($"UPDATE Novedades SET Solucion = N'Otra cosa' WHERE IdNovedad = {id}");
        await using var alterada = Base.CrearContexto();
        Assert.False(Firma.VerificarNovedad(await alterada.Novedades.AsNoTracking().Include(n => n.Inspeccion).SingleAsync(n => n.IdNovedad == id)));
    }
}
