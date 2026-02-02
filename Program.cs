using vkine.Components;
using vkine.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("MongoDb")
    ?? builder.Configuration["ConnectionStrings:MongoDb"]
    ?? builder.Configuration["MongoDb"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Helpful message when configuration is missing; in development prefer user-secrets or env vars
    throw new InvalidOperationException("MongoDB connection string not configured. Set 'ConnectionStrings:MongoDb' in user-secrets or an environment variable.");
}

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddScoped(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("movies"));

builder.Services.AddScoped<IMovieService, MovieService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapStaticAssets();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
