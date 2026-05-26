using Microsoft.EntityFrameworkCore;
using Warhammer.Components;
using Warhammer.Data;
using Warhammer.Services;
using Warhammer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["BaseAddress"] ?? "https://localhost:7000/")
});

builder.Services.AddSingleton<RosterLoadService>();
builder.Services.AddSingleton<CrusadeTraitsService>();
builder.Services.AddScoped<ObjectiveService>();
builder.Services.AddScoped<FactionStateService>();
builder.Services.AddScoped<GameStateService>();
builder.Services.AddScoped<StratagemService>();
builder.Services.AddScoped<WoundCalcStateService>();
builder.Services.AddScoped<CrusadeManagerService>();
builder.Services.AddScoped<CampaignManagerService>();
builder.Services.AddScoped<GmNoteSeeder>();
builder.Services.AddScoped<NodeSeeder>();

var app = builder.Build();

// Ensure the SQLite schema is up to date, then seed default data on first run.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var ctx = await factory.CreateDbContextAsync();
    await ctx.Database.EnsureCreatedAsync();

    var gmSeeder   = scope.ServiceProvider.GetRequiredService<GmNoteSeeder>();
    var nodeSeeder = scope.ServiceProvider.GetRequiredService<NodeSeeder>();
    await gmSeeder.SeedAsync();
    await nodeSeeder.SeedAsync();
}

// Dev-only seed mode: `dotnet run --project T'au -- seed <name>` runs a seeder
// against the configured DB and exits without starting the web host.
if (args is ["seed", .. var seedArgs])
{
    var exitCode = await SeedRunner.RunAsync(app.Services, seedArgs);
    return exitCode;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
return 0;