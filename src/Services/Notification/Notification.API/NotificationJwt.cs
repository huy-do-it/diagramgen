using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace UMLForge.Notification.API;

// Bootstrap cục bộ theo TASKS §1.2 khi B-01 chưa có; thay bằng shared defaults khi ghép.
internal static class NotificationJwt
{
    public static IServiceCollection AddNotificationJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32
            || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configure Jwt:Key (at least 32 UTF-8 bytes), Jwt:Issuer and Jwt:Audience.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, NameClaimType = "unique_name",
                RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30)
            };
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty)
                        context.Fail("Token must contain a nonempty Guid sub.");
                    return Task.CompletedTask;
                }
            };
        });
        return services;
    }
}
