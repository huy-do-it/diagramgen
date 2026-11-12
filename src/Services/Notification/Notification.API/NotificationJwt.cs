using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace UMLForge.Notification.API;

// Giữ ràng buộc token của Notification trên đăng ký JWT chung, không tạo scheme thứ hai.
internal static class NotificationJwt
{
    public static IServiceCollection ConfigureNotificationJwt(this IServiceCollection services)
        => services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
        {
            o.TokenValidationParameters.ValidateIssuer = true;
            o.TokenValidationParameters.ValidateAudience = true;
            o.TokenValidationParameters.ValidateLifetime = true;
            o.TokenValidationParameters.RequireExpirationTime = true;
            o.TokenValidationParameters.RequireSignedTokens = true;
            o.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
            o.Events ??= new JwtBearerEvents();
            var previous = o.Events.OnTokenValidated;
            o.Events.OnTokenValidated = async context =>
            {
                await previous(context);
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty)
                    context.Fail("Token must contain a nonempty Guid sub.");
            };
        });
}
