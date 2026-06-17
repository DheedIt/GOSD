using Microsoft.EntityFrameworkCore;
using WeatherMonitor.Components;
using WeatherMonitor.Data;
using WeatherMonitor.Data.Models;
using WeatherMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Database (factory — thread-safe, works in Singleton BackgroundService) ───
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContextFactory<AppDbContext>(opt =>
    opt.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

// ─── HTTP clients ──────────────────────────────────────────────────────────────
// HttpClient.Timeout is set above ApiTimeoutSeconds so the per-city CancellationTokenSource
// in WeatherCollectorWorker always fires first — giving us a clean OperationCanceledException
// instead of HttpClient's internal timeout wrapping.
var apiTimeoutSec = builder.Configuration.GetValue<int>("ApiTimeoutSeconds", 60);
builder.Services.AddHttpClient<OpenMeteoClient>(c =>
    c.Timeout = TimeSpan.FromSeconds(apiTimeoutSec + 30));

// ─── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<WeatherDataService>();

// Singleton so Blazor pages can inject it for on-demand collection
builder.Services.AddSingleton<WeatherCollectorWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WeatherCollectorWorker>());

// ─── Blazor ────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ─── Ensure DB schema + seed ──────────────────────────────────────────────────
await InitialiseDatabaseAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// ─── DB setup helper ───────────────────────────────────────────────────────────
static async Task InitialiseDatabaseAsync(WebApplication app)
{
    var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();

    await db.Database.EnsureCreatedAsync();

    if (!await db.Cities.AnyAsync())
    {
        db.Cities.AddRange(
            new City { Name = "Воткинск",         Latitude = 57.053200m, Longitude = 53.985500m },
            new City { Name = "Ижевск",           Latitude = 56.853100m, Longitude = 53.211200m },
            new City { Name = "Пермь",            Latitude = 58.010500m, Longitude = 56.250200m },
            new City { Name = "Казань",           Latitude = 55.830400m, Longitude = 49.066100m },
            new City { Name = "Сарапул",          Latitude = 56.481900m, Longitude = 53.803200m },
            new City { Name = "Набережные Челны", Latitude = 55.743500m, Longitude = 52.403200m }
        );
    }

    if (!await db.DataSources.AnyAsync())
    {
        db.DataSources.Add(new DataSource
        {
            Name = "Open-Meteo",
            SourceType = "open_meteo",
            ApiUrl = "https://api.open-meteo.com/v1/forecast",
            Config = """{"air_quality_url":"https://air-quality-api.open-meteo.com/v1/air-quality"}""",
        });
    }

    await db.SaveChangesAsync();
}
