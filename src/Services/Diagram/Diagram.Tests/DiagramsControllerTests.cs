using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UMLForge.Diagram.API.Controllers;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.Tests;

public class DiagramsControllerTests
{
    private readonly Mock<IDiagramService> _diagramServiceMock;
    private readonly Mock<ICommentService> _commentServiceMock;
    private readonly Mock<IDiagramExportService> _exportServiceMock;
    private readonly DiagramsController _controller;

    public DiagramsControllerTests()
    {
        _diagramServiceMock = new Mock<IDiagramService>();
        _commentServiceMock = new Mock<ICommentService>();
        _exportServiceMock = new Mock<IDiagramExportService>();
        _controller = new DiagramsController(
            _diagramServiceMock.Object,
            _commentServiceMock.Object,
            _exportServiceMock.Object);
    }

    [Fact]
    [Trait("API", "RequestRender")]
    public async Task RequestRender_ReturnsAcceptedResult_WithRenderResponse()
    {
        // Arrange
        var diagramId = Guid.NewGuid();
        var expectedResponse = new RenderResponse
        {
            DiagramId = diagramId,
            Version = 1,
            Status = DiagramStatus.Pending,
            Message = "Render request accepted."
        };

        _diagramServiceMock
            .Setup(s => s.RequestRenderAsync(diagramId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var actionResult = await _controller.RequestRender(diagramId, CancellationToken.None);

        // Assert
        actionResult.Should().BeOfType<AcceptedResult>();
        var acceptedResult = actionResult as AcceptedResult;
        acceptedResult!.StatusCode.Should().Be(202);
        acceptedResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    [Trait("API", "RequestRender")]
    public async Task RequestRender_WhenServiceThrowsBR14_PropagatesException()
    {
        // Arrange
        var diagramId = Guid.NewGuid();
        _diagramServiceMock
            .Setup(s => s.RequestRenderAsync(diagramId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("BR-14", "Cannot trigger render while diagram is pending or rendering.", 409));

        // Act
        var act = () => _controller.RequestRender(diagramId, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-14");
        ex.Which.Status.Should().Be(409);
    }

    [Fact]
    [Trait("API", "RequestRender")]
    public async Task RequestRender_WhenServiceThrowsBR11_PropagatesException()
    {
        // Arrange
        var diagramId = Guid.NewGuid();
        _diagramServiceMock
            .Setup(s => s.RequestRenderAsync(diagramId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("BR-11", "You do not have access to this project.", 403));

        // Act
        var act = () => _controller.RequestRender(diagramId, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-11");
        ex.Which.Status.Should().Be(403);
    }

    [Fact]
    [Trait("API", "RequestRender")]
    public async Task RequestRender_WhenServiceThrowsBR32_PropagatesException()
    {
        // Arrange
        var diagramId = Guid.NewGuid();
        _diagramServiceMock
            .Setup(s => s.RequestRenderAsync(diagramId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("BR-32", "Lecturers have read-only access to diagrams.", 403));

        // Act
        var act = () => _controller.RequestRender(diagramId, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BR-32");
        ex.Which.Status.Should().Be(403);
    }
}
