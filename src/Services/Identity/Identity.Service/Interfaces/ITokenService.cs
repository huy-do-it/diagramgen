using Identity.Repositories.Entities;

namespace Identity.Service.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}
