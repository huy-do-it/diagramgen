using Grpc.Health.V1;
using Grpc.Net.Client;

namespace UMLForge.DiagramEngine.Grpc;

/// <summary>
/// Healthcheck cho Docker: <c>dotnet DiagramEngine.Grpc.dll --healthcheck</c> gọi grpc.health.v1 trên chính container.
/// Cần vì container chỉ nhận HTTP/2 (không dùng được curl /health) và image aspnet không có grpc_health_probe.
/// </summary>
internal static class GrpcHealthProbe
{
    public const string Argument = "--healthcheck";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <returns>0 nếu SERVING, 1 nếu không.</returns>
    public static async Task<int> RunAsync(string address)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(address);
            var response = await new Health.HealthClient(channel)
                .CheckAsync(new HealthCheckRequest(), deadline: DateTime.UtcNow.Add(Timeout));
            return response.Status == HealthCheckResponse.Types.ServingStatus.Serving ? 0 : 1;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}
