using Microsoft.EntityFrameworkCore;
using UMLForge.Render.Repositories;
using UMLForge.Render.Service;
using UMLForge.Render.Worker;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;

using UMLForge.BuildingBlocks.Jobs;
using UMLForge.Render.Worker.Jobs;

var builder = WebApplication.CreateBuilder(args);
builder.AddUmlForgeDefaults(Producers.Render);
builder.Services.AddRenderServices(builder.Configuration, builder.Environment);
builder.Services.AddHostedService<RenderJobProcessor>();
builder.Services.AddHealthChecks().AddCheck<RenderDatabaseHealthCheck>("render-sql");
builder.Services.AddControllers();
builder.Services.AddScheduledJob<ReconcileJob>();
builder.Services.AddScheduledJob<CleanupJob>();
builder.Services.AddScheduledJob<DailyRenderReportJob>();
var app = builder.Build();
app.UseUmlForgeDefaults();
app.MapControllers();
app.MapJobTriggerEndpoint("render");

if (app.Configuration.GetValue<bool>("DiagramEngine:UseFake"))
    app.Logger.LogWarning("Development FAKE DiagramEngine is enabled. This is not real UML validation or gRPC acceptance.");

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<RenderDbContext>().Database.MigrateAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
