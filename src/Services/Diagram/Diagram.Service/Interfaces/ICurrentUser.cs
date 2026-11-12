namespace UMLForge.Diagram.Service.Interfaces;

public interface ICurrentUser
{
    Guid UserId { get; }
    string Username { get; }
    string Role { get; }
}
