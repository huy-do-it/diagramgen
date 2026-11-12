using System.Text;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using UMLForge.Contracts.Events;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Repositories.Repositories;
using UMLForge.Render.Service.EventHandlers;
using UMLForge.Render.Service.Validators;
using UMLForge.Render.Service;

namespace UMLForge.Render.Tests;

public sealed class RenderRequestedHandlerTests
{
    [Fact]
    public async Task HandleAsync_RegisteredServices_ValidatesBeforeDatabaseAccess()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RenderDb"] = "Server=unused;Database=unused;Integrated Security=True",
            ["DiagramEngine:UseFake"] = "true"
        }).Build();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRenderServices(configuration, environment.Object);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RenderRequestedHandler>();

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(
            EventEnvelope.Create(Producers.Diagram, Request() with { Version = 0 }), default));
    }

    [Fact, Trait("BR", "BR-26")]
    public async Task HandleAsync_NewRequest_QueuesSnapshotWithoutSavingOrRunningPipeline()
    {
        var repository = new Mock<IRenderJobRepository>(MockBehavior.Strict);
        var request = Request();
        var envelope = EventEnvelope.Create(Producers.Diagram, request, Guid.NewGuid());
        repository.Setup(x => x.GetByDiagramVersionAsync(request.DiagramId, request.Version, It.IsAny<CancellationToken>())).ReturnsAsync((RenderJob?)null);
        RenderJob? captured = null;
        repository.Setup(x => x.Add(It.IsAny<RenderJob>())).Callback<RenderJob>(x => captured = x);
        await Handler(repository.Object).HandleAsync(envelope, default);
        Assert.NotNull(captured);
        Assert.Equal(RenderJobStatus.Queued, captured.Status);
        Assert.Equal(request.ActorUserId, captured.RequestedBy);
        Assert.Equal(request.ModelJson, captured.ModelJson);
        Assert.Equal(request.Version, captured.DiagramVersion);
        Assert.Equal(envelope.CorrelationId, captured.CorrelationId);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, Trait("BR", "BR-26")]
    [InlineData(RenderJobStatus.Queued)] [InlineData(RenderJobStatus.Running)] [InlineData(RenderJobStatus.Done)] [InlineData(RenderJobStatus.Failed)]
    public async Task HandleAsync_BusinessKeyExists_DoesNotOverwriteSnapshot(RenderJobStatus status)
    {
        var repository = new Mock<IRenderJobRepository>(MockBehavior.Strict);
        var request = Request(); var existing = Fixtures.Job(); existing.Status = status;
        repository.Setup(x => x.GetByDiagramVersionAsync(request.DiagramId, request.Version, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        await Handler(repository.Object).HandleAsync(EventEnvelope.Create(Producers.Diagram, request), default);
        Assert.Equal("{}", existing.ModelJson);
        Assert.Equal(status, existing.Status);
        repository.Verify(x => x.Add(It.IsAny<RenderJob>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task HandleAsync_InvalidPayload_ThrowsWithoutAccessingRepository(RenderRequested request, string property)
    {
        var repository = new Mock<IRenderJobRepository>(MockBehavior.Strict);
        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler(repository.Object)
            .HandleAsync(EventEnvelope.Create(Producers.Diagram, request), default));
        Assert.Contains(error.Errors, x => x.PropertyName == property);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task HandleAsync_AtInputLimits_QueuesSnapshot(int type)
    {
        var request = Request() with
        {
            DiagramType = type, Version = 1, Title = new string('t', 200), ModelJson = ModelAtLimit()
        };
        Assert.Equal(256 * 1024, Encoding.UTF8.GetByteCount(request.ModelJson));
        var repository = new Mock<IRenderJobRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetByDiagramVersionAsync(request.DiagramId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RenderJob?)null);
        repository.Setup(x => x.Add(It.IsAny<RenderJob>()));

        await Handler(repository.Object).HandleAsync(EventEnvelope.Create(Producers.Diagram, request), default);

        repository.Verify(x => x.Add(It.Is<RenderJob>(j => j.ModelJson == request.ModelJson && j.Title == request.Title)), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        var request = Request();
        yield return [request with { DiagramId = Guid.Empty }, nameof(request.DiagramId)];
        yield return [request with { ProjectId = Guid.Empty }, nameof(request.ProjectId)];
        yield return [request with { ActorUserId = Guid.Empty }, nameof(request.ActorUserId)];
        yield return [request with { Version = 0 }, nameof(request.Version)];
        yield return [request with { Version = -1 }, nameof(request.Version)];
        yield return [request with { DiagramType = 0 }, nameof(request.DiagramType)];
        yield return [request with { DiagramType = 6 }, nameof(request.DiagramType)];
        yield return [request with { Title = null! }, nameof(request.Title)];
        yield return [request with { Title = "" }, nameof(request.Title)];
        yield return [request with { Title = " \t" }, nameof(request.Title)];
        yield return [request with { Title = new string('t', 201) }, nameof(request.Title)];
        yield return [request with { ModelJson = null! }, nameof(request.ModelJson)];
        yield return [request with { ModelJson = "" }, nameof(request.ModelJson)];
        yield return [request with { ModelJson = " \t" }, nameof(request.ModelJson)];
        yield return [request with { ModelJson = ModelAtLimit() + " " }, nameof(request.ModelJson)];
    }

    private static string ModelAtLimit() => "{\"x\":\"" + new string('é', (256 * 1024 - 8) / 2) + "\"}";
    private static RenderRequestedHandler Handler(IRenderJobRepository repository) => new(repository, new RenderRequestedValidator());
    private static RenderRequested Request() => new(Guid.NewGuid(), Guid.NewGuid(), 2, 3, "Snapshot", "{}", Guid.NewGuid());
}
