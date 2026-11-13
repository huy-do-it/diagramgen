using FluentAssertions;
using Identity.Service.Jobs;
using Identity.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UMLForge.BuildingBlocks.Jobs;

namespace Identity.Tests.Jobs;

public class JobRegistryTests
{
    [Fact]
    public async Task Registry_UsesSamePurgeInstanceForHostedJobAndManualTrigger()
    {
        var services = new ServiceCollection();
        var database = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddScoped(_ => TestDbContextFactory.CreateInMemoryDbContext(database));
        services.AddScheduledJob<PurgeRefreshTokensJob>();
        services.AddScheduledJob<PurgeRefreshTokensJob>();
        await using var provider = services.BuildServiceProvider();

        var job = provider.GetRequiredService<PurgeRefreshTokensJob>();
        provider.GetServices<IHostedService>().Should().ContainSingle().Which.Should().BeSameAs(job);
        var trigger = provider.GetRequiredService<IJobTrigger>();
        trigger.Jobs.Should().ContainSingle().Which.JobName.Should().Be("purge-refresh-tokens");

        var result = await trigger.RunNowAsync("purge-refresh-tokens");

        result.Should().NotBeNull();
        result!.Succeeded.Should().BeTrue();
        result.Trigger.Should().Be("manual");
        trigger.Jobs.Single().LastResult.Should().BeSameAs(result);
        (await trigger.RunNowAsync("unknown")).Should().BeNull();
    }
}
