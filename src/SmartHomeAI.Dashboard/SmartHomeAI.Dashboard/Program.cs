using SmartHomeAI.Dashboard;
using SmartHomeAI.Dashboard.Components;
using SmartHomeAI.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpClient<HubClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Hub:BaseUrl"] ?? "http://localhost:5002");
    c.Timeout = TimeSpan.FromSeconds(3);
    if (builder.Configuration["Dashboard:ApiKey"] is { Length: > 0 } key) c.DefaultRequestHeaders.Add("X-Api-Key", key);
});
builder.Services.AddSingleton<HubLauncher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HubLauncher>());
builder.Services.AddSingleton<AiRunner>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AiRunner>());
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// LAN HTTP-only; no HTTPS redirect

app.UseAntiforgery();

// Trang camera/cảnh báo cũ đã gộp vào trang chủ.
app.MapGet("/camera", () => Results.Redirect("/"));
app.MapGet("/alerts", () => Results.Redirect("/"));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SmartHomeAI.Dashboard.Client._Imports).Assembly);

app.Run();
