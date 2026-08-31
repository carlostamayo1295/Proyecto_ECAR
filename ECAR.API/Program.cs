using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
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

// Configure Entity Framework
builder.Services.AddDbContext<ECARDbContext>(options =>
    options.UseSqlServer(conn));

// Configure JWT Authentication
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
});

builder.Services.AddAuthorization();

// Register Services
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

// Configure CORS
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

// Configure Scalar/OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseAuthorization();

// Asegurar que la base de datos existe y aplicar migraciones
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ECARDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await dbContext.Database.MigrateAsync();
    await DataSeeder.SeedDataAsync(dbContext, configuration);
}

app.MapControllers();

app.Run();

// Required by integration tests that host the API in memory.
public partial class Program;
