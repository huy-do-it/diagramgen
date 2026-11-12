#if HAS_GRPC_CONTRACT
using System.Diagnostics;
using Grpc.Core;
using UMLForge.Contracts.Grpc;
using UMLForge.Render.Service.Interfaces;

namespace UMLForge.Render.Service.Services;

public sealed class GrpcDiagramEngineClient(DiagramEngineService.DiagramEngineServiceClient client) : IDiagramEngineClient
{
    public async Task<bool> ValidateAsync(int diagramType, string modelJson, CancellationToken cancellationToken = default)
    {
        using var call = client.ValidateDiagramAsync(new DiagramValidationRequest
        {
            DiagramType = diagramType, ModelJson = modelJson
        }, headers: CreateHeaders(), deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: cancellationToken);
        return (await call.ResponseAsync).IsValid;
    }

    public async Task<string> GeneratePlantUmlAsync(int diagramType, string modelJson, string title, CancellationToken cancellationToken = default)
    {
        using var call = client.GeneratePlantUmlAsync(new GeneratePlantUmlRequest
        {
            DiagramType = diagramType, ModelJson = modelJson, Title = title
        }, headers: CreateHeaders(), deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: cancellationToken);
        return (await call.ResponseAsync).PlantUml;
    }

    private static Metadata CreateHeaders()
    {
        var headers = new Metadata();
        if (Activity.Current is { } activity)
            headers.Add("x-correlation-id", activity.TraceId.ToString());
        return headers;
    }
}
#endif
