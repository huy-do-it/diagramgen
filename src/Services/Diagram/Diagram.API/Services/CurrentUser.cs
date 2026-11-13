using System.Security.Claims;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.API.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var user = Principal;
            if (user == null) return Guid.Empty;

            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(sub, out var guid) ? guid : Guid.Empty;
        }
    }

    public string Username
    {
        get
        {
            var user = Principal;
            if (user == null) return string.Empty;

            return user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.FindFirst("unique_name")?.Value
                ?? string.Empty;
        }
    }

    public string Role
    {
        get
        {
            var user = Principal;
            if (user == null) return string.Empty;

            return user.FindFirst(ClaimTypes.Role)?.Value
                ?? user.FindFirst("role")?.Value
                ?? string.Empty;
        }
    }
}
