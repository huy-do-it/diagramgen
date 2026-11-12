namespace UMLForge.Contracts.Events;

/// <summary>Giá trị chuỗi dùng trong payload (TASKS §1.2, §1.3, §1.5).</summary>
public static class UserRoles
{
    public const string Student = "Student";
    public const string Lecturer = "Lecturer";
    public const string Admin = "Admin";
}

public static class ProjectRoles
{
    public const string Leader = "Leader";
    public const string Member = "Member";
    public const string Lecturer = "Lecturer";
}

public static class ReviewDecisions
{
    public const string Approved = "Approved";
    public const string ChangesRequested = "ChangesRequested";
}
