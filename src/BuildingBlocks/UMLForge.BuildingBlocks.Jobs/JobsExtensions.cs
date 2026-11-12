using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UMLForge.Contracts.Events;

namespace UMLForge.BuildingBlocks.Jobs;

public static class JobsExtensions
{
    /// <summary>Đăng ký job định kỳ (chạy nền theo lịch + chạy tay qua <see cref="IJobTrigger"/>).</summary>
    public static IServiceCollection AddScheduledJob<TJob>(this IServiceCollection services)
        where TJob : ScheduledJobBase
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TJob)))
            return services;

        services.AddSingleton<TJob>();
        services.AddSingleton<ScheduledJobBase>(sp => sp.GetRequiredService<TJob>());
        services.AddHostedService(sp => sp.GetRequiredService<TJob>());
        services.TryAddSingleton<IJobTrigger, JobTrigger>();
        return services;
    }

    /// <summary>
    /// <c>GET /api/admin/{service}/jobs</c> — danh sách job ·
    /// <c>POST /api/admin/{service}/jobs/{jobName}/run</c> — chạy ngay (chỉ Admin).
    /// </summary>
    public static RouteGroupBuilder MapJobTriggerEndpoint(this IEndpointRouteBuilder endpoints, string service)
    {
        var group = endpoints.MapGroup($"/api/admin/{service}/jobs")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin))
            .WithTags("Jobs");

        group.MapGet("/", (IJobTrigger trigger) => Results.Ok(trigger.Jobs));

        group.MapPost("/{jobName}/run", async (string jobName, IJobTrigger trigger, CancellationToken ct) =>
        {
            var result = await trigger.RunNowAsync(jobName, ct);
            return result is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Không có job '{jobName}'.",
                    extensions: new Dictionary<string, object?> { ["code"] = "JOB_NOT_FOUND" })
                : Results.Accepted(value: result);
        });

        return group;
    }
}
