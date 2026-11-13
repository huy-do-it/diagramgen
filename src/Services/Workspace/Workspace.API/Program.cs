using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.BuildingBlocks.Jobs;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Data;
using Workspace.Repositories.Repositories;
using Workspace.Service.EventHandlers;
using Workspace.Service.Interfaces;
using Workspace.Service.Jobs;
using Workspace.Service.Services;
using Workspace.Service.Validators;

var builder = WebApplication.CreateBuilder(args);

// Standard UMLForge defaults: Serilog, ProblemDetails, JWT, Swagger, HealthCheck, Controllers
builder.AddUmlForgeDefaults(Producers.Workspace);

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration.GetConnectionString("WorkspaceDb")
    ?? "Server=localhost,1433;Database=WorkspaceDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<WorkspaceDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Outbox publisher (automatic Redis + IOutbox + OutboxPublisher<WorkspaceDbContext>)
builder.Services.AddOutbox<WorkspaceDbContext>(Producers.Workspace);

// Directory synchronization consumer on identity.events stream
builder.Services.AddEventConsumer<WorkspaceDbContext>(StreamNames.Identity, ConsumerGroups.WorkspaceSvc)
    .On<UserRegistered, UserRegisteredHandler>()
    .On<UserRoleChanged, UserRoleChangedHandler>()
    .On<UserLocked, UserLockedHandler>();

// Scheduled Background Job
builder.Services.AddScheduledJob<ExpireInvitationsJob>();

// Validators
builder.Services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();

// Repositories & Services DI
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddScoped<IUserDirectoryRepository, UserDirectoryRepository>();

builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IUserDirectoryService, UserDirectoryService>();

var app = builder.Build();

// Standard UMLForge middlewares: CorrelationId, RequestLogging, ProblemDetails, Swagger, Auth, HealthChecks, Controllers
app.UseUmlForgeDefaults();

// Standard job trigger endpoints: GET /api/admin/workspace/jobs & POST /api/admin/workspace/jobs/{jobName}/run
app.MapJobTriggerEndpoint("workspace");

// Apply database migrations on startup (unconditional for relational databases; fail-fast if broken)
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WorkspaceDbContext>();
    if (dbContext.Database.IsRelational())
    {
        try
        {
            await dbContext.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Workspace schema migration failed; startup aborted.");
            throw;
        }
    }
}

await app.RunAsync();

public partial class Program { }
