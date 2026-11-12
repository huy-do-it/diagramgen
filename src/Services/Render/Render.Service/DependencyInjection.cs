using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UMLForge.Render.Repositories;
using UMLForge.Render.Repositories.Repositories;
using UMLForge.Render.Service.Interfaces;
using UMLForge.Render.Service.Models;
using UMLForge.Render.Service.Services;
using UMLForge.Render.Service.EventHandlers;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using UMLForge.Contracts.Grpc;
using FluentValidation;
using UMLForge.Render.Service.Validators;

namespace UMLForge.Render.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddRenderServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connection = configuration.GetConnectionString("RenderDb");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set ConnectionStrings__RenderDb before starting Render.");
        services.AddDbContext<RenderDbContext>(o => o.UseSqlServer(connection));
        services.AddScoped<IRenderJobRepository, RenderJobRepository>();
        services.AddScoped<IRenderPipeline, RenderPipeline>();
        services.AddScoped<IRenderJobRunner, RenderJobRunner>();
        services.AddScoped<IValidator<RenderRequested>, RenderRequestedValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddOutbox<RenderDbContext>(Producers.Render);
        services.AddEventConsumer<RenderDbContext>(StreamNames.Diagram, ConsumerGroups.RenderSvc)
            .On<RenderRequested, RenderRequestedHandler>();
        services.AddOptions<RenderOptions>().Bind(configuration.GetSection(RenderOptions.SectionName))
            .Validate(o => o.TimeoutSeconds is > 0 and <= 30, "Render timeout must be between 1 and 30 seconds.").ValidateOnStart();
        services.AddOptions<PlantUmlOptions>().Bind(configuration.GetSection(PlantUmlOptions.SectionName))
            .Validate(o => IsHttpUrl(o.BaseUrl), "PlantUml:BaseUrl must be an absolute HTTP(S) URL.").ValidateOnStart();
        services.AddOptions<DiagramEngineOptions>().Bind(configuration.GetSection(DiagramEngineOptions.SectionName))
            .Validate(o => IsHttpUrl(o.Url), "DiagramEngine:Url must be an absolute HTTP(S) URL.").ValidateOnStart();
        services.AddHttpClient<PlantUmlRenderer>((provider, client) =>
        {
            var url = provider.GetRequiredService<IOptions<PlantUmlOptions>>().Value.BaseUrl;
            client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        if (configuration.GetValue<bool>("DiagramEngine:UseFake"))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("DiagramEngine:UseFake is allowed only in Development.");
            services.AddScoped<IDiagramEngineClient, FakeDiagramEngineClient>();
        }
        else
        {
            services.AddGrpcClient<DiagramEngineService.DiagramEngineServiceClient>((provider, o) =>
                o.Address = new Uri(provider.GetRequiredService<IOptions<DiagramEngineOptions>>().Value.Url))
                .ConfigureChannel(o => o.ServiceConfig = new ServiceConfig
                {
                    MethodConfigs =
                    {
                        new MethodConfig
                        {
                            Names = { MethodName.Default },
                            RetryPolicy = new RetryPolicy
                            {
                                MaxAttempts = 3, InitialBackoff = TimeSpan.FromMilliseconds(100),
                                MaxBackoff = TimeSpan.FromSeconds(1), BackoffMultiplier = 2,
                                RetryableStatusCodes = { StatusCode.Unavailable }
                            }
                        }
                    }
                });
            services.AddScoped<IDiagramEngineClient, GrpcDiagramEngineClient>();
        }
        return services;
    }

    private static bool IsHttpUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
