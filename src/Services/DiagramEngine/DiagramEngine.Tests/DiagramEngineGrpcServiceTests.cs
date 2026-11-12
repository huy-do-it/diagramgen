using FluentAssertions;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using Microsoft.AspNetCore.Mvc.Testing;
using UMLForge.Contracts.Grpc;
using UMLForge.DiagramEngine.Grpc.Models;
using Xunit;

namespace UMLForge.DiagramEngine.Tests;

/// <summary>
/// B-04: gọi cả 4 RPC + health + reflection qua gRPC client thật (TestServer trong bộ nhớ),
/// dữ liệu mẫu CampusEats lấy từ template của v1.
/// </summary>
public sealed class DiagramEngineGrpcServiceTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string CampusEatsUseCase = """
        {
          "title": "CampusEats - Use Cases",
          "systemBoundary": "CampusEats",
          "actors": ["Student", "Canteen Staff", "Canteen Manager", "VNPay"],
          "useCases": ["Log in", "Browse menu", "Place order", "Apply voucher", "Pay online", "Pay with VNPay",
                       "Track order status", "Confirm order", "Update menu and stock", "View sales report"],
          "relations": [
            { "from": "Student", "to": "Browse menu" },
            { "from": "Student", "to": "Place order" },
            { "from": "Student", "to": "Track order status" },
            { "from": "Place order", "to": "Log in", "type": "include" },
            { "from": "Place order", "to": "Pay online", "type": "include" },
            { "from": "Apply voucher", "to": "Place order", "type": "extend" },
            { "from": "Pay with VNPay", "to": "Pay online", "type": "generalization" },
            { "from": "Pay with VNPay", "to": "VNPay" },
            { "from": "Canteen Staff", "to": "Confirm order" },
            { "from": "Canteen Staff", "to": "Update menu and stock" },
            { "from": "Canteen Manager", "to": "Canteen Staff", "type": "generalization" },
            { "from": "Canteen Manager", "to": "View sales report" }
          ]
        }
        """;

    // Include xuất phát từ actor — lỗi UML kinh điển dùng trong demo WF-02
    private const string IncludeFromActor = """
        {
          "title": "CampusEats - Wrong include",
          "actors": ["Student"],
          "useCases": ["Place order"],
          "relations": [ { "from": "Student", "to": "Place order", "type": "include" } ]
        }
        """;

    private const string CampusEatsActivity = """
        {
          "title": "CampusEats - Order a Meal",
          "startNode": "Open CampusEats",
          "steps": ["Choose canteen", "Add dishes to cart", "Place order", "Pay with VNPay"],
          "decisions": [ { "condition": "Logged in?", "yesBranch": "Choose canteen", "noBranch": "Log in with student account" } ]
        }
        """;

    private readonly GrpcChannel _channel;
    private readonly DiagramEngineService.DiagramEngineServiceClient _client;

    public DiagramEngineGrpcServiceTests(WebApplicationFactory<Program> factory)
    {
        _channel = GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler(),
        });
        _client = new DiagramEngineService.DiagramEngineServiceClient(_channel);
    }

    public void Dispose() => _channel.Dispose();

    [Fact]
    public async Task ValidateDiagram_CampusEatsUseCase_IsValid()
    {
        var result = await _client.ValidateDiagramAsync(new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = CampusEatsUseCase,
        });

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateDiagram_IncludeFromActor_ReturnsError()
    {
        var result = await _client.ValidateDiagramAsync(new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = IncludeFromActor,
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("must connect two use cases"));
    }

    [Fact]
    public async Task SuggestRelations_UnconnectedActor_SuggestsFirstUseCase()
    {
        var result = await _client.SuggestRelationsAsync(new RelationSuggestionRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = """{ "title": "T", "actors": ["Student", "Auditor"], "useCases": ["Browse menu"], "relations": [ { "from": "Student", "to": "Browse menu" } ] }""",
        });

        result.Suggestions.Should().ContainSingle(suggestion => suggestion.From == "Auditor" && suggestion.To == "Browse menu");
    }

    [Fact]
    public async Task GeneratePlantUml_ValidUseCase_ReturnsPlantUmlWithRequestTitle()
    {
        var result = await _client.GeneratePlantUmlAsync(new GeneratePlantUmlRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = CampusEatsUseCase,
            Title = "Use Case v3",
        });

        result.PlantUml.Should().StartWith("@startuml\n").And.EndWith("@enduml");
        result.PlantUml.Should().Contain("title Use Case v3");
        result.PlantUml.Should().Contain("<<include>>").And.Contain("<<extend>>");
    }

    [Fact]
    public async Task GeneratePlantUml_EmptyRequestTitle_KeepsModelTitle()
    {
        var result = await _client.GeneratePlantUmlAsync(new GeneratePlantUmlRequest
        {
            DiagramType = (int)DiagramType.Activity,
            ModelJson = CampusEatsActivity,
        });

        result.PlantUml.Should().Contain("title CampusEats - Order a Meal");
        result.PlantUml.Should().Contain("if (Logged in?) then (yes)");
    }

    [Fact]
    public async Task GeneratePlantUml_InvalidModel_ThrowsInvalidArgumentWithErrors()
    {
        var call = () => _client.GeneratePlantUmlAsync(new GeneratePlantUmlRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = IncludeFromActor,
        }).ResponseAsync;

        var exception = (await call.Should().ThrowAsync<RpcException>()).Which;
        exception.StatusCode.Should().Be(StatusCode.InvalidArgument);
        exception.Status.Detail.Should().StartWith("Sơ đồ chưa hợp lệ:").And.Contain("must connect two use cases");
    }

    [Theory]
    [InlineData((int)DiagramType.UseCase, "{ not json")]
    [InlineData(99, "{}")]
    public async Task GeneratePlantUml_MalformedJsonOrUnknownType_ThrowsInvalidArgument(int diagramType, string modelJson)
    {
        var call = () => _client.GeneratePlantUmlAsync(new GeneratePlantUmlRequest
        {
            DiagramType = diagramType,
            ModelJson = modelJson,
        }).ResponseAsync;

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task ValidateProject_ThreeDiagrams_StreamsOneResultPerDiagramInOrder()
    {
        var request = new ProjectValidationRequest();
        request.Diagrams.Add(new ProjectDiagramItem { DiagramId = "d1", DiagramType = (int)DiagramType.UseCase, ModelJson = CampusEatsUseCase });
        request.Diagrams.Add(new ProjectDiagramItem { DiagramId = "d2", DiagramType = (int)DiagramType.UseCase, ModelJson = IncludeFromActor });
        request.Diagrams.Add(new ProjectDiagramItem { DiagramId = "d3", DiagramType = (int)DiagramType.Activity, ModelJson = CampusEatsActivity });

        using var call = _client.ValidateProject(request);
        var received = new List<ProjectDiagramValidation>();
        await foreach (var item in call.ResponseStream.ReadAllAsync())
            received.Add(item);

        received.Select(item => item.DiagramId).Should().Equal("d1", "d2", "d3");
        received.Select(item => item.Result.IsValid).Should().Equal(true, false, true);
    }

    [Fact]
    public async Task Call_WithCorrelationIdMetadata_EchoedInTrailers()
    {
        var correlationId = Guid.NewGuid().ToString();
        using var call = _client.ValidateDiagramAsync(
            new DiagramValidationRequest { DiagramType = (int)DiagramType.UseCase, ModelJson = CampusEatsUseCase },
            new Metadata { { "x-correlation-id", correlationId } });

        await call.ResponseAsync;

        call.GetTrailers().GetValue("x-correlation-id").Should().Be(correlationId);
    }

    [Fact]
    public async Task HealthCheck_Overall_IsServing()
    {
        var health = new Health.HealthClient(_channel);

        var response = await health.CheckAsync(new HealthCheckRequest());

        response.Status.Should().Be(HealthCheckResponse.Types.ServingStatus.Serving);
    }

    [Fact]
    public async Task Reflection_Development_ListsDiagramEngineService()
    {
        var reflection = new ServerReflection.ServerReflectionClient(_channel);
        using var call = reflection.ServerReflectionInfo();

        await call.RequestStream.WriteAsync(new ServerReflectionRequest { ListServices = string.Empty });
        await call.RequestStream.CompleteAsync();
        (await call.ResponseStream.MoveNext()).Should().BeTrue();

        call.ResponseStream.Current.ListServicesResponse.Service.Select(service => service.Name)
            .Should().Contain("diagramengine.DiagramEngineService");
    }
}
