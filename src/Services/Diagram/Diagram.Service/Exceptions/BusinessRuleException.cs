namespace UMLForge.Diagram.Service.Exceptions;

public class BusinessRuleException : Exception
{
    public string Code { get; }
    public int Status { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public BusinessRuleException(string code, string message, int status = 409, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Code = code;
        Status = status;
        Errors = errors;
    }
}
