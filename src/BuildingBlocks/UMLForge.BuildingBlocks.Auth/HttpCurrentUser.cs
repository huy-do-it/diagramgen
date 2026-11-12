using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UMLForge.BuildingBlocks.Auth;

internal sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(FindClaim(UmlForgeClaims.Subject), out var id)
        ? id
        : throw new UnauthorizedAccessException("Token thiếu hoặc sai claim 'sub'.");

    public string Username => FindClaim(UmlForgeClaims.Username)
        ?? throw new UnauthorizedAccessException("Token thiếu claim 'unique_name'.");

    public string Role => FindClaim(UmlForgeClaims.Role)
        ?? throw new UnauthorizedAccessException("Token thiếu claim 'role'.");

    private string? FindClaim(string type) => Principal?.FindFirst(type)?.Value;
}
