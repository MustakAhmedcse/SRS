namespace SalesCom.Domain.Common;

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Unexpected = 6,
}

/// <summary>
/// A domain-level failure. Codes follow <c>Area.Cause</c> (e.g. <c>DataSource.NotFound</c>,
/// <c>DataSource.AlreadyRegistered</c>) and are stable identifiers safe for clients to switch on.
/// </summary>
public sealed record ErrorBase(string Code, string Message, ErrorType Type)
{
    public static readonly ErrorBase None = new(string.Empty, string.Empty, ErrorType.None);

    public static ErrorBase Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static ErrorBase NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static ErrorBase Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static ErrorBase Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static ErrorBase Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static ErrorBase Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
