using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace UMLForge.Render.Service.Services;

public sealed class PlantUmlRenderer
{
    public const int MaxSvgBytes = 2_000_000;
    private readonly HttpClient _httpClient;

    public PlantUmlRenderer(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.MaxResponseContentBufferSize = MaxSvgBytes;
    }

    public async Task<string> RenderSvgAsync(string plantUml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plantUml);
        using var request = new HttpRequestMessage(HttpMethod.Post, "svg")
        {
            Content = new StringContent(plantUml, Encoding.UTF8, "text/plain")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/svg+xml"));
        try
        {
            // Buffer có giới hạn để timeout/cancellation bao phủ cả response body.
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Headers.Contains("X-PlantUML-Diagram-Error")
                || response.Headers.TryGetValues("X-PlantUML-Diagram-Description", out var descriptions)
                    && descriptions.Any(x => x.Contains("(Error)", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("PlantUML rejected the diagram.");

            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PlantUML did not return SVG.");

            var svg = await response.Content.ReadAsStringAsync(cancellationToken);
            using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxSvgBytes
            });
            var document = XDocument.Load(reader);
            if (document.Root?.Name != XName.Get("svg", "http://www.w3.org/2000/svg"))
                throw new InvalidDataException("Invalid SVG root.");

            if (document.Descendants().Any(x => x.Name.LocalName is "script" or "foreignObject"
                || x.Attributes().Any(a => a.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    || a.Name.LocalName == "href" && a.Value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException("SVG contains active content.");

            cancellationToken.ThrowIfCancellationRequested();
            return svg;
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("PlantUML returned invalid SVG XML.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("PlantUML rendering timed out.", ex);
        }
    }
}
