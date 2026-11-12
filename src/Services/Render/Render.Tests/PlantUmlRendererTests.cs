using System.Net;
using System.Text;
using UMLForge.Render.Service.Services;

namespace UMLForge.Render.Tests;

public sealed class PlantUmlRendererTests
{
    [Fact]
    public async Task RenderSvgAsync_ValidResponse_PostsPlainTextAndReturnsSvg()
    {
        var handler = new StubHttpHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://renderer/svg", request.RequestUri!.AbsoluteUri);
            Assert.Equal("text/plain", request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal("@startuml\n@enduml", await request.Content.ReadAsStringAsync(ct));
            return Fixtures.SvgResponse();
        });
        using var http = Fixtures.Http(handler);
        Assert.Equal(Fixtures.Svg, await new PlantUmlRenderer(http).RenderSvgAsync("@startuml\n@enduml"));
    }

    [Theory]
    [InlineData("<svg/>")]
    [InlineData("<html/>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\">")]
    [InlineData("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg xmlns=\"http://www.w3.org/2000/svg\">&x;</svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><foreignObject/></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><a href=\"javascript:alert(1)\"/></svg>")]
    public async Task RenderSvgAsync_InvalidOrActiveSvg_RejectsResponse(string svg)
    {
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse(svg))));
        await Assert.ThrowsAsync<InvalidDataException>(() => new PlantUmlRenderer(http).RenderSvgAsync("code"));
    }

    [Theory]
    [InlineData("X-PlantUML-Diagram-Error", "error")]
    [InlineData("X-PlantUML-Diagram-Description", "(Error)")]
    public async Task RenderSvgAsync_PlantUmlErrorHeader_RejectsErrorImage(string header, string value)
    {
        var response = Fixtures.SvgResponse(); response.Headers.Add(header, value);
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(response)));
        await Assert.ThrowsAsync<InvalidDataException>(() => new PlantUmlRenderer(http).RenderSvgAsync("code"));
    }

    [Fact]
    public async Task RenderSvgAsync_WrongMediaType_RejectsResponse()
    {
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(Fixtures.Svg, Encoding.UTF8, "text/html") })));
        await Assert.ThrowsAsync<InvalidDataException>(() => new PlantUmlRenderer(http).RenderSvgAsync("code"));
    }

    [Fact]
    public async Task RenderSvgAsync_OversizedBody_RejectsResponse()
    {
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(Fixtures.SvgResponse(new string('x', PlantUmlRenderer.MaxSvgBytes + 1)))));
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlantUmlRenderer(http).RenderSvgAsync("code"));
    }

    [Fact]
    public async Task RenderSvgAsync_NonSuccess_ThrowsHttpError()
    {
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlantUmlRenderer(http).RenderSvgAsync("code"));
    }

    [Fact, Trait("BR", "BR-27")]
    public async Task RenderSvgAsync_TimeoutDuringBody_ThrowsTimeout()
    {
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new SlowContent() })));
        http.Timeout = TimeSpan.FromMilliseconds(200);
        await Assert.ThrowsAsync<TimeoutException>(() => new PlantUmlRenderer(http).RenderSvgAsync("code"));
    }

    [Fact]
    public async Task RenderSvgAsync_CallerCancelsDuringBody_PropagatesCancellation()
    {
        using var http = Fixtures.Http(new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new SlowContent() })));
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PlantUmlRenderer(http).RenderSvgAsync("code", source.Token));
    }

    private sealed class SlowContent : HttpContent
    {
        public SlowContent() => Headers.ContentType = new("image/svg+xml");
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => throw new NotSupportedException();
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
