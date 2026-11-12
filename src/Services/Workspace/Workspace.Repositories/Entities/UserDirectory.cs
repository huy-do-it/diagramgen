namespace Workspace.Repositories.Entities;

public class UserDirectory
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Student";
    public bool IsLocked { get; set; }
}
