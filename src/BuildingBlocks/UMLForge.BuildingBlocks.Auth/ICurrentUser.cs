namespace UMLForge.BuildingBlocks.Auth;

/// <summary>Người dùng của request hiện tại, đọc từ JWT. Chưa đăng nhập mà đọc UserId → 401.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string Username { get; }

    string Role { get; }
}
