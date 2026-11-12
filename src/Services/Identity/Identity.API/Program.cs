using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Identity.API.Middleware;
using Identity.Repositories.Data;
using Identity.Repositories.Repositories;
using Identity.Service.DTOs;
using Identity.Service.Interfaces;
using Identity.Service.Jobs;
using Identity.Service.Services;
using Identity.Service.Validators;

var builder = WebApplication.CreateBuilder(args);

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UMLForge Identity Service API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Format: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
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

// Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

// Authentication & JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost,1433;Database=IdentityDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Redis
var redisConnectionString = builder.Configuration["REDIS_URL"]
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var config = ConfigurationOptions.Parse(redisConnectionString);
        config.AbortOnConnectFail = false;
        config.ConnectRetry = 5;
        config.KeepAlive = 60;
        return ConnectionMultiplexer.Connect(config);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not establish initial connection to Redis at {Connection}. Auto-retry enabled.", redisConnectionString);
        var config = ConfigurationOptions.Parse(redisConnectionString);
        config.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(config);
    }
});

// Repositories & Outbox
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOutbox, Outbox>();

// Domain Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

// Background Jobs & Outbox Publisher
builder.Services.AddSingleton<PurgeRefreshTokensJob>();
builder.Services.AddSingleton<IScheduledJob>(sp => sp.GetRequiredService<PurgeRefreshTokensJob>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PurgeRefreshTokensJob>());
builder.Services.AddHostedService<OutboxPublisher>();

// Validators
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<ChangeRoleRequest>, ChangeRoleRequestValidator>();

var app = builder.Build();

// RFC 7807 ProblemDetails middleware
app.UseMiddleware<ProblemDetailsMiddleware>();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UMLForge Identity Service API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint verifying Database and Redis readiness
app.MapGet("/health", async (IdentityDbContext dbContext, IConnectionMultiplexer? redis, ILogger<Program> logger) =>
{
    var dbHealthy = false;
    var redisHealthy = false;

    try
    {
        dbHealthy = await dbContext.Database.CanConnectAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Health check database connectivity check failed.");
        dbHealthy = false;
    }

    try
    {
        if (redis != null && redis.IsConnected)
        {
            var ping = await redis.GetDatabase().PingAsync();
            redisHealthy = ping.TotalMilliseconds >= 0;
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Health check Redis connectivity check failed.");
        redisHealthy = false;
    }

    var isHealthy = dbHealthy && redisHealthy;
    var response = new
    {
        status = isHealthy ? "healthy" : "degraded",
        service = "identity",
        timestamp = DateTime.UtcNow,
        checks = new
        {
            database = dbHealthy ? "healthy" : "unhealthy",
            redis = redisHealthy ? "healthy" : "unhealthy"
        }
    };

    return isHealthy
        ? Results.Ok(response)
        : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
});

// Auto migration & seed for Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    if (dbContext.Database.IsRelational())
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            await IdentityDbSeeder.SeedAsync(dbContext, app.Configuration);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not apply database migrations on startup.");
        }
    }
}

app.Run();

public partial class Program { }
