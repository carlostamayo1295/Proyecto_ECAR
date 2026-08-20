using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ECAR.Client;
using ECAR.Client.Services;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7296") });

// Register services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HttpClientService>();
builder.Services.AddScoped<AuthorizationService>();

// Configure MudBlazor with ECAR corporate theme
builder.Services.AddMudServices();

// Configure ECAR corporate theme
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
