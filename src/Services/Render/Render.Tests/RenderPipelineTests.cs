using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Service.Models;
using UMLForge.Render.Service.Services;

namespace UMLForge.Render.Tests;

public sealed class RenderPipelineTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task ProcessJobAsync_ValidSnapshot_ReturnsArtifactWithoutMutatingJob(int type)
    {
        var job = Fixtures.Job(type);
        var originalCreatedAt = job.CreatedAt;
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        var result = await Pipeline(new StubEngine(), http).ProcessJobAsync(job);
        Assert.True(result.IsSuccess);
        Assert.Equal(Fixtures.Svg, result.SvgXml);
        Assert.Contains("@startuml", result.PlantUml);
        Assert.True(result.DurationMs >= 0);
        Assert.Equal(RenderJobStatus.Queued, job.Status);
        Assert.Null(job.Artifact);
        Assert.Null(job.FinishedAt);
        Assert.Equal(originalCreatedAt, job.CreatedAt);
    }

    [Fact]
    public async Task ProcessJobAsync_InvalidUml_DoesNotGenerateOrRender()
    {
        var engine = new StubEngine { Validate = _ => Task.FromResult(false) };
        var handler = new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse()));
        using var http = Fixtures.Http(handler);
        var result = await Pipeline(engine, http).ProcessJobAsync(Fixtures.Job());
        Assert.False(result.IsSuccess);
        Assert.Equal(0, engine.GenerateCalls);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("{")] [InlineData("[]")] [InlineData("null")] [InlineData("")]
    public async Task ProcessJobAsync_InvalidJson_DoesNotCallEngine(string json)
    {
        var job = Fixtures.Job(); job.ModelJson = json;
        var engine = new StubEngine();
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        Assert.False((await Pipeline(engine, http).ProcessJobAsync(job)).IsSuccess);
        Assert.Equal(0, engine.ValidateCalls);
    }

    [Fact]
    public async Task ProcessJobAsync_JsonExceedsUtf8Limit_DoesNotCallEngine()
    {
        var job = Fixtures.Job(); job.ModelJson = "{\"x\":\"" + new string('漢', 90_000) + "\"}";
        var engine = new StubEngine();
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        Assert.False((await Pipeline(engine, http).ProcessJobAsync(job)).IsSuccess);
        Assert.Equal(0, engine.ValidateCalls);
    }

    [Theory]
    [InlineData(0)] [InlineData(6)]
    public async Task ProcessJobAsync_UnknownType_IsRejected(int type)
    {
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        Assert.False((await Pipeline(new StubEngine(), http).ProcessJobAsync(Fixtures.Job(type))).IsSuccess);
    }

    [Fact, Trait("BR", "BR-27")]
    public async Task ProcessJobAsync_EngineExceedsPipelineBudget_ReturnsTimeout()
    {
        var engine = new StubEngine
        {
            Validate = async ct => { await Task.Delay(Timeout.Infinite, ct); return true; }
        };
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        var result = await Pipeline(engine, http, 1).ProcessJobAsync(Fixtures.Job());
        Assert.False(result.IsSuccess);
        Assert.Contains("thời gian", result.Error);
        Assert.Equal(0, engine.GenerateCalls);
        Assert.Null(result.SvgXml);
    }

    [Fact, Trait("BR", "BR-27")]
    public async Task ProcessJobAsync_Shutdown_PropagatesCancellation()
    {
        using var source = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new StubEngine
        {
            Validate = async ct => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); return true; }
        };
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        var task = Pipeline(engine, http).ProcessJobAsync(Fixtures.Job(), source.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)] [InlineData(StatusCode.Internal)]
    public async Task ProcessJobAsync_EngineInfrastructureFailure_PropagatesForConsumerRecovery(StatusCode status)
    {
        var engine = new StubEngine { Validate = _ => throw new RpcException(new Status(status, "internal-host:5006")) };
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        await Assert.ThrowsAsync<RpcException>(() => Pipeline(engine, http).ProcessJobAsync(Fixtures.Job()));
    }

    [Theory]
    [InlineData(StatusCode.DeadlineExceeded)] [InlineData(StatusCode.InvalidArgument)]
    public async Task ProcessJobAsync_EngineExpectedFailure_ReturnsSafeError(StatusCode status)
    {
        var engine = new StubEngine { Validate = _ => throw new RpcException(new Status(status, "internal-secret")) };
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        var result = await Pipeline(engine, http).ProcessJobAsync(Fixtures.Job());
        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("internal-secret", result.Error);
    }

    [Fact, Trait("BR", "BR-28")]
    public async Task ProcessJobAsync_RenderFailure_PreservesExistingArtifact()
    {
        var job = Fixtures.Job();
        job.Artifact = new RenderArtifact { JobId = job.Id, PlantUml = "old", SvgXml = Fixtures.Svg };
        var artifact = job.Artifact;
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => throw new HttpRequestException("secret-host")));
        var result = await Pipeline(new StubEngine(), http).ProcessJobAsync(job);
        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("secret-host", result.Error);
        Assert.Same(artifact, job.Artifact);
        Assert.Equal(Fixtures.Svg, job.Artifact.SvgXml);
        Assert.Null(result.SvgXml);
    }

    [Fact]
    public async Task ProcessJobAsync_EmptyGeneratedCode_ReturnsFailure()
    {
        var engine = new StubEngine { Generate = _ => Task.FromResult(" ") };
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse())));
        Assert.False((await Pipeline(engine, http).ProcessJobAsync(Fixtures.Job())).IsSuccess);
    }

    private static RenderPipeline Pipeline(StubEngine engine, HttpClient http, int timeout = 30)
        => new(engine, new PlantUmlRenderer(http), Options.Create(new RenderOptions { TimeoutSeconds = timeout }), NullLogger<RenderPipeline>.Instance);
}
