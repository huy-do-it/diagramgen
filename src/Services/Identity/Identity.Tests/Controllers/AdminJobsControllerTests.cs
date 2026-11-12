using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Identity.API.Controllers;
using Identity.Service.Jobs;

namespace Identity.Tests.Controllers;

public class AdminJobsControllerTests
{
    private readonly Mock<IScheduledJob> _purgeTokensJobMock;
    private readonly Mock<ILogger<AdminJobsController>> _loggerMock;
    private readonly AdminJobsController _controller;

    public AdminJobsControllerTests()
    {
        _purgeTokensJobMock = new Mock<IScheduledJob>();
        _purgeTokensJobMock.Setup(j => j.JobName).Returns("purge-tokens");
        _purgeTokensJobMock.Setup(j => j.ExecuteDirectlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Purged 5 expired tokens.");

        _loggerMock = new Mock<ILogger<AdminJobsController>>();

        var jobs = new List<IScheduledJob> { _purgeTokensJobMock.Object };
        _controller = new AdminJobsController(jobs, _loggerMock.Object);
    }

    [Fact]
    public async Task RunPurgeTokens_WhenJobExists_ReturnsOkWithSummaryMessage()
    {
        // Act
        var result = await _controller.RunPurgeTokens(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);

        _purgeTokensJobMock.Verify(j => j.ExecuteDirectlyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunJob_WhenJobExists_ReturnsOkWithSummaryMessage()
    {
        // Act
        var result = await _controller.RunJob("purge-tokens", CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task RunJob_WhenJobDoesNotExist_ReturnsNotFound()
    {
        // Act
        var result = await _controller.RunJob("non_existent_job", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult!.StatusCode.Should().Be(404);
    }
}
