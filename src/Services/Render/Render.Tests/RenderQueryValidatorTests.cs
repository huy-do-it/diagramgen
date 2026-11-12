using UMLForge.Render.Service.Models;
namespace UMLForge.Render.Tests;
public sealed class RenderQueryValidatorTests
{
    [Theory]
    [InlineData(0,20)] [InlineData(1,0)] [InlineData(1,101)] [InlineData(int.MaxValue,100)]
    public void Validate_InvalidPaging_Rejects(int page,int size) => Assert.False(new RenderQueryValidator().Validate(new RenderQuery{Page=page,PageSize=size}).IsValid);
    [Theory]
    [InlineData("Queued",true)] [InlineData("Failed",true)] [InlineData("Running",true)] [InlineData("Done",true)] [InlineData("1",false)] [InlineData("unknown",false)]
    public void Validate_Status_UsesNamedValues(string status,bool valid) => Assert.Equal(valid,new RenderQueryValidator().Validate(new RenderQuery{Status=status}).IsValid);
    [Fact]
    public async Task JobsAsync_InvalidPaging_ReturnsBusiness400BeforeSql()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<UMLForge.Render.Repositories.RenderDbContext>();
        Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions.UseSqlServer(options, "Server=unused;Database=unused;Integrated Security=True");
        await using var db = new UMLForge.Render.Repositories.RenderDbContext(options.Options);
        var service = new UMLForge.Render.Service.Services.RenderOperationsService(new UMLForge.Render.Repositories.Repositories.RenderOperationsRepository(db),
            new Moq.Mock<UMLForge.BuildingBlocks.EventBus.IStreamMaintenance>(Moq.MockBehavior.Strict).Object,
            new Moq.Mock<UMLForge.BuildingBlocks.Outbox.IOutbox>(Moq.MockBehavior.Strict).Object, TimeProvider.System, new RenderQueryValidator());
        var error = await Assert.ThrowsAsync<UMLForge.BuildingBlocks.Web.BusinessRuleException>(() => service.JobsAsync(new RenderQuery { Page = 0 }, default));
        Assert.Equal(400, error.Status);
        Assert.NotNull(error.Errors);
    }
    [Fact]
    public void Validate_ReversedDates_Rejects() => Assert.False(new RenderQueryValidator().Validate(new RenderQuery{From=DateTime.UtcNow,To=DateTime.UtcNow.AddDays(-1)}).IsValid);
}
