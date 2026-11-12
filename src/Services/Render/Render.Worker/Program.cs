using Microsoft.EntityFrameworkCore;
using UMLForge.Render.Repositories;
using UMLForge.Render.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRenderServices(builder.Configuration, builder.Environment);
var app = builder.Build();

if (app.Configuration.GetValue<bool>("DiagramEngine:UseFake"))
    app.Logger.LogWarning("Development FAKE DiagramEngine is enabled. This is not real UML validation or gRPC acceptance.");

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<RenderDbContext>().Database.MigrateAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/health", async (RenderDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken) ? Results.Ok(new { status = "Healthy" }) : Results.StatusCode(503));

await app.RunAsync();
