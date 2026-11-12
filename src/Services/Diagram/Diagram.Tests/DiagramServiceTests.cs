using System.Text;
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

public class DiagramServiceTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly DiagramRepository _diagramRepository;
    private readonly DiagramVersionRepository _versionRepository;
    private readonly MemberAccessRepository _memberAccessRepository;
    private readonly OutboxRepository _outboxRepository;
    private readonly ReviewRepository _reviewRepository;
    private readonly Mock<ICurrentUser> _currentUserMock;
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
        _outboxRepository = new OutboxRepository(_dbContext);
        _reviewRepository = new ReviewRepository(_dbContext);
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.UserId).Returns(_testUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Student");
        _currentUserMock.Setup(u => u.Username).Returns("student1");

        _service = new DiagramService(_diagramRepository, _versionRepository, _memberAccessRepository, _currentUserMock.Object, _outboxRepository, _reviewRepository);
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
    public async Task RequestRender_WhenStatusIsRendered_TransitionsToPending_And_EnqueuesOutboxMessage()
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

        // Act
        var result = await _service.RequestRenderAsync(diagram.Id);

        // Assert
        result.Status.Should().Be(DiagramStatus.Pending);

        var updatedDiagram = await _diagramRepository.GetByIdAsync(diagram.Id);
        updatedDiagram!.Status.Should().Be(DiagramStatus.Pending);
        updatedDiagram.SvgXml.Should().Be("<svg>existing</svg>"); // giữ SVG cũ trong khi render lại
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
    public async Task RequestRender_WhenStatusIsFailed_TransitionsToPending_And_EnqueuesOutboxMessage()
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

        // Act
        var result = await _service.RequestRenderAsync(diagram.Id);

        // Assert (BR-14: Failed -> Pending)
        result.Status.Should().Be(DiagramStatus.Pending);

        var updatedDiagram = await _diagramRepository.GetByIdAsync(diagram.Id);
        updatedDiagram!.Status.Should().Be(DiagramStatus.Pending);

        var outboxMessages = await _dbContext.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle();
        var outbox = outboxMessages.First();
        outbox.Type.Should().Be("RenderRequested");
        outbox.Stream.Should().Be("diagram.events");
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
}
