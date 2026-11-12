using Grpc.Core;
using UMLForge.Contracts.Grpc;
using UMLForge.DiagramEngine.Grpc.Generators;
using UMLForge.DiagramEngine.Grpc.Models;
using UMLForge.DiagramEngine.Grpc.Validators;

namespace UMLForge.DiagramEngine.Grpc.Services;

/// <summary>
/// 4 RPC của <c>diagramengine.DiagramEngineService</c> (TASKS §1.7). Không giữ trạng thái;
/// mọi luật nằm ở <see cref="IUmlRuleValidator"/> / <see cref="IPlantUmlGenerator"/>.
/// </summary>
public sealed class DiagramEngineGrpcService : DiagramEngineService.DiagramEngineServiceBase
{
    // Giới hạn số lỗi ghép vào grpc-message để header không quá dài
    private const int MaxErrorsInStatus = 10;

    private readonly IUmlRuleValidator _validator;
    private readonly IRelationSuggester _suggester;
    private readonly IPlantUmlGenerator _generator;

    public DiagramEngineGrpcService(IUmlRuleValidator validator, IRelationSuggester suggester, IPlantUmlGenerator generator)
    {
        _validator = validator;
        _suggester = suggester;
        _generator = generator;
    }

    public override Task<DiagramValidationResponse> ValidateDiagram(DiagramValidationRequest request, ServerCallContext context)
        => Task.FromResult(_validator.Validate(request));

    public override Task<RelationSuggestionResponse> SuggestRelations(RelationSuggestionRequest request, ServerCallContext context)
        => Task.FromResult(_suggester.Suggest(request));

    /// <summary>Kiểm tra luật trước; sơ đồ sai → <see cref="StatusCode.InvalidArgument"/> kèm danh sách lỗi.</summary>
    public override Task<GeneratePlantUmlResponse> GeneratePlantUml(GeneratePlantUmlRequest request, ServerCallContext context)
    {
        var validation = _validator.Validate(new DiagramValidationRequest
        {
            DiagramType = request.DiagramType,
            ModelJson = request.ModelJson,
        });
        if (!validation.IsValid)
        {
            var errors = string.Join(" | ", validation.Errors.Take(MaxErrorsInStatus));
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Sơ đồ chưa hợp lệ: {errors}"));
        }

        var type = (DiagramType)request.DiagramType;
        var model = DiagramModelParser.Parse(type, request.ModelJson);
        // Tiêu đề do Diagram service gửi (tên sơ đồ hiện tại) được ưu tiên hơn title trong JSON
        if (!string.IsNullOrWhiteSpace(request.Title))
            model.Title = request.Title;

        return Task.FromResult(new GeneratePlantUmlResponse { PlantUml = _generator.GeneratePlantUml(type, model) });
    }

    /// <summary>Server streaming: kiểm tra từng sơ đồ của project, gửi kết quả ngay khi xong từng cái.</summary>
    public override async Task ValidateProject(
        ProjectValidationRequest request, IServerStreamWriter<ProjectDiagramValidation> responseStream, ServerCallContext context)
    {
        foreach (var item in request.Diagrams)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var result = _validator.Validate(new DiagramValidationRequest
            {
                DiagramType = item.DiagramType,
                ModelJson = item.ModelJson,
            });
            await responseStream.WriteAsync(new ProjectDiagramValidation { DiagramId = item.DiagramId, Result = result });
        }
    }
}
