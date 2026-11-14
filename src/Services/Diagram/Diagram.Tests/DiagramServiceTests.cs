using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Validation;
using UMLForge.Diagram.Service.Services;
using Xunit;

namespace UMLForge.Diagram.Tests;

public class DiagramServiceTests
{
    private sealed class TestOutbox(DiagramDbContext dbContext, string producer = Producers.Diagram) : IOutbox
    {
        public void Add<T>(string stream, T payload) where T : class
        {
            var type = EventTypeRegistry.NameOf<T>();
            var envelope = EventEnvelope.Create(producer, payload);
            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = envelope.EventId,
                Stream = stream,
                Type = type,
                Envelope = EventJson.Serialize(envelope),
                CreatedAt = envelope.OccurredAt
            });
        }
    }

    private readonly DiagramDbContext _dbContext;
    private readonly DiagramRepository _diagramRepository;
    private readonly DiagramVersionRepository _versionRepository;
    private readonly MemberAccessRepository _memberAccessRepository;
    private readonly IOutbox _outbox;
    private readonly ReviewRepository _reviewRepository;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IDiagramEngineClient> _engineClientMock;
    private readonly DiagramService _service;

    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testProjectId = Guid.NewGuid();

    private const string ValidUseCaseJson = @"{
        ""systemBoundary"": ""CampusEats"",
        ""actors"": [""Student"", ""Admin""],
        ""useCases"": [""Order Food"", ""Manage Menu""],
        ""relations"": [
            { ""from"": ""Student"", ""to"": ""Order Food"", ""type"": ""association"" }
        ]
    }";

    public DiagramServiceTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _diagramRepository = new DiagramRepository(_dbContext);
        _versionRepository = new DiagramVersionRepository(_dbContext);
        _memberAccessRepository = new MemberAccessRepository(_dbContext);
        _outbox = new TestOutbox(_dbContext);
        _reviewRepository = new ReviewRepository(_dbContext);
        _currentUserMock = new Mock<ICurrentUser>();
        _engineClientMock = new Mock<IDiagramEngineClient>();

        _engineClientMock.Setup(c => c.ValidateDiagramAsync(It.IsAny<DiagramType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramValidationResult { IsValid = true, Errors = new List<string>(), Warnings = new List<string>() });

        _currentUserMock.Setup(u => u.UserId).Returns(_testUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Student");
        _currentUserMock.Setup(u => u.Username).Returns("student1");

        _service = new DiagramService(_diagramRepository, _versionRepository, _memberAccessRepository, _currentUserMock.Object, _outbox, _reviewRepository, _engineClientMock.Object);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task CreateDiagram_UserWithoutAccess_ThrowsForbidden()
    {
        var request = new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = ValidUseCaseJson
        };

        var act = () => _service.CreateDiagramAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-11");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task GetDiagramById_UserWithoutAccess_ThrowsForbidden()
    {
        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Use Case 1",
            ModelJson = ValidUseCaseJson,
            CreatedBy = Guid.NewGuid()
        };
        await _diagramRepository.AddAsync(diagram);
        await _diagramRepository.SaveChangesAsync();

        var act = () => _service.GetDiagramByIdAsync(diagram.Id);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-11");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public async Task CreateDiagram_InvalidModelJson_ThrowsBadRequest()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var request = new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Invalid JSON",
            ModelJson = "{ invalid json content }"
        };

        var act = () => _service.CreateDiagramAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-12");
        ex.Which.Status.Should().Be(400);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public async Task CreateDiagram_ModelJsonExceeds256KB_ThrowsBadRequest()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var hugeJson = "{\"title\": \"" + new string('A', 260 * 1024) + "\"}";

        var request = new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Huge Diagram",
            ModelJson = hugeJson
        };

        var act = () => _service.CreateDiagramAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-12");
        ex.Which.Status.Should().Be(400);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task UpdateDiagram_StatusIsPending_ThrowsConflict()
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
            Title = "Pending Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Pending,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateDiagramRequest
        {
            Title = "Updated Title",
            ModelJson = ValidUseCaseJson
        };

        var act = () => _service.UpdateDiagramAsync(diagram.Id, request, ifMatch: "\"\"");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-13");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task UpdateDiagram_StatusIsRendering_ThrowsConflict()
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
            Title = "Rendering Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Rendering,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateDiagramRequest
        {
            Title = "Updated Title",
            ModelJson = ValidUseCaseJson
        };

        var act = () => _service.UpdateDiagramAsync(diagram.Id, request, ifMatch: "\"\"");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-13");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-15")]
    public async Task UpdateDiagram_ValidUpdate_IncrementsVersionAndSavesSnapshot()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Leader
        });
        await _memberAccessRepository.SaveChangesAsync();

        // 1. Tạo mới -> version 1
        var createRequest = new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Initial Title",
            ModelJson = ValidUseCaseJson
        };
        var created = await _service.CreateDiagramAsync(createRequest);
        created.CurrentVersion.Should().Be(1);

        // Kiểm tra snapshot version 1
        var v1 = await _versionRepository.GetByVersionAsync(created.Id, 1);
        v1.Should().NotBeNull();
        v1!.Title.Should().Be("Initial Title");

        // 2. Update -> version 2
        var updateRequest = new UpdateDiagramRequest
        {
            Title = "Updated Title v2",
            ModelJson = ValidUseCaseJson
        };
        var updated = await _service.UpdateDiagramAsync(created.Id, updateRequest, ifMatch: $"\"{created.ETag}\"");

        // Assert
        updated.CurrentVersion.Should().Be(2);
        updated.Title.Should().Be("Updated Title v2");

        var v2 = await _versionRepository.GetByVersionAsync(created.Id, 2);
        v2.Should().NotBeNull();
        v2!.Title.Should().Be("Updated Title v2");

        var count = await _versionRepository.CountVersionsAsync(created.Id);
        count.Should().Be(2);
    }

    [Fact]
    [Trait("BR", "BR-16")]
    public async Task UpdateDiagram_MissingIfMatch_ThrowsConflict()
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
            Title = "Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Draft,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateDiagramRequest { Title = "New Title", ModelJson = ValidUseCaseJson };

        // Act: Gọi không truyền If-Match
        var act = () => _service.UpdateDiagramAsync(diagram.Id, request, ifMatch: null);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-16");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-16")]
    public async Task UpdateDiagram_MismatchedIfMatch_ThrowsConflict()
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
            Title = "Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Draft,
            CreatedBy = _testUserId,
            RowVersion = new byte[] { 1, 2, 3 }
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateDiagramRequest { Title = "New Title", ModelJson = ValidUseCaseJson };

        // Act: ETag sai
        var act = () => _service.UpdateDiagramAsync(diagram.Id, request, ifMatch: "\"invalid-etag\"");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-16");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-17")]
    public async Task UpdateDiagram_RenderedStatus_SetsStatusToDraftAndPreservesSvg()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });

        const string existingSvg = "<svg>Old rendered diagram</svg>";
        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Rendered Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Rendered,
            SvgXml = existingSvg,
            CreatedBy = _testUserId,
            RowVersion = Array.Empty<byte>()
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateDiagramRequest { Title = "New Draft Title", ModelJson = ValidUseCaseJson };

        // Act
        var updated = await _service.UpdateDiagramAsync(diagram.Id, request, ifMatch: "\"\"");

        // Assert: Trạng thái quay về Draft, nhưng SvgXml cũ vẫn còn
        updated.Status.Should().Be(DiagramStatus.Draft);
        updated.SvgXml.Should().Be(existingSvg);
    }

    [Fact]
    [Trait("BR", "BR-32")]
    public async Task CreateDiagram_LecturerRole_ThrowsForbidden()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Lecturer
        });
        await _memberAccessRepository.SaveChangesAsync();

        var request = new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Lecturer Diagram",
            ModelJson = ValidUseCaseJson
        };

        var act = () => _service.CreateDiagramAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-32");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-32")]
    public async Task GetDiagramById_LecturerRole_Succeeds()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Lecturer
        });

        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "View for Lecturer",
            ModelJson = ValidUseCaseJson,
            CreatedBy = Guid.NewGuid()
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        var (detail, _) = await _service.GetDiagramByIdAsync(diagram.Id);

        detail.Should().NotBeNull();
        detail.Id.Should().Be(diagram.Id);
    }

    [Fact]
    public async Task GetVersions_ReturnsPagedHistory()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });

        var diagramId = Guid.NewGuid();
        var diagram = new Repositories.Entities.Diagram
        {
            Id = diagramId,
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Multi-version Diagram",
            ModelJson = ValidUseCaseJson,
            CurrentVersion = 3,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);

        await _versionRepository.AddAsync(new DiagramVersion { DiagramId = diagramId, Version = 1, Title = "v1", ModelJson = "{}" });
        await _versionRepository.AddAsync(new DiagramVersion { DiagramId = diagramId, Version = 2, Title = "v2", ModelJson = "{}" });
        await _versionRepository.AddAsync(new DiagramVersion { DiagramId = diagramId, Version = 3, Title = "v3", ModelJson = "{}" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetVersionsAsync(diagramId, page: 1, pageSize: 10);

        // Assert
        result.TotalItems.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.First().Version.Should().Be(3); // sắp xếp giảm dần theo version
    }

    [Fact]
    public async Task DeleteDiagram_ValidUser_SoftDeletesDiagram()
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
            Title = "To be deleted",
            ModelJson = ValidUseCaseJson,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        await _service.DeleteDiagramAsync(diagram.Id);

        var inDb = await _dbContext.Diagrams.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == diagram.Id);
        inDb.Should().NotBeNull();
        inDb!.IsDeleted.Should().BeTrue();

        var notFound = await _diagramRepository.GetByIdAsync(diagram.Id);
        notFound.Should().BeNull();
    }

    [Fact]
    [Trait("BR", "BR-14")]
    public async Task RequestRender_WhenStatusIsPending_ThrowsConflict()
    {
        // Arrange
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
            Title = "Pending Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Pending,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _service.RequestRenderAsync(diagram.Id);

        // Assert (BR-14: Không render trùng khi đang Pending/Rendering)
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-14");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-14")]
    public async Task RequestRender_WhenStatusIsRendering_ThrowsConflict()
    {
        // Arrange
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
            Title = "Rendering Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Rendering,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _service.RequestRenderAsync(diagram.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-14");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("BR", "BR-14")]
    public async Task RequestRender_WhenStatusIsDraft_TransitionsToPending_And_EnqueuesOutboxMessage()
    {
        // Arrange
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
            Title = "Draft Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Draft,
            CurrentVersion = 2,
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RequestRenderAsync(diagram.Id);

        // Assert
        result.DiagramId.Should().Be(diagram.Id);
        result.Version.Should().Be(2);
        result.Status.Should().Be(DiagramStatus.Pending);

        var updatedDiagram = await _diagramRepository.GetByIdAsync(diagram.Id);
        updatedDiagram!.Status.Should().Be(DiagramStatus.Pending);

        var outboxMessages = await _dbContext.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle();
        var outbox = outboxMessages.First();
        outbox.Type.Should().Be("RenderRequested");
        outbox.Stream.Should().Be("diagram.events");
        outbox.Envelope.Should().Contain("RenderRequested");
        outbox.Envelope.Should().Contain(diagram.Id.ToString());
    }

    [Fact]
    [Trait("BR", "BR-14")]
    public async Task RequestRender_WhenStatusIsRendered_ThrowsConflictAndKeepsDiagram()
    {
        // Arrange
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
            Title = "Already Rendered Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Rendered,
            CurrentVersion = 1,
            SvgXml = "<svg>existing</svg>",
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        // Act: render lại cùng phiên bản đã Rendered (R16: Render sẽ bỏ qua theo BR-26 → phải chặn từ Diagram)
        var act = () => _service.RequestRenderAsync(diagram.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-14");
        ex.Which.Status.Should().Be(409);

        var unchanged = await _diagramRepository.GetByIdAsync(diagram.Id);
        unchanged!.Status.Should().Be(DiagramStatus.Rendered);
        unchanged.SvgXml.Should().Be("<svg>existing</svg>");
        (await _dbContext.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task RequestRender_UserWithoutAccess_ThrowsForbidden()
    {
        // Arrange: Không cấp quyền MemberAccess
        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Protected Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Draft,
            CreatedBy = Guid.NewGuid()
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _service.RequestRenderAsync(diagram.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-11");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-32")]
    public async Task RequestRender_LecturerRole_ThrowsForbidden()
    {
        // Arrange: Cấp quyền Lecturer cho user
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Lecturer
        });

        var diagram = new Repositories.Entities.Diagram
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Diagram Viewed By Lecturer",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Draft,
            CreatedBy = Guid.NewGuid()
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _service.RequestRenderAsync(diagram.Id);

        // Assert (BR-32: Lecturer chỉ đọc, không trigger render)
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-32");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("BR", "BR-14")]
    public async Task RequestRender_WhenStatusIsFailed_ThrowsConflictAndKeepsError()
    {
        // Arrange
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
            Title = "Failed Diagram",
            ModelJson = ValidUseCaseJson,
            Status = DiagramStatus.Failed,
            CurrentVersion = 1,
            LastError = "PlantUML compilation error",
            CreatedBy = _testUserId
        };
        await _diagramRepository.AddAsync(diagram);
        await _dbContext.SaveChangesAsync();

        // Act: render lại phiên bản đã Failed mà không sửa (muốn render lại phải tạo phiên bản mới hoặc Admin Retry)
        var act = () => _service.RequestRenderAsync(diagram.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-14");
        ex.Which.Status.Should().Be(409);

        var unchanged = await _diagramRepository.GetByIdAsync(diagram.Id);
        unchanged!.Status.Should().Be(DiagramStatus.Failed);
        unchanged.LastError.Should().Be("PlantUML compilation error");
        (await _dbContext.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestRender_DiagramNotFound_ThrowsNotFound()
    {
        // Act
        var act = () => _service.RequestRenderAsync(Guid.NewGuid());

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("NOT_FOUND");
        ex.Which.Status.Should().Be(404);
    }

    [Fact]
    [Trait("BR", "BR-31")]
    public async Task UpdateDiagram_ProjectHasApprovedReview_ReturnsNeedsResubmitTrue()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original Title",
            ModelJson = ValidUseCaseJson
        });

        // Add Approved Review for the project
        await _reviewRepository.AddAsync(new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Status = ReviewStatus.Approved,
            SubmittedBy = _testUserId,
            SubmittedAt = DateTime.UtcNow
        });
        await _reviewRepository.SaveChangesAsync();

        // Act
        var updateRequest = new UpdateDiagramRequest
        {
            Title = "Updated Title",
            ModelJson = ValidUseCaseJson
        };
        var updated = await _service.UpdateDiagramAsync(created.Id, updateRequest, ifMatch: $"\"{created.ETag}\"");

        // Assert (BR-31: needsResubmit is true)
        Assert.True(updated.NeedsResubmit);
    }

    [Fact]
    [Trait("BR", "BR-31")]
    public async Task UpdateDiagram_ProjectHasNoApprovedReview_ReturnsNeedsResubmitFalse()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original Title",
            ModelJson = ValidUseCaseJson
        });

        // Act
        var updateRequest = new UpdateDiagramRequest
        {
            Title = "Updated Title",
            ModelJson = ValidUseCaseJson
        };
        var updated = await _service.UpdateDiagramAsync(created.Id, updateRequest, ifMatch: $"\"{created.ETag}\"");

        // Assert (BR-31: needsResubmit is false)
        Assert.False(updated.NeedsResubmit);
    }

    [Fact]
    [Trait("BR", "BR-15")]
    public async Task RestoreVersion_ValidVersion_RestoresSnapshotAndIncrementsVersion()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original V1 Title",
            ModelJson = ValidUseCaseJson
        });

        // Update to V2
        var updatedV2 = await _service.UpdateDiagramAsync(created.Id, new UpdateDiagramRequest
        {
            Title = "Updated V2 Title",
            ModelJson = ValidUseCaseJson
        }, ifMatch: $"\"{created.ETag}\"");

        Assert.Equal(2, updatedV2.CurrentVersion);
        Assert.Equal("Updated V2 Title", updatedV2.Title);

        // Act: Restore to V1
        var restored = await _service.RestoreVersionAsync(created.Id, 1);

        // Assert: Becomes V3 with content of V1, status Draft
        Assert.Equal(3, restored.CurrentVersion);
        Assert.Equal("Original V1 Title", restored.Title);
        Assert.Equal(DiagramStatus.Draft, restored.Status);

        var v3 = await _versionRepository.GetByVersionAsync(created.Id, 3);
        Assert.NotNull(v3);
        Assert.Equal("Original V1 Title", v3.Title);
    }

    [Fact]
    [Trait("BR", "BR-32")]
    public async Task RestoreVersion_Lecturer_Throws403()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });

        var lecturerId = Guid.NewGuid();
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = lecturerId,
            Role = ProjectRole.Lecturer
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original V1",
            ModelJson = ValidUseCaseJson
        });

        _currentUserMock.Setup(u => u.UserId).Returns(lecturerId);
        _currentUserMock.Setup(u => u.Role).Returns("Lecturer");

        // Act & Assert (BR-32)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RestoreVersionAsync(created.Id, 1));
        Assert.Equal(403, ex.Status);
        Assert.Equal("BR-32", ex.Code);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task RestoreVersion_PendingStatus_Throws409()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original V1",
            ModelJson = ValidUseCaseJson
        });

        // Set status to Pending
        var entity = await _diagramRepository.GetByIdAsync(created.Id);
        entity!.Status = DiagramStatus.Pending;
        _diagramRepository.Update(entity);
        await _diagramRepository.SaveChangesAsync();

        // Act & Assert (BR-13)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RestoreVersionAsync(created.Id, 1));
        Assert.Equal(409, ex.Status);
        Assert.Equal("BR-13", ex.Code);
    }

    [Fact]
    [Trait("BR", "BR-16")]
    public async Task UpdateDiagram_DbUpdateConcurrencyException_Throws409BR16()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original V1",
            ModelJson = ValidUseCaseJson
        });

        var diagramMock = new Mock<IDiagramRepository>();
        diagramMock.Setup(r => r.GetByIdAsync(created.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(await _diagramRepository.GetByIdAsync(created.Id));
        diagramMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        var service = new DiagramService(
            diagramMock.Object,
            _versionRepository,
            _memberAccessRepository,
            _currentUserMock.Object,
            _outbox,
            _reviewRepository,
            _engineClientMock.Object);

        var updateRequest = new UpdateDiagramRequest
        {
            Title = "Updated Title",
            ModelJson = ValidUseCaseJson
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateDiagramAsync(created.Id, updateRequest, created.ETag));
        Assert.Equal(409, ex.Status);
        Assert.Equal("BR-16", ex.Code);
    }

    [Fact]
    [Trait("BR", "BR-31")]
    public async Task RestoreVersion_ProjectWithApprovedReview_ReturnsNeedsResubmitTrue()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Leader
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original V1",
            ModelJson = ValidUseCaseJson
        });

        // Add Approved Review for this project
        await _reviewRepository.AddAsync(new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Status = ReviewStatus.Approved,
            SubmittedBy = _testUserId,
            SubmittedAt = DateTime.UtcNow,
            SnapshotJson = "[]"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.RestoreVersionAsync(created.Id, 1);

        // Assert
        Assert.True(result.NeedsResubmit);
    }

    [Fact]
    [Trait("BR", "BR-18")]
    public async Task CreateDiagram_DiagramEngineReturnsWarnings_IncludesWarningsInResponse()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        _engineClientMock.Setup(c => c.ValidateDiagramAsync(It.IsAny<DiagramType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagramValidationResult
            {
                IsValid = true,
                Warnings = new List<string> { "Consider adding more actors." }
            });

        // Act
        var result = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "With Warnings",
            ModelJson = ValidUseCaseJson
        });

        // Assert
        Assert.Single(result.Warnings);
        Assert.Equal("Consider adding more actors.", result.Warnings[0]);
    }

    [Fact]
    public async Task ValidateDiagram_WithDraftModelJson_ValidatesDraftJson()
    {
        // Arrange
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _testUserId,
            Role = ProjectRole.Member
        });
        await _memberAccessRepository.SaveChangesAsync();

        var created = await _service.CreateDiagramAsync(new CreateDiagramRequest
        {
            ProjectId = _testProjectId,
            Type = DiagramType.UseCase,
            Title = "Original",
            ModelJson = ValidUseCaseJson
        });

        var draftJson = @"{
  ""systemBoundary"": ""DraftSystem"",
  ""actors"": [""Student""],
  ""useCases"": [""DraftUC""],
  ""relations"": [{ ""from"": ""Student"", ""to"": ""DraftUC"", ""type"": ""association"" }]
}";
        string? validatedJson = null;
        _engineClientMock.Setup(c => c.ValidateDiagramAsync(It.IsAny<DiagramType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DiagramType, string, CancellationToken>((_, json, _) => validatedJson = json)
            .ReturnsAsync(new DiagramValidationResult { IsValid = true });

        // Act
        var result = await _service.ValidateDiagramAsync(created.Id, draftJson);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(draftJson, validatedJson);
    }
}
