using ECAR.API.Services;
using ECAR.API.Services.Notificaciones;
using ECAR.Infrastructure.Data;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using ECAR.Infrastructure.Entities;

var builder = WebApplication.CreateBuilder(args);

// Registrar los servicios en el contenedor.
builder.Services.AddControllers(options => options.Filters.Add<CambioPasswordPendienteFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Solicitud inválida"
                    : error.ErrorMessage)
                .Distinct()
                .ToList();

            return new BadRequestObjectResult(
                ApiResponse<object>.ErrorResponse("La solicitud contiene datos inválidos", errors));
        };
    });

var conn = builder.Configuration.GetConnectionString("ECARConnection");
if (string.IsNullOrWhiteSpace(conn))
{
    throw new InvalidOperationException("ConnectionStrings:ECARConnection is not configured");
}

// Configurar Entity Framework
builder.Services.AddDbContext<ECARDbContext>(options =>
    options.UseSqlServer(conn));

// Configurar la autenticación JWT
var jwtSecret = builder.Configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException("JWT:Secret must contain at least 32 characters");
}
var jwtIssuer = builder.Configuration["JWT:Issuer"] ?? "ECAR-Auditoria";
var jwtAudience = builder.Configuration["JWT:Audience"] ?? "ECAR-Users";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
    // Un token deja de valer en cuanto se desactiva al usuario o cambia su contraseña (endpoint 27).
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = SelloSeguridad.ValidarTokenAsync
    };
});

builder.Services.AddAuthorization();

// Registrar los servicios propios
builder.Services.AddOptions<EcarAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(EcarAuthenticationOptions.SectionName))
    .Validate(options => Enum.TryParse<EcarAuthenticationMode>(options.Mode, true, out _),
        "ECARAuthentication:Mode debe ser Local, ActiveDirectory o Hybrid")
    .ValidateOnStart();
var configuredAuthenticationMode = builder.Configuration
    .GetSection(EcarAuthenticationOptions.SectionName)
    .GetValue<string>(nameof(EcarAuthenticationOptions.Mode));
var activeDirectoryIsRequired = Enum.TryParse<EcarAuthenticationMode>(
    configuredAuthenticationMode,
    true,
    out var authenticationMode) &&
    authenticationMode is EcarAuthenticationMode.ActiveDirectory or EcarAuthenticationMode.Hybrid;
builder.Services.AddOptions<ActiveDirectoryOptions>()
    .Bind(builder.Configuration.GetSection(ActiveDirectoryOptions.SectionName))
    .Validate(options => !activeDirectoryIsRequired ||
        (options.Enabled && !string.IsNullOrWhiteSpace(options.Server) && options.Port is > 0 and <= 65535),
        "ActiveDirectory debe estar habilitado y tener servidor/puerto válidos cuando el modo sea ActiveDirectory o Hybrid")
    .ValidateOnStart();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IActiveDirectoryAuthService, LdapActiveDirectoryAuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
// Auditoría transaccional (Fase 4): ECARDbContext recibe IContextoAuditoria por su constructor.
builder.Services.AddScoped<IMotivoCambio, MotivoCambio>();
builder.Services.AddScoped<ECAR.Infrastructure.Data.IContextoAuditoria, ContextoAuditoriaHttp>();
builder.Services.AddScoped<IInspeccionService, InspeccionService>();
builder.Services.AddScoped<IReporteService, ReporteService>();

// Fase 4: política de cuentas y firma electrónica (PLAN_FASE4_TAREAS §3.2). El API no arranca sin
// Firma:ClaveHmac (user-secrets en desarrollo; almacén de secretos o variable Firma__ClaveHmac en el servidor).
builder.Services.AddOptions<ECAR.API.Configuration.SeguridadOptions>()
    .Bind(builder.Configuration.GetSection(ECAR.API.Configuration.SeguridadOptions.SectionName))
    .Validate(options => options.LongitudMinimaPassword > 0 && options.MaxIntentosFallidos > 0 && options.MinutosBloqueo > 0,
        "Seguridad: la longitud mínima, el máximo de intentos y los minutos de bloqueo deben ser mayores que cero")
    .ValidateOnStart();
builder.Services.AddOptions<ECAR.API.Configuration.FirmaOptions>()
    .Bind(builder.Configuration.GetSection(ECAR.API.Configuration.FirmaOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClaveHmac) &&
        options.ClaveHmac.Length >= ECAR.API.Configuration.FirmaOptions.LongitudMinimaClave,
        "Firma:ClaveHmac debe configurarse fuera de Git (user-secrets o variable de entorno Firma__ClaveHmac) con al menos 32 caracteres")
    .ValidateOnStart();
builder.Services.AddSingleton<PoliticaPasswords>();
builder.Services.AddScoped<VerificadorIntegridad>();

// Correo de cada novedad a la mesa de ayuda (SRS v1.1, regla 4). Firmar solo deja las novedades en
// la cola; un servicio en segundo plano las envía, así que un correo caído no detiene ni revierte
// la firma. Sin Correo:Usuario y Correo:Password (user-secrets) los mensajes se guardan como .eml
// en Correo:CarpetaSalida y la novedad queda "sin enviar" para reenviarla después.
builder.Services.AddOptions<ECAR.API.Configuration.CorreoOptions>()
    .Bind(builder.Configuration.GetSection(ECAR.API.Configuration.CorreoOptions.SectionName));
builder.Services.AddSingleton<ColaNotificaciones>();
builder.Services.AddSingleton<INotificadorNovedades>(servicios => servicios.GetRequiredService<ColaNotificaciones>());
builder.Services.AddSingleton<IEnviadorCorreo, EnviadorCorreoSmtp>();
builder.Services.AddScoped<NotificacionNovedades>();
builder.Services.AddHostedService<EnvioNotificacionesService>();

// Configurar almacenamiento de evidencias
builder.Services.Configure<ECAR.API.Configuration.EvidenciasOptions>(builder.Configuration.GetSection("Evidencias"));
// Una RutaBase relativa ("EvidenciasStorageLocal") se resuelve contra la carpeta del API y no
// contra el directorio de trabajo del proceso: con Path.GetFullPath a secas, las fotos acababan
// donde se hubiera lanzado "dotnet run", y en IIS dependía de cómo arrancara el proceso.
// En producción conviene una ruta absoluta fuera de la carpeta de la aplicación.
builder.Services.PostConfigure<ECAR.API.Configuration.EvidenciasOptions>(opciones =>
{
    if (!string.IsNullOrWhiteSpace(opciones.RutaBase) && !Path.IsPathRooted(opciones.RutaBase))
    {
        opciones.RutaBase = Path.Combine(builder.Environment.ContentRootPath, opciones.RutaBase);
    }
});
builder.Services.AddScoped<ECAR.API.Services.IEvidenciaStorage, ECAR.API.Services.EvidenciaStorageDisco>();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient",
        policy =>
        {
            policy.WithOrigins(
                "https://localhost:5000", "http://localhost:5000",
                "https://localhost:5204", "http://localhost:5204",
                "https://localhost:7267", "http://localhost:7267")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Configurar Scalar/OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Configurar la canalización de solicitudes HTTP.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Cabeceras de seguridad en todas las respuestas del API. La política de contenido estricta no se
// aplica a la documentación (Scalar, solo en desarrollo), que necesita cargar sus scripts.
app.Use(async (context, next) =>
{
    var cabeceras = context.Response.Headers;
    cabeceras["X-Content-Type-Options"] = "nosniff";
    cabeceras["X-Frame-Options"] = "DENY";
    cabeceras["Referrer-Policy"] = "no-referrer";
    if (!context.Request.Path.StartsWithSegments("/scalar") && !context.Request.Path.StartsWithSegments("/openapi"))
    {
        cabeceras["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    }

    await next();
});

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseAuthorization();

// Aplicar las migraciones pendientes y sembrar los datos faltantes al iniciar.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ECARDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await dbContext.Database.MigrateAsync();
    await DataSeeder.SeedDataAsync(dbContext, configuration);
}

app.MapControllers();

app.Run();

// Requerido por las pruebas de integración que hospedan el API en memoria.
public partial class Program;
