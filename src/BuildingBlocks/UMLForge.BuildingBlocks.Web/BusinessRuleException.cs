using Microsoft.AspNetCore.Http;

namespace UMLForge.BuildingBlocks.Web;

/// <summary>
/// Vi phạm nghiệp vụ → ProblemDetails có <c>code</c> (ví dụ "BR-14") và <c>status</c> tương ứng.
/// Gán <see cref="Errors"/> khi cần trả danh sách lỗi chi tiết (ví dụ lỗi UML từ gRPC → 422).
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message, int status = StatusCodes.Status409Conflict)
        : base(message)
    {
        Code = code;
        Status = status;
    }

    public string Code { get; }

    public int Status { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}

public sealed class NotFoundException : BusinessRuleException
{
    public NotFoundException(string message)
        : base("NOT_FOUND", message, StatusCodes.Status404NotFound)
    {
    }
}

public sealed class ForbiddenException : BusinessRuleException
{
    public ForbiddenException(string code, string message)
        : base(code, message, StatusCodes.Status403Forbidden)
    {
    }
}
