using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Grpc;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Validation;
using UMLForge.Diagram.Service.Services;
using Xunit;

namespace UMLForge.Diagram.Tests;

public class DiagramEngineClientTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly DiagramRepository _diagramRepository;
    private readonly DiagramVersionRepository _versionRepository;
    private readonly MemberAccessRepository _memberAccessRepository;
    private readonly Mock<IDiagramEngineClient> _engineClientMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly DiagramService _service;

    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testProjectId = Guid.NewGuid();

    public DiagramEngineClientTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _diagramRepository = new DiagramRepository(_dbContext);
        _versionRepository = new DiagramVersionRepository(_dbContext);
        _memberAccessRepository = new MemberAccessRepository(_dbContext);
        _engineClientMock = new Mock<IDiagramEngineClient>();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.UserId).Returns(_testUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Student");
        _currentUserMock.Setup(u => u.Username).Returns("student1");

        _service = new DiagramService(
            _diagramRepository,
            _versionRepository,
            _memberAccessRepository,
            _currentUserMock.Object,
            engineClient: _engineClientMock.Object);
    }

    [Fact]
    public async Task FakeDiagramEngineClient_ValidateDiagramAsync_ReturnsValid()
    {
        var client = new FakeDiagramEngineClient();
        var result = await client.ValidateDiagramAsync(DiagramType.UseCase, "{}");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task FakeDiagramEngineClient_SuggestRelationsAsync_ReturnsEmptyList()
    {
        var client = new FakeDiagramEngineClient();
        var suggestions = await client.SuggestRelationsAsync(DiagramType.UseCase, "{}");

        suggestions.Should().BeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-18")]
    public async Task CreateDiagram_ViolatingUmlRule_Throws422UnprocessableEntity()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Leader
        });
        await _memberAccessRepository.SaveChangesAsync();

        _engineClientMock
            .Setup(c => c.ValidateDiagramAsync(It.IsAny<DiagramType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Association between two actors is invalid." }
            });

        var request = new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Invalid UML Diagram",
            ModelJson = "{}"
        };

        var act = () => _service.CreateDiagramAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Status.Should().Be(422);
        ex.Which.Code.Should().Be("BR-UML");
    }

    [Fact]
    public async Task ValidateDiagram_OnService_ReturnsValidationResult()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });

        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Valid Diagram",
            ModelJson = "{}",
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        _engineClientMock
            .Setup(c => c.ValidateDiagramAsync(diagram.Type, diagram.ModelJson, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramValidationResult { IsValid = true });

        var result = await _service.ValidateDiagramAsync(diagram.Id);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProject_ReturnsConsolidatedResults()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });

        var diagram1 = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "D1",
            ModelJson = "{}",
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram1);
        await _dbContext.SaveChangesAsync();

        _engineClientMock
            .Setup(c => c.ValidateProjectAsync(It.IsAny<IEnumerable<(Guid, DiagramType, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectDiagramValidationResult>
            {
                new() { DiagramId = diagram1.Id, IsValid = true }
            });

        var results = await _service.ValidateProjectAsync(_testProjectId);

        results.Should().HaveCount(1);
        results.First().IsValid.Should().BeTrue();
    }

    private static AsyncUnaryCall<T> MakeCall<T>(T response)
    {
        return new AsyncUnaryCall<T>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    private static AsyncUnaryCall<T> MakeThrowingCall<T>(RpcException ex)
    {
        return new AsyncUnaryCall<T>(
            Task.FromException<T>(ex),
            Task.FromResult(new Metadata()),
            () => ex.Status,
            () => new Metadata(),
            () => { });
    }

    [Fact]
    public async Task GrpcDiagramEngineClient_Success_ReturnsValidationResult()
    {
        var grpcClientMock = new Mock<DiagramEngineService.DiagramEngineServiceClient>();
        var loggerMock = new Mock<ILogger<GrpcDiagramEngineClient>>();

        grpcClientMock
            .Setup(c => c.ValidateDiagramAsync(
                It.IsAny<DiagramValidationRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(MakeCall(new DiagramValidationResponse { IsValid = true }));

        var client = new GrpcDiagramEngineClient(grpcClientMock.Object, loggerMock.Object);
        var result = await client.ValidateDiagramAsync(DiagramType.UseCase, "{}");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GrpcDiagramEngineClient_Unavailable_RetriesAndThrows503WhenExhausted()
    {
        var grpcClientMock = new Mock<DiagramEngineService.DiagramEngineServiceClient>();
        var loggerMock = new Mock<ILogger<GrpcDiagramEngineClient>>();
        var attempts = 0;

        grpcClientMock
            .Setup(c => c.ValidateDiagramAsync(
                It.IsAny<DiagramValidationRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return MakeThrowingCall<DiagramValidationResponse>(
                    new RpcException(new Status(StatusCode.Unavailable, "Engine unreachable")));
            });

        var client = new GrpcDiagramEngineClient(grpcClientMock.Object, loggerMock.Object);
        var act = () => client.ValidateDiagramAsync(DiagramType.UseCase, "{}");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Status.Should().Be(503);
        ex.Which.Code.Should().Be("BR-503");
        attempts.Should().Be(3); // 1 initial + 2 retries = 3 attempts
    }

    [Fact]
    public async Task GrpcDiagramEngineClient_Cancelled_ThrowsOperationCanceledException()
    {
        var grpcClientMock = new Mock<DiagramEngineService.DiagramEngineServiceClient>();
        var loggerMock = new Mock<ILogger<GrpcDiagramEngineClient>>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var client = new GrpcDiagramEngineClient(grpcClientMock.Object, loggerMock.Object);
        var act = () => client.ValidateDiagramAsync(DiagramType.UseCase, "{}", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GrpcDiagramEngineClient_DeadlineExceeded_Throws503WithoutRetrying()
    {
        var grpcClientMock = new Mock<DiagramEngineService.DiagramEngineServiceClient>();
        var loggerMock = new Mock<ILogger<GrpcDiagramEngineClient>>();
        var attempts = 0;

        grpcClientMock
            .Setup(c => c.ValidateDiagramAsync(
                It.IsAny<DiagramValidationRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return MakeThrowingCall<DiagramValidationResponse>(
                    new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline passed")));
            });

        var client = new GrpcDiagramEngineClient(grpcClientMock.Object, loggerMock.Object);
        var act = () => client.ValidateDiagramAsync(DiagramType.UseCase, "{}");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Status.Should().Be(503);
        ex.Which.Code.Should().Be("BR-503");
        attempts.Should().Be(1); // DeadlineExceeded does not retry, fails immediately
    }

    [Fact]
    public async Task GrpcDiagramEngineClient_AttachesCorrelationHeader()
    {
        var grpcClientMock = new Mock<DiagramEngineService.DiagramEngineServiceClient>();
        var loggerMock = new Mock<ILogger<GrpcDiagramEngineClient>>();
        Metadata? capturedMetadata = null;

        grpcClientMock
            .Setup(c => c.ValidateDiagramAsync(
                It.IsAny<DiagramValidationRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<DiagramValidationRequest, Metadata, DateTime?, CancellationToken>((_, meta, _, _) =>
            {
                capturedMetadata = meta;
            })
            .Returns(MakeCall(new DiagramValidationResponse { IsValid = true }));

        var expectedCorrelationId = Guid.NewGuid();
        using (UMLForge.Contracts.Events.CorrelationContext.Begin(expectedCorrelationId, "test-op"))
        {
            var client = new GrpcDiagramEngineClient(grpcClientMock.Object, loggerMock.Object);
            await client.ValidateDiagramAsync(DiagramType.UseCase, "{}");
        }

        capturedMetadata.Should().NotBeNull();
        var entry = capturedMetadata!.FirstOrDefault(e => e.Key == UMLForge.Contracts.Events.CorrelationKeys.GrpcMetadata);
        entry.Should().NotBeNull();
        entry!.Value.Should().Be(expectedCorrelationId.ToString());
    }
}
