using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using UMLForge.Notification.API;
using UMLForge.Notification.Repositories;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service.Interfaces;
using UMLForge.Notification.Service.Models;
using UMLForge.Notification.Service.Services;
using UMLForge.Notification.Service.Validators;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("NotificationDb");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Set ConnectionStrings__NotificationDb before starting Notification.");

builder.Services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IValidator<NotificationQuery>, NotificationQueryValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddNotificationJwt(builder.Configuration);
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole("Admin")));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions.TryAdd("code", "HTTP_" + context.ProblemDetails.Status);
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "UMLForge Notification API", Version = "v1" });
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "UMLForge.Notification.API.xml"));
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    var correlation = Guid.TryParse(context.Request.Headers["X-Correlation-Id"], out var id) && id != Guid.Empty ? id : Guid.NewGuid();
    context.TraceIdentifier = correlation.ToString();
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });
    using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier });
    await next(context);
});
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    ProblemDetails problem;
    if (exception is ValidationException validation)
    {
        problem = new HttpValidationProblemDetails(validation.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray()))
        { Status = 400, Title = "Tham số không hợp lệ." };
        problem.Extensions["code"] = "VALIDATION";
    }
    else
    {
        problem = new ProblemDetails { Status = 500, Title = "Không thể xử lý yêu cầu lúc này." };
    }
    context.Response.StatusCode = problem.Status!.Value;
    await context.RequestServices.GetRequiredService<IProblemDetailsService>()
        .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
}));
app.UseStatusCodePages(async statusContext =>
{
    await Results.Problem(statusCode: statusContext.HttpContext.Response.StatusCode).ExecuteAsync(statusContext.HttpContext);
});
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (NotificationDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken) ? Results.Ok(new { status = "Healthy" }) : Results.StatusCode(503));
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
