using System.Net;
using System.Text;
using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Service.Interfaces;

namespace UMLForge.Render.Tests;

internal sealed class StubEngine : IDiagramEngineClient
{
    public Func<CancellationToken, Task<bool>> Validate { get; set; } = _ => Task.FromResult(true);
    public Func<CancellationToken, Task<string>> Generate { get; set; } = _ => Task.FromResult("@startuml\nAlice -> Bob: hello\n@enduml");
    public int ValidateCalls { get; private set; }
    public int GenerateCalls { get; private set; }

    public Task<bool> ValidateAsync(int diagramType, string modelJson, CancellationToken cancellationToken = default)
    {
        ValidateCalls++;
        return Validate(cancellationToken);
    }

    public Task<string> GeneratePlantUmlAsync(int diagramType, string modelJson, string title, CancellationToken cancellationToken = default)
    {
        GenerateCalls++;
        return Generate(cancellationToken);
    }
}

internal sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return send(request, cancellationToken);
    }
}

internal static class Fixtures
{
    public const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>hello</text></svg>";
    public static RenderJob Job(int type = 3) => new()
    {
        DiagramId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), RequestedBy = Guid.NewGuid(),
        DiagramVersion = 1, DiagramType = type, Title = "Snapshot", ModelJson = "{}"
    };

    public static HttpResponseMessage SvgResponse(string svg = Svg) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(svg, Encoding.UTF8, "image/svg+xml")
    };

    public static HttpClient Http(StubHttpHandler handler) => new(handler) { BaseAddress = new Uri("http://renderer/") };
}
