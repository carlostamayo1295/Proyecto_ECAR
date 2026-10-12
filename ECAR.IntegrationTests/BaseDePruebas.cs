using ECAR.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// Una base de SQL Server temporal (ECARDB_pruebas_xxxxxxxx) para lo que el proveedor en memoria
/// no tiene: transacciones, el trigger y los bloqueos (PLAN_FASE4_TAREAS, tarea 6 de BE-0).
/// Se crea vacía y se borra al terminar; nunca se toca la base ECARDB.
/// </summary>
/// <remarks>
/// El servidor se toma de la variable de entorno ECAR_PRUEBAS_SQL o, si no existe, de la cadena
/// ECARConnection de los user-secrets del API (la que cada uno ya tiene configurada), cambiando
/// solo el nombre de la base. Así funciona igual con SQL Express local y con el SQL de Docker.
/// </remarks>
public sealed class BaseDePruebas : IAsyncDisposable
{
    private const string UserSecretsIdApi = "b724bb04-92d3-41ed-8e2d-3a2ec8e5c071";

    public string CadenaConexion { get; }
    private string Nombre { get; }

    private BaseDePruebas(string cadenaConexion, string nombre)
    {
        CadenaConexion = cadenaConexion;
        Nombre = nombre;
    }

    /// <summary>Crea la base y, salvo que se pida otra, aplica todas las migraciones.</summary>
    public static async Task<BaseDePruebas> CrearAsync(string? hastaMigracion = null)
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsIdApi)
            .AddEnvironmentVariables()
            .Build();
        var servidor = configuracion["ECAR_PRUEBAS_SQL"] ?? configuracion.GetConnectionString("ECARConnection");
        if (string.IsNullOrWhiteSpace(servidor))
        {
            throw new InvalidOperationException(
                "Configure ECAR_PRUEBAS_SQL o ConnectionStrings:ECARConnection en los user-secrets del API");
        }

        var nombre = $"ECARDB_pruebas_{Guid.NewGuid():N}"[..23];
        var builder = new SqlConnectionStringBuilder(servidor) { InitialCatalog = nombre };
        var baseDePruebas = new BaseDePruebas(builder.ConnectionString, nombre);

        await using var db = baseDePruebas.CrearContexto();
        await db.GetService<IMigrator>().MigrateAsync(hastaMigracion);
        return baseDePruebas;
    }

    public ECARDbContext CrearContexto(IContextoAuditoria? contexto = null) =>
        new(new DbContextOptionsBuilder<ECARDbContext>().UseSqlServer(CadenaConexion).Options, contexto);

    public async Task EjecutarAsync(string sql)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<T> EscalarAsync<T>(string sql)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(CadenaConexion) { InitialCatalog = "master" };
        await using var conexion = new SqlConnection(master.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(
            $"IF DB_ID(N'{Nombre}') IS NOT NULL BEGIN ALTER DATABASE [{Nombre}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Nombre}]; END",
            conexion);
        await comando.ExecuteNonQueryAsync();
    }
}

/// <summary>Una base compartida por las pruebas de una clase que no alteran la auditoría.</summary>
public sealed class BaseCompartida : IAsyncLifetime
{
    public BaseDePruebas Base { get; private set; } = null!;

    public async Task InitializeAsync() => Base = await BaseDePruebas.CrearAsync();

    public async Task DisposeAsync() => await Base.DisposeAsync();
}

public sealed class ContextoDePrueba : IContextoAuditoria
{
    public long? IdUsuario { get; init; }
    public string Usuario { get; init; } = "Pruebas de integración";
    public string? DireccionIp { get; init; } = "127.0.0.1";
    public string? AgenteUsuario { get; init; } = "ECAR.IntegrationTests";
    public string? Motivo { get; init; }
}
