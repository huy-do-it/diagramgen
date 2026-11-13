using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Moq;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Repositories.Repositories;
using UMLForge.Diagram.Service.Exceptions;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Services;
using Xunit;
using DiagramEntity = UMLForge.Diagram.Repositories.Entities.Diagram;

namespace UMLForge.Diagram.Tests;

public class DiagramExportServiceTests
{
    private readonly DiagramDbContext _dbContext;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly DiagramExportService _service;

    private readonly Guid _testProjectId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DiagramExportServiceTests()
    {
        var options = new DbContextOptionsBuilder<DiagramDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DiagramDbContext(options);
        _diagramRepository = new DiagramRepository(_dbContext);
        _memberAccessRepository = new MemberAccessRepository(_dbContext);
        _currentUserMock = new Mock<ICurrentUser>();

        _currentUserMock.Setup(u => u.UserId).Returns(_userId);
        _currentUserMock.Setup(u => u.Role).Returns("Student");
        _currentUserMock.Setup(u => u.Username).Returns("student1");

        _service = new DiagramExportService(
            _diagramRepository,
            _memberAccessRepository,
            _currentUserMock.Object);
    }

    [Fact]
    public async Task ExportProjectDiagramsZip_ValidProject_ReturnsZipWithFiles()
    {
        await _memberAccessRepository.AddAsync(new MemberAccess
        {
            ProjectId = _testProjectId,
            UserId = _userId,
            Role = ProjectRole.Member
        });

        await _diagramRepository.AddAsync(new DiagramEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Title = "Online Booking",
            Type = DiagramType.UseCase,
            Status = DiagramStatus.Rendered,
            ModelJson = "{\"actors\": []}",
            PlantUml = "@startuml\nactor Customer\n@enduml",
            SvgXml = "<svg>booking</svg>"
        });
        await _diagramRepository.SaveChangesAsync();

        var (zipBytes, fileName) = await _service.ExportProjectDiagramsZipAsync(_testProjectId);

        Assert.NotNull(zipBytes);
        Assert.True(zipBytes.Length > 0);
        Assert.Equal($"project_{_testProjectId}_diagrams.zip", fileName);

        // Verify ZIP contents
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("Online Booking.json"));
        Assert.NotNull(archive.GetEntry("Online Booking.puml"));
        Assert.NotNull(archive.GetEntry("Online Booking.svg"));
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task ExportProjectDiagramsZip_NoAccess_ThrowsForbidden()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ExportProjectDiagramsZipAsync(_testProjectId));
        Assert.Equal("BR-11", ex.Code);
        Assert.Equal(403, ex.Status);
    }
}
