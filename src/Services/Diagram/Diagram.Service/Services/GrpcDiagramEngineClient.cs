using System.Diagnostics;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using UMLForge.Contracts.Grpc;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Validation;
using ModelRelationSuggestion = UMLForge.Diagram.Service.Models.Validation.RelationSuggestion;

namespace UMLForge.Diagram.Service.Services;

public sealed class GrpcDiagramEngineClient : IDiagramEngineClient
{
    private const int MaxUnavailableRetries = 2;
    private static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(5);

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
        return await ExecuteWithRetryAsync(async headers =>
        {
            var request = new DiagramValidationRequest
            {
                DiagramType = (int)diagramType,
                ModelJson = modelJson ?? string.Empty
            };

            using var call = _client.ValidateDiagramAsync(
                request,
                headers: headers,
                deadline: DateTime.UtcNow.Add(DefaultDeadline),
                cancellationToken: cancellationToken);

            var response = await call.ResponseAsync;
            return new DiagramValidationResult
            {
                IsValid = response.IsValid,
                Errors = response.Errors.ToList(),
                Warnings = response.Warnings.ToList()
            };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ModelRelationSuggestion>> SuggestRelationsAsync(
        DiagramType diagramType,
        string modelJson,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async headers =>
        {
            var request = new RelationSuggestionRequest
            {
                DiagramType = (int)diagramType,
                ModelJson = modelJson ?? string.Empty
            };

            using var call = _client.SuggestRelationsAsync(
                request,
                headers: headers,
                deadline: DateTime.UtcNow.Add(DefaultDeadline),
                cancellationToken: cancellationToken);

            var response = await call.ResponseAsync;
            return (IReadOnlyList<ModelRelationSuggestion>)response.Suggestions.Select(s => new ModelRelationSuggestion
            {
                From = s.From,
                To = s.To,
                Description = s.Description
            }).ToList();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectDiagramValidationResult>> ValidateProjectAsync(
        IEnumerable<(Guid DiagramId, DiagramType Type, string ModelJson)> diagrams,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async headers =>
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
                headers: headers,
                deadline: DateTime.UtcNow.Add(DefaultDeadline),
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
            return (IReadOnlyList<ProjectDiagramValidationResult>)results;
        }, cancellationToken);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Metadata, Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxUnavailableRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var headers = CreateHeaders();
                return await action(headers);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable && attempt < MaxUnavailableRetries)
            {
                _logger.LogWarning(ex, "DiagramEngine gRPC Unavailable on attempt {Attempt}/{MaxRetries}. Retrying...", attempt + 1, MaxUnavailableRetries);
                await Task.Delay(100 * (1 << attempt), cancellationToken);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("DiagramEngine gRPC call was cancelled.");
                throw;
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            {
                _logger.LogWarning(ex, "DiagramEngine gRPC service call failed with {StatusCode} after {Attempt} attempts.", ex.StatusCode, attempt + 1);
                throw new BusinessRuleException(
                    "BR-503",
                    "Dịch vụ kiểm tra UML tạm thời không phản hồi. Vui lòng thử lại sau.",
                    503);
            }
        }

        throw new BusinessRuleException(
            "BR-503",
            "Dịch vụ kiểm tra UML tạm thời không phản hồi. Vui lòng thử lại sau.",
            503);
    }

    private static Metadata CreateHeaders()
    {
        var headers = new Metadata();
        var correlation = CorrelationContext.Current?.ToString() ?? Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(correlation))
        {
            headers.Add(CorrelationKeys.GrpcMetadata, correlation);
        }
        return headers;
    }
}
