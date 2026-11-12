using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.BuildingBlocks.Jobs;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using UMLForge.Contracts.Grpc;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.EventHandlers;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// BuildingBlocks defaults: Serilog, ProblemDetails, JWT, Swagger, Controllers with JsonStringEnumConverter, HealthChecks
builder.AddUmlForgeDefaults(Producers.Diagram);

// Database
builder.Services.AddDbContext<DiagramDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DiagramDb")));

// Repositories
builder.Services.AddScoped<IDiagramRepository, DiagramRepository>();
builder.Services.AddScoped<IDiagramVersionRepository, DiagramVersionRepository>();
builder.Services.AddScoped<IMemberAccessRepository, MemberAccessRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();

// Services
builder.Services.AddScoped<IDiagramService, DiagramService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IDiagramExportService, DiagramExportService>();
builder.Services.AddScoped<IMemberAccessService, MemberAccessService>();

// DiagramEngine gRPC Client (T-05)
var useFakeEngine = builder.Configuration.GetValue<bool>("DiagramEngine:UseFake");
if (useFakeEngine)
{
    builder.Services.AddScoped<IDiagramEngineClient, FakeDiagramEngineClient>();
}
else
{
    var engineUrl = builder.Configuration["DiagramEngine:Url"]
        ?? builder.Configuration["DIAGRAM_ENGINE_URL"]
        ?? "http://localhost:5006";

    builder.Services.AddGrpcClient<DiagramEngineService.DiagramEngineServiceClient>(o =>
    {
        o.Address = new Uri(engineUrl);
    });
    builder.Services.AddScoped<IDiagramEngineClient, GrpcDiagramEngineClient>();
}

// Transactional Outbox + Publisher (T-03)
builder.Services.AddOutbox<DiagramDbContext>(Producers.Diagram);

// Event Consumers with full recovery, retry, DLQ, inbox idempotency (T-04)
builder.Services.AddEventConsumer<DiagramDbContext>(StreamNames.Workspace, ConsumerGroups.DiagramSvc)
    .On<ProjectCreated, ProjectCreatedHandler>()
    .On<MemberAdded, MemberAddedHandler>()
    .On<MemberRemoved, MemberRemovedHandler>()
    .On<LecturerAssigned, LecturerAssignedHandler>()
    .On<ProjectDeleted, ProjectDeletedHandler>();

builder.Services.AddEventConsumer<DiagramDbContext>(StreamNames.Render, ConsumerGroups.DiagramSvc)
    .On<RenderStarted, RenderStartedHandler>()
    .On<RenderCompleted, RenderCompletedHandler>()
    .On<RenderFailed, RenderFailedHandler>();

// CORS for React dev
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("ETag");
    });
});

var app = builder.Build();

app.UseCors("AllowClient");
app.UseUmlForgeDefaults();
app.MapControllers();
app.MapJobTriggerEndpoint("diagram");

// Auto-migrate on startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DiagramDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
