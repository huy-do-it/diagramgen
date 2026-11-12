using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UMLForge.Diagram.API.Middleware;
using UMLForge.Diagram.API.Services;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.EventHandlers;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<DiagramDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DiagramDb")));

// Repositories & Services
builder.Services.AddScoped<IDiagramRepository, DiagramRepository>();
builder.Services.AddScoped<IDiagramVersionRepository, DiagramVersionRepository>();
builder.Services.AddScoped<IMemberAccessRepository, MemberAccessRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IOutbox, Outbox>();
builder.Services.AddScoped<IDiagramService, DiagramService>();
builder.Services.AddScoped<IWorkspaceEventsHandler, WorkspaceEventsHandler>();
builder.Services.AddScoped<IRenderEventsHandler, RenderEventsHandler>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IDiagramExportService, DiagramExportService>();
builder.Services.AddScoped<IDiagramEngineClient, DiagramEngineClient>();
builder.Services.AddScoped<IMemberAccessService, MemberAccessService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<WorkspaceEventsConsumer>();
builder.Services.AddHostedService<RenderEventsConsumer>();

// Controllers & CORS
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("ETag");
    });
});

// JWT Authentication (TASKS §1.2)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            NameClaimType = "unique_name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// Swagger with JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UMLForge Diagram API",
        Version = "v1",
        Description = "Microservice for managing diagrams, versions, and validation (Thiện)."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter token directly.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Auto-migrate Database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DiagramDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ProblemDetailsMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UMLForge Diagram API v1");
    });
}

app.UseCors("AllowClient");

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "diagram", timestamp = DateTime.UtcNow }));

app.MapControllers();

app.Run();

// For integration testing WebApplicationFactory
public partial class Program { }
