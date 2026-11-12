using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UMLForge.Render.Service;
using UMLForge.Render.Service.Interfaces;
using UMLForge.Render.Service.Models;
using UMLForge.Render.Service.Services;

namespace UMLForge.Render.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddRenderServices_FakeOutsideDevelopment_RejectsStartup()
        => Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddRenderServices(Config(), new EnvironmentStub("Production")));

    [Fact]
    public void AddRenderServices_MissingConnection_RejectsStartup()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddRenderServices(config, new EnvironmentStub("Development")));
    }

    [Fact]
    public void AddRenderServices_ExplicitDevelopmentFake_ResolvesPipeline()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRenderServices(Config(), new EnvironmentStub("Development"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.IsType<FakeDiagramEngineClient>(scope.ServiceProvider.GetRequiredService<IDiagramEngineClient>());
        Assert.IsType<RenderPipeline>(scope.ServiceProvider.GetRequiredService<IRenderPipeline>());
    }

    [Theory]
    [InlineData(0)] [InlineData(31)]
    public void AddRenderServices_InvalidBudget_RejectsOptions(int seconds)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRenderServices(Config(new() { ["Render:TimeoutSeconds"] = seconds.ToString() }), new EnvironmentStub("Development"));
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RenderOptions>>().Value);
    }

    [Fact]
    public void AddRenderServices_UnsupportedRendererUrl_RejectsOptions()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRenderServices(Config(new() { ["PlantUml:BaseUrl"] = "file:///tmp/svg" }), new EnvironmentStub("Development"));
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PlantUmlOptions>>().Value);
    }

    private static IConfiguration Config(Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:RenderDb"] = "Server=localhost;Database=RenderDb;Integrated Security=True",
            ["DiagramEngine:UseFake"] = "true"
        };
        if (extra is not null) foreach (var value in extra) values[value.Key] = value.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class EnvironmentStub(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Render.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
