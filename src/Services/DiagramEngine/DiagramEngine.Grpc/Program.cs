using UMLForge.BuildingBlocks.Web;
using UMLForge.DiagramEngine.Grpc.Generators;
using UMLForge.DiagramEngine.Grpc.Validators;

var builder = WebApplication.CreateBuilder(args);

// Service gRPC nội bộ: không JWT/Swagger, chỉ log có cấu trúc (Serilog + CorrelationId)
builder.AddUmlForgeLogging("diagram-engine");
builder.Services.AddGrpc();

// Logic thuần, không giữ trạng thái → singleton
builder.Services.AddSingleton<IPlantUmlGenerator, PlantUmlGenerator>();
builder.Services.AddSingleton<IUmlRuleValidator, UmlRuleValidator>();
builder.Services.AddSingleton<IRelationSuggester, RelationSuggester>();

var app = builder.Build();

app.MapGet("/", () => "UMLForge DiagramEngine (gRPC). Gọi bằng gRPC client, không qua trình duyệt.");

app.Run();
