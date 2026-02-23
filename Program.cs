using System.Globalization;
using Microsoft.AspNetCore.Localization;
using vkine.Components;
using vkine.Services;
using vkine.Mappers;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("cs")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new[]
    {
        new CookieRequestCultureProvider()
    };
});

var connectionString = builder.Configuration.GetConnectionString("MongoDb")
    ?? builder.Configuration["ConnectionStrings:MongoDb"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Helpful message when configuration is missing; in development prefer user-secrets or env vars
    throw new InvalidOperationException("MongoDB connection string not configured. Set 'ConnectionStrings:MongoDb' in user-secrets or an environment variable.");
}

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddScoped(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("movies"));

builder.Services.AddMemoryCache(options => options.SizeLimit = 2048);
builder.Services.AddSingleton<ICountryLookupService, CountryLookupService>();
builder.Services.AddSingleton<MovieMapper>();
builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();

// used by the TMDB image proxy
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRequestLocalization();
app.UseHttpsRedirection();

// TMDB image proxy - avoids CORS for canvas sampling and allows client-side color detection
app.MapGet("/proxy/tmdb/{**path}", async (string path, IHttpClientFactory httpFactory, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(path))
    {
        ctx.Response.StatusCode = 404;
        return;
    }

    // construct upstream TMDB URL (restrict proxy to TMDB only)
    var upstream = $"https://image.tmdb.org/{path}";

    try
    {
        var client = httpFactory.CreateClient();
        using var upstreamResp = await client.GetAsync(upstream, HttpCompletionOption.ResponseHeadersRead);
        if (!upstreamResp.IsSuccessStatusCode)
        {
            ctx.Response.StatusCode = (int)upstreamResp.StatusCode;
            return;
        }

        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = upstreamResp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        // allow same-origin canvas access from the browser
        ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
        // caching for performance (30 days)
        ctx.Response.Headers["Cache-Control"] = "public, max-age=2592000";

        await upstreamResp.Content.CopyToAsync(ctx.Response.Body);
    }
    catch
    {
        ctx.Response.StatusCode = 502;
    }
});

app.MapStaticAssets();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
