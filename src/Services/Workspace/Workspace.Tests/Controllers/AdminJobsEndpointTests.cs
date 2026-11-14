using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UMLForge.BuildingBlocks.Jobs;
using Workspace.API.Controllers;

namespace Workspace.Tests.Controllers;

public class AdminJobsEndpointTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LiteralRoute_Returns202AndUnchangedJobRunResult(bool succeeded)
    {
        var result = new JobRunResult("expire-invitations", succeeded, "expired 0 invitations", 12,
            new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), "manual");
        var trigger = CreateTrigger(result);
        await using var app = await StartAsync(trigger.Object);
        using var client = CreateClient(app);

        var response = await client.PostAsync("/api/admin/workspace/jobs/expire-invitations/run", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(nameof(AdminJobsController), response.Headers.GetValues("X-Test-Endpoint").Single());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.Equal(6, body.EnumerateObject().Count());
        Assert.Equal(result.JobName, body.GetProperty("jobName").GetString());
        Assert.Equal(succeeded, body.GetProperty("succeeded").GetBoolean());
        Assert.Equal(result.Summary, body.GetProperty("summary").GetString());
        Assert.Equal(result.DurationMs, body.GetProperty("durationMs").GetInt64());
        Assert.Equal(result.FinishedAt, body.GetProperty("finishedAt").GetDateTime());
        Assert.Equal(result.Trigger, body.GetProperty("trigger").GetString());
        trigger.Verify(x => x.RunNowAsync("expire-invitations", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("expire-invitations")]
    [InlineData("unknown-job")]
    public async Task MissingJob_ReturnsShared404Problem(string jobName)
    {
        await using var app = await StartAsync(CreateTrigger(null).Object);
        using var client = CreateClient(app);

        var response = await client.PostAsync($"/api/admin/workspace/jobs/{jobName}/run", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("JOB_NOT_FOUND", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SharedRegistry_RemainsAvailableWithControllerRegistered()
    {
        var trigger = CreateTrigger(null);
        trigger.SetupGet(x => x.Jobs).Returns(new[] { new JobInfo("expire-invitations", TimeSpan.FromHours(1), null) });
        await using var app = await StartAsync(trigger.Object);
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/admin/workspace/jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("expire-invitations", json.RootElement[0].GetProperty("jobName").GetString());
        trigger.Verify(x => x.RunNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void OpenApiMetadata_Advertises202ForJobRunResult()
    {
        var method = typeof(AdminJobsController).GetMethod(nameof(AdminJobsController.RunExpireInvitations))!;
        var responses = method.GetCustomAttributes(typeof(ProducesResponseTypeAttribute), false)
            .Cast<ProducesResponseTypeAttribute>();
        Assert.Contains(responses, response => response.StatusCode == 202 && response.Type == typeof(JobRunResult));
        Assert.DoesNotContain(responses, response => response.StatusCode == 200);
    }

    private static Mock<IJobTrigger> CreateTrigger(JobRunResult? result)
    {
        var trigger = new Mock<IJobTrigger>();
        trigger.Setup(x => x.RunNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return trigger;
    }

    private static async Task<WebApplication> StartAsync(IJobTrigger trigger)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(trigger);
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminJobsController).Assembly);
        var app = builder.Build();
        app.UseRouting();
        // Auth is a fixture here; production JWT/401/403 are checked separately against the live container.
        app.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "fixture"));
            context.Response.Headers["X-Test-Endpoint"] = context.GetEndpoint()?.DisplayName;
            return next(context);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapJobTriggerEndpoint("workspace");
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
