using UMLForge.Diagram.Repositories.Enums;

namespace UMLForge.Diagram.Repositories.Entities;

public class MemberAccess
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = ProjectRole.Member;
}
