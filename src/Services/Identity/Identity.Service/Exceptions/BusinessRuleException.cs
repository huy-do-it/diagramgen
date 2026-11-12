namespace Identity.Service.Exceptions;

public class BusinessRuleException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public BusinessRuleException(string code, string message, int statusCode = 409, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Errors = errors;
    }
}
