using Grpc.Core;
using Microsoft.Extensions.Logging;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Grpc;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Validation;
using RelationSuggestion = UMLForge.Diagram.Service.Models.Validation.RelationSuggestion;
using ModelRelationSuggestion = UMLForge.Diagram.Service.Models.Validation.RelationSuggestion;

namespace UMLForge.Diagram.Service.Services;

public sealed class GrpcDiagramEngineClient : IDiagramEngineClient
{
    private readonly DiagramEngineService.DiagramEngineServiceClient _client;
    private readonly ILogger<GrpcDiagramEngineClient> _logger;

    public GrpcDiagramEngineClient(
        DiagramEngineService.DiagramEngineServiceClient client,
        ILogger<GrpcDiagramEngineClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<DiagramValidationResult> ValidateDiagramAsync(
        DiagramType diagramType,
        string modelJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DiagramValidationRequest
            {
                DiagramType = (int)diagramType,
                ModelJson = modelJson ?? string.Empty
            };

            using var call = _client.ValidateDiagramAsync(
                request,
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: cancellationToken);

            var response = await call.ResponseAsync;
            return new DiagramValidationResult
            {
                IsValid = response.IsValid,
                Errors = response.Errors.ToList(),
                Warnings = response.Warnings.ToList()
            };
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "DiagramEngine gRPC service is unavailable or deadline exceeded.");
            throw new BusinessRuleException(
                "BR-503",
                "Dịch vụ kiểm tra UML tạm thời không phản hồi. Vui lòng thử lại sau.",
                503);
        }
    }

    public async Task<IReadOnlyList<ModelRelationSuggestion>> SuggestRelationsAsync(
        DiagramType diagramType,
        string modelJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new RelationSuggestionRequest
            {
                DiagramType = (int)diagramType,
                ModelJson = modelJson ?? string.Empty
            };

            using var call = _client.SuggestRelationsAsync(
                request,
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: cancellationToken);

            var response = await call.ResponseAsync;
            return response.Suggestions.Select(s => new ModelRelationSuggestion
            {
                From = s.From,
                To = s.To,
                Description = s.Description
            }).ToList();
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "DiagramEngine gRPC service is unavailable or deadline exceeded.");
            throw new BusinessRuleException(
                "BR-503",
                "Dịch vụ kiểm tra UML tạm thời không phản hồi. Vui lòng thử lại sau.",
                503);
        }
    }

    public async Task<IReadOnlyList<ProjectDiagramValidationResult>> ValidateProjectAsync(
        IEnumerable<(Guid DiagramId, DiagramType Type, string ModelJson)> diagrams,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ProjectValidationRequest();
            foreach (var (diagramId, type, modelJson) in diagrams)
            {
                request.Diagrams.Add(new ProjectDiagramItem
                {
                    DiagramId = diagramId.ToString(),
                    DiagramType = (int)type,
                    ModelJson = modelJson ?? string.Empty
                });
            }

            using var call = _client.ValidateProject(
                request,
                deadline: DateTime.UtcNow.AddSeconds(10),
                cancellationToken: cancellationToken);

            var results = new List<ProjectDiagramValidationResult>();
            while (await call.ResponseStream.MoveNext(cancellationToken))
            {
                var current = call.ResponseStream.Current;
                if (Guid.TryParse(current.DiagramId, out var parsedId))
                {
                    results.Add(new ProjectDiagramValidationResult
                    {
                        DiagramId = parsedId,
                        IsValid = current.Result.IsValid,
                        Errors = current.Result.Errors.ToList(),
                        Warnings = current.Result.Warnings.ToList()
                    });
                }
            }
            return results;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "DiagramEngine gRPC service is unavailable or deadline exceeded.");
            throw new BusinessRuleException(
                "BR-503",
                "Dịch vụ kiểm tra UML tạm thời không phản hồi. Vui lòng thử lại sau.",
                503);
        }
    }
}
