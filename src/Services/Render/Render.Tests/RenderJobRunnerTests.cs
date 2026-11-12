using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.Contracts.Events;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Repositories.Repositories;
using UMLForge.Render.Service.Interfaces;
using UMLForge.Render.Service.Models;
using UMLForge.Render.Service.Services;

namespace UMLForge.Render.Tests;

public sealed class RenderJobRunnerTests
{
    [Fact]
    public async Task RunNextAsync_EmptyQueue_ReturnsFalse()
    {
        var repository = new Mock<IRenderJobRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetNextQueuedAsync(It.IsAny<CancellationToken>())).ReturnsAsync((RenderJob?)null);
        Assert.False(await Runner(repository.Object, new Mock<IRenderPipeline>().Object, new Mock<IOutbox>().Object).RunNextAsync());
    }

    [Fact, Trait("BR", "BR-26")]
    public async Task RunNextAsync_LosesClaim_DoesNotRunPipeline()
    {
        var job = Fixtures.Job();
        var repository = Repository(job);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException());
        var pipeline = new Mock<IRenderPipeline>(MockBehavior.Strict);
        Assert.False(await Runner(repository.Object, pipeline.Object, new Mock<IOutbox>().Object).RunNextAsync());
        pipeline.Verify(x => x.ProcessJobAsync(It.IsAny<RenderJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("BR", "BR-26")]
    public async Task RunNextAsync_Success_SavesStartedBeforePipelineAndCompletionWithArtifact()
    {
        var job = Fixtures.Job(); job.CorrelationId = Guid.NewGuid();
        var repository = Repository(job);
        var events = new List<object>();
        var outbox = new Mock<IOutbox>(MockBehavior.Strict);
        outbox.Setup(x => x.Add(StreamNames.Render, It.IsAny<RenderStarted>())).Callback<string, RenderStarted>((_, e) => events.Add(e));
        outbox.Setup(x => x.Add(StreamNames.Render, It.IsAny<RenderCompleted>())).Callback<string, RenderCompleted>((_, e) => events.Add(e));
        var saves = 0;
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() =>
        {
            saves++;
            Assert.Equal(saves == 1 ? RenderJobStatus.Running : RenderJobStatus.Done, job.Status);
            Assert.Equal(saves, events.Count);
        }).ReturnsAsync(1);
        var pipeline = new Mock<IRenderPipeline>(MockBehavior.Strict);
        pipeline.Setup(x => x.ProcessJobAsync(job, It.IsAny<CancellationToken>())).Callback(() =>
        {
            Assert.Equal(1, saves);
            Assert.Equal(job.CorrelationId, CorrelationContext.Current);
        }).ReturnsAsync(new RenderResult("code", Fixtures.Svg, null, 123));
        Assert.True(await Runner(repository.Object, pipeline.Object, outbox.Object).RunNextAsync());
        Assert.Equal(2, saves); Assert.Equal(1, job.Attempt);
        Assert.Equal(Fixtures.Svg, job.Artifact!.SvgXml);
        Assert.Equal(123, job.DurationMs);
        var completed = Assert.IsType<RenderCompleted>(events[1]);
        Assert.Equal(job.RequestedBy, completed.RequestedBy);
        Assert.Equal(job.DiagramVersion, completed.Version);
        Assert.Null(CorrelationContext.Current);
    }

    [Fact, Trait("BR", "BR-28")]
    public async Task RunNextAsync_Failure_KeepsPreviousArtifactAndPublishesFailed()
    {
        var job = Fixtures.Job(); job.Artifact = new RenderArtifact { JobId = job.Id, PlantUml = "previous", SvgXml = Fixtures.Svg };
        var repository = Repository(job); repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var pipeline = new Mock<IRenderPipeline>();
        pipeline.Setup(x => x.ProcessJobAsync(job, It.IsAny<CancellationToken>())).ReturnsAsync(new RenderResult(null, null, "safe failure", 5));
        var outbox = new Mock<IOutbox>();
        Assert.True(await Runner(repository.Object, pipeline.Object, outbox.Object).RunNextAsync());
        Assert.Equal(RenderJobStatus.Failed, job.Status);
        Assert.Equal(Fixtures.Svg, job.Artifact.SvgXml);
        outbox.Verify(x => x.Add(StreamNames.Render, It.Is<RenderFailed>(e => e.Error == "safe failure")), Times.Once);
        outbox.Verify(x => x.Add(StreamNames.Render, It.IsAny<RenderCompleted>()), Times.Never);
    }

    [Fact]
    public async Task RunNextAsync_InfrastructureFailure_LeavesRunningAndPropagates()
    {
        var job = Fixtures.Job(); var repository = Repository(job);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var pipeline = new Mock<IRenderPipeline>();
        pipeline.Setup(x => x.ProcessJobAsync(job, It.IsAny<CancellationToken>())).ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "controlled")));
        var outbox = new Mock<IOutbox>();
        await Assert.ThrowsAsync<RpcException>(() => Runner(repository.Object, pipeline.Object, outbox.Object).RunNextAsync());
        Assert.Equal(RenderJobStatus.Running, job.Status);
        Assert.Null(job.FinishedAt);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(x => x.Add(StreamNames.Render, It.IsAny<RenderFailed>()), Times.Never);
    }

    private static Mock<IRenderJobRepository> Repository(RenderJob job)
    {
        var repository = new Mock<IRenderJobRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetNextQueuedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(job);
        return repository;
    }
    private static RenderJobRunner Runner(IRenderJobRepository repository, IRenderPipeline pipeline, IOutbox outbox)
        => new(repository, pipeline, outbox, TimeProvider.System, NullLogger<RenderJobRunner>.Instance);
}
