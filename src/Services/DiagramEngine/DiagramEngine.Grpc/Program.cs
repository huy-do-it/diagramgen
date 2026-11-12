using Microsoft.Extensions.Diagnostics.HealthChecks;
using UMLForge.BuildingBlocks.Web;
using UMLForge.DiagramEngine.Grpc.Generators;
using UMLForge.DiagramEngine.Grpc.Interceptors;
using UMLForge.DiagramEngine.Grpc.Services;
using UMLForge.DiagramEngine.Grpc.Validators;

var builder = WebApplication.CreateBuilder(args);

// Service gRPC nội bộ: không JWT/Swagger, chỉ log có cấu trúc (Serilog + CorrelationId)
builder.AddUmlForgeLogging("diagram-engine");
builder.Services.AddGrpc(options => options.Interceptors.Add<LoggingInterceptor>());
builder.Services.AddGrpcHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());
if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

// Logic thuần, không giữ trạng thái → singleton
builder.Services.AddSingleton<IPlantUmlGenerator, PlantUmlGenerator>();
builder.Services.AddSingleton<IUmlRuleValidator, UmlRuleValidator>();
builder.Services.AddSingleton<IRelationSuggester, RelationSuggester>();

var app = builder.Build();

app.MapGrpcService<DiagramEngineGrpcService>();
app.MapGrpcHealthChecksService();
// Reflection để Postman / grpcurl tự đọc danh sách RPC (chỉ Development)
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

app.MapGet("/", () => "UMLForge DiagramEngine (gRPC). Gọi bằng gRPC client, không qua trình duyệt.");

app.Run();

/// <summary>Cho WebApplicationFactory trong DiagramEngine.Tests.</summary>
public partial class Program
{
}
