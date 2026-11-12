namespace Workspace.Service.DTOs;

public record UserDirectoryResponse(
    Guid UserId,
    string Username,
    string FullName,
    string Role,
    bool IsLocked);

public record AssignLecturerRequest(Guid LecturerId);
