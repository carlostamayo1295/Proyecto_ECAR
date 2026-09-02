using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ECAR.Client;
using ECAR.Client.Services;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configurar el HttpClient del API
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7296") });

// Registrar los servicios
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HttpClientService>();
builder.Services.AddScoped<AuthorizationService>();
// Mock temporal para pantallas de Fase 2/3 aún sin backend (PreguntasChecklist, RespuestasInspeccion).
builder.Services.AddScoped<MockDataService>();

// Configurar MudBlazor con el tema corporativo de ECAR
builder.Services.AddMudServices();

// Configurar el tema corporativo de ECAR
builder.Services.AddSingleton(new MudTheme()
{
    PaletteLight = new PaletteLight()
    {
        Primary = "#397FDE",
        Secondary = "#79B75D",
        Info = "#17A2B8",
        Success = "#28A745",
        Warning = "#FFC107",
        Error = "#DC3545",
        Background = "#F4F5F7",
        Surface = "#FFFFFF",
        TextPrimary = "#16438C",
        TextSecondary = "#5A6A7C"
    },
    PaletteDark = new PaletteDark()
    {
        Primary = "#6BA0E6",
        Secondary = "#96C982",
        Info = "#4ECDC4",
        Success = "#6FDCE2",
        Warning = "#FFD54F",
        Error = "#FF6B6B",
        Background = "#1A1A2E",
        Surface = "#252542",
        TextPrimary = "#FFFFFF",
        TextSecondary = "#B0B0B0"
    }
});

await builder.Build().RunAsync();
