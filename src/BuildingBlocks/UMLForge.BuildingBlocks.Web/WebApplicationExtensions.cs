using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Web;

public static class WebApplicationExtensions
{
    private const string LogTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>Serilog + CorrelationId + ProblemDetails + JWT + Swagger (nút Authorize) + /health + Controllers.</summary>
    public static WebApplicationBuilder AddUmlForgeDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        builder.AddUmlForgeLogging(serviceName);

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            if (CorrelationContext.Current is { } correlationId)
                problem.Extensions["correlationId"] = correlationId;
            if (!problem.Extensions.ContainsKey("code"))
                problem.Extensions["code"] = problem is ValidationProblemDetails ? "VALIDATION" : $"HTTP_{problem.Status}";
        });

        builder.Services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(
            options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddUmlForgeJwt(builder.Configuration);
        builder.Services.AddUmlForgeSwagger(serviceName);
        builder.Services.AddHealthChecks();
        return builder;
    }

    /// <summary>Chỉ log có cấu trúc (Serilog) — dùng riêng cho service không cần JWT/Swagger như DiagramEngine.</summary>
    public static WebApplicationBuilder AddUmlForgeLogging(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, logger) => logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(outputTemplate: LogTemplate));
        return builder;
    }

    /// <summary>Thứ tự middleware: CorrelationId → log request → ProblemDetails → Swagger → Auth → /health + Controllers.</summary>
    public static WebApplication UseUmlForgeDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
            exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose : LogEventLevel.Information);
        app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
        app.UseStatusCodePages();

        if (app.Configuration.GetValue("Swagger:Enabled", true))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapControllers();
        return app;
    }

    private static IServiceCollection AddUmlForgeSwagger(this IServiceCollection services, string serviceName)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = $"UMLForge {serviceName} API", Version = "v1" });

            var bearer = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Dán access token (không cần gõ chữ Bearer).",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            };
            options.AddSecurityDefinition("Bearer", bearer);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });

            // XML comment của các project cùng tiền tố với project API (ví dụ "Diagram.") và các gói UMLForge.*
            var entryName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
            var prefix = entryName.Split('.')[0] + ".";
            foreach (var xml in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.xml"))
            {
                var name = Path.GetFileNameWithoutExtension(xml);
                if (name.StartsWith(prefix, StringComparison.Ordinal) || name.StartsWith("UMLForge.", StringComparison.Ordinal))
                    options.IncludeXmlComments(xml, includeControllerXmlComments: true);
            }

            options.SupportNonNullableReferenceTypes();
        });
        return services;
    }
}
