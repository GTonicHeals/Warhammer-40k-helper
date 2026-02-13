using Warhammer.Components;

var builder = WebApplication.CreateBuilder(args);

// 1. REMOVED: .AddInteractiveWebAssemblyComponents()
// We only need Server components now.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register HttpClient (useful if you want to load the JSON file via HTTP later)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["BaseAddress"] ?? "https://localhost:7000/")
});
// Add this line to register your new service
builder.Services.AddScoped<ObjectiveService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// 2. REMOVED: app.UseWebAssemblyDebugging(); 
// No WebAssembly means no need for this debugger.

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// 3. REMOVED: .AddInteractiveWebAssemblyRenderMode() and .AddAdditionalAssemblies(...)
// This was the line causing your crash. We removed the reference to the Client project.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();