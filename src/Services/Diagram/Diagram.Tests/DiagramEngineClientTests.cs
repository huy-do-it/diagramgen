using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Exceptions;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Services;
using Xunit;

namespace UMLForge.Diagram.Tests;

public class DiagramEngineClientTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly DiagramRepository _diagramRepository;
    private readonly DiagramVersionRepository _versionRepository;
    private readonly MemberAccessRepository _memberAccessRepository;
    private readonly DiagramEngineClient _engineClient;
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
        _engineClient = new DiagramEngineClient();
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.UserId).Returns(_testUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Student");
        _currentUserMock.Setup(u => u.Username).Returns("student1");

        _service = new DiagramService(
            _diagramRepository,
            _versionRepository,
            _memberAccessRepository,
            _currentUserMock.Object,
            engineClient: _engineClient);
    }

    [Fact]
    [Trait("BR", "BR-18")]
    public async Task ValidateDiagram_ValidUseCase_ReturnsValid()
    {
        var validJson = @"{
            ""systemBoundary"": ""CampusEats"",
            ""actors"": [""Customer""],
            ""useCases"": [""Order Food""],
            ""relations"": [
                { ""from"": ""Customer"", ""to"": ""Order Food"", ""type"": ""association"" }
            ]
        }";

        var result = await _engineClient.ValidateDiagramAsync(DiagramType.UseCase, validJson);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-18")]
    public async Task ValidateDiagram_AssociationBetweenTwoActors_ReturnsError()
    {
        var invalidJson = @"{
            ""systemBoundary"": ""System"",
            ""actors"": [""Actor1"", ""Actor2""],
            ""useCases"": [""UseCase1""],
            ""relations"": [
                { ""from"": ""Actor1"", ""to"": ""Actor2"", ""type"": ""association"" }
            ]
        }";

        var result = await _engineClient.ValidateDiagramAsync(DiagramType.UseCase, invalidJson);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must connect an actor with a use case"));
    }

    [Fact]
    [Trait("BR", "BR-20")]
    public async Task ValidateDiagram_IncludeCycle_ReturnsError()
    {
        var cycleJson = @"{
            ""systemBoundary"": ""System"",
            ""actors"": [""Customer""],
            ""useCases"": [""UC1"", ""UC2""],
            ""relations"": [
                { ""from"": ""Customer"", ""to"": ""UC1"", ""type"": ""association"" },
                { ""from"": ""UC1"", ""to"": ""UC2"", ""type"": ""include"" },
                { ""from"": ""UC2"", ""to"": ""UC1"", ""type"": ""include"" }
            ]
        }";

        var result = await _engineClient.ValidateDiagramAsync(DiagramType.UseCase, cycleJson);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must not form a cycle"));
    }

    [Fact]
    [Trait("BR", "BR-21")]
    public async Task ValidateDiagram_SequenceWithInsufficientParticipants_ReturnsError()
    {
        var invalidSeq = @"{
            ""participants"": [""User""],
            ""messages"": []
        }";

        var result = await _engineClient.ValidateDiagramAsync(DiagramType.Sequence, invalidSeq);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least two participants"));
    }

    [Fact]
    public async Task SuggestRelations_UnconnectedActor_ReturnsSuggestion()
    {
        var jsonWithOrphanActor = @"{
            ""systemBoundary"": ""System"",
            ""actors"": [""ConnectedActor"", ""OrphanActor""],
            ""useCases"": [""Login""],
            ""relations"": [
                { ""from"": ""ConnectedActor"", ""to"": ""Login"", ""type"": ""association"" }
            ]
        }";

        var suggestions = await _engineClient.SuggestRelationsAsync(DiagramType.UseCase, jsonWithOrphanActor);

        suggestions.Should().Contain(s => s.From == "OrphanActor" && s.To == "Login");
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

        var invalidUmlJson = @"{
            ""systemBoundary"": ""System"",
            ""actors"": [""Actor1"", ""Actor2""],
            ""useCases"": [""UseCase1""],
            ""relations"": [
                { ""from"": ""Actor1"", ""to"": ""Actor2"", ""type"": ""association"" }
            ]
        }";

        var request = new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Invalid UML Diagram",
            ModelJson = invalidUmlJson
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

        var validJson = @"{
            ""systemBoundary"": ""System"",
            ""actors"": [""User""],
            ""useCases"": [""Browse""],
            ""relations"": [
                { ""from"": ""User"", ""to"": ""Browse"", ""type"": ""association"" }
            ]
        }";

        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Valid Diagram",
            ModelJson = validJson,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

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

        var validJson = @"{
            ""systemBoundary"": ""System"",
            ""actors"": [""User""],
            ""useCases"": [""Browse""],
            ""relations"": [
                { ""from"": ""User"", ""to"": ""Browse"", ""type"": ""association"" }
            ]
        }";

        var diagram1 = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "D1",
            ModelJson = validJson,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram1);
        await _dbContext.SaveChangesAsync();

        var results = await _service.ValidateProjectAsync(_testProjectId);

        results.Should().HaveCount(1);
        results.First().IsValid.Should().BeTrue();
    }
}
