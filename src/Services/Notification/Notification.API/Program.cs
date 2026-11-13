using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.Jobs;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using UMLForge.Notification.API;
using UMLForge.Notification.API.Jobs;
using UMLForge.Notification.Repositories;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service;
using UMLForge.Notification.Service.Interfaces;
using UMLForge.Notification.Service.Models;
using UMLForge.Notification.Service.Services;
using UMLForge.Notification.Service.Validators;

var builder = WebApplication.CreateBuilder(args);
builder.AddUmlForgeDefaults("notification");
var connection = builder.Configuration.GetConnectionString("NotificationDb");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Set ConnectionStrings__NotificationDb before starting Notification.");

builder.Services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IValidator<NotificationQuery>, NotificationQueryValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddNotificationConsumers();
builder.Services.AddScheduledJob<PurgeReadNotificationsJob>();
builder.Services.ConfigureNotificationJwt();
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole("Admin")));
builder.Services.AddHealthChecks().AddCheck<NotificationDatabaseHealthCheck>("notification-sql");
builder.Services.Configure<ProblemDetailsOptions>(o =>
{
    var previous = o.CustomizeProblemDetails;
    o.CustomizeProblemDetails = context =>
    {
        previous?.Invoke(context);
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddControllers().ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
{
    var problem = new ValidationProblemDetails(context.ModelState) { Status = 400, Title = "Tham số không hợp lệ." };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    problem.Extensions["code"] = "VALIDATION";
    var result = new BadRequestObjectResult(problem);
    result.ContentTypes.Add("application/problem+json");
    return result;
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    var correlation = Guid.TryParse(context.Request.Headers[CorrelationKeys.HttpHeader], out var id) && id != Guid.Empty ? id : Guid.NewGuid();
    context.Request.Headers[CorrelationKeys.HttpHeader] = correlation.ToString();
    context.TraceIdentifier = correlation.ToString();
    context.Response.OnStarting(() =>
    {
        context.Response.Headers[CorrelationKeys.HttpHeader] = correlation.ToString();
        return Task.CompletedTask;
    });
    await next(context);
});
app.UseStatusCodePages(async statusContext =>
    await Results.Problem(statusCode: statusContext.HttpContext.Response.StatusCode).ExecuteAsync(statusContext.HttpContext));
app.UseUmlForgeDefaults();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (ValidationException validation) when (!context.Response.HasStarted)
    {
        // Middleware chung xử lý BusinessRuleException; giữ validation của service là HTTP 400.
        throw new BusinessRuleException("VALIDATION", "Tham số không hợp lệ.", 400)
        {
            Errors = validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray())
        };
    }
});
app.MapJobTriggerEndpoint("notification");
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/admin/notification/dev/seed", async (INotificationService service, ClaimsPrincipal user, CancellationToken ct) =>
        Results.Ok(new SeedNotificationsResponse(await service.SeedDevelopmentAsync(Guid.Parse(user.FindFirstValue("sub")!), ct))))
        .RequireAuthorization("Admin")
        .WithSummary("Development: tạo 5 thông báo mẫu cho Admin hiện tại.")
        .Produces<SeedNotificationsResponse>()
        .Produces<ProblemDetails>(401).Produces<ProblemDetails>(403).Produces<ProblemDetails>(500);
}
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();

public partial class Program;
