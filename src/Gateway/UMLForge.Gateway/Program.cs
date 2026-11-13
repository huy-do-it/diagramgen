using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UMLForge.BuildingBlocks.Auth;

var builder = WebApplication.CreateBuilder(args);

// 1. CORS
const string corsPolicy = "UmlForgeCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("ETag", "X-Correlation-Id");
    });
});

// 2. JWT Authentication & Authorization for Gateway
builder.Services.AddUmlForgeJwt(builder.Configuration);

// 3. Rate Limiter (BR / Tasks §2.5: 10 requests / min / IP on /api/auth/login)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login-limiter", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// 4. YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// 5. Middleware: Correlation ID
app.Use(async (context, next) =>
{
    const string headerName = "X-Correlation-Id";
    if (!context.Request.Headers.TryGetValue(headerName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString();
        context.Request.Headers[headerName] = correlationId;
    }
    context.Response.Headers[headerName] = correlationId;
    await next();
});

// 6. Security Pipeline
app.UseCors(corsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 7. Health Check
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Gateway", timestamp = DateTime.UtcNow }));

// 8. Swagger Aggregator Portal (Tasks §2.5 item 6)
app.MapGet("/swagger", () => Results.Content("""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>UMLForge v2 — API Gateway Portal</title>
    <style>
        :root { --primary: #3b82f6; --bg: #0f172a; --card: #1e293b; --text: #f8fafc; --muted: #94a3b8; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: var(--bg); color: var(--text); padding: 40px 20px; margin: 0; }
        .container { max-width: 900px; margin: 0 auto; }
        h1 { font-size: 2rem; margin-bottom: 8px; color: #fff; }
        p.subtitle { color: var(--muted); margin-bottom: 32px; font-size: 1.1rem; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 20px; }
        .card { background: var(--card); border: 1px solid #334155; border-radius: 12px; padding: 24px; text-decoration: none; color: inherit; transition: all 0.2s; display: flex; flex-direction: column; justify-content: space-between; }
        .card:hover { transform: translateY(-3px); border-color: var(--primary); box-shadow: 0 10px 25px -5px rgba(59, 130, 246, 0.2); }
        .badge { display: inline-block; padding: 4px 8px; border-radius: 6px; font-size: 0.75rem; font-weight: 600; text-transform: uppercase; background: #0284c7; color: #fff; margin-bottom: 12px; width: fit-content; }
        .card h3 { margin: 0 0 8px 0; font-size: 1.25rem; }
        .card p { color: var(--muted); font-size: 0.9rem; margin: 0 0 16px 0; line-height: 1.4; }
        .port { font-family: monospace; font-size: 0.85rem; color: #38bdf8; }
    </style>
</head>
<body>
    <div class="container">
        <h1>UMLForge v2 — API Gateway</h1>
        <p class="subtitle">Cổng điều phối Reverse Proxy (YARP) trên Port 5000 kết nối 5 Microservices</p>
        <div class="grid">
            <a href="http://localhost:5001/swagger" target="_blank" class="card">
                <div>
                    <span class="badge" style="background: #2563eb;">Port 5001</span>
                    <h3>Identity Service</h3>
                    <p>Xác thực người dùng, JWT, Refresh Token rotation (BR-01..05) & Quản lý User.</p>
                </div>
                <div class="port">http://localhost:5001/swagger &rarr;</div>
            </a>
            <a href="http://localhost:5002/swagger" target="_blank" class="card">
                <div>
                    <span class="badge" style="background: #059669;">Port 5002</span>
                    <h3>Workspace Service</h3>
                    <p>Quản lý dự án, thành viên (BR-06..08), lời mời (BR-09), danh bạ GVHD (BR-10).</p>
                </div>
                <div class="port">http://localhost:5002/swagger &rarr;</div>
            </a>
            <a href="http://localhost:5003/swagger" target="_blank" class="card">
                <div>
                    <span class="badge" style="background: #d97706;">Port 5003</span>
                    <h3>Diagram Service</h3>
                    <p>Thiết kế sơ đồ UML, phiên bản (BR-15..17), gRPC client, yêu cầu render (BR-14).</p>
                </div>
                <div class="port">http://localhost:5003/swagger &rarr;</div>
            </a>
            <a href="http://localhost:5004/swagger" target="_blank" class="card">
                <div>
                    <span class="badge" style="background: #7c3aed;">Port 5004</span>
                    <h3>Render Service</h3>
                    <p>Pipeline render PlantUML qua worker, timeout (BR-27), lưu trữ SVG kết quả.</p>
                </div>
                <div class="port">http://localhost:5004/swagger &rarr;</div>
            </a>
            <a href="http://localhost:5005/swagger" target="_blank" class="card">
                <div>
                    <span class="badge" style="background: #db2777;">Port 5005</span>
                    <h3>Notification Service</h3>
                    <p>Tiêu thụ sự kiện, thông báo trong ứng dụng cho dự án, thành viên và render.</p>
                </div>
                <div class="port">http://localhost:5005/swagger &rarr;</div>
            </a>
        </div>
    </div>
</body>
</html>
""", "text/html"));

// 9. Map YARP Endpoints
app.MapReverseProxy();

app.Run();

