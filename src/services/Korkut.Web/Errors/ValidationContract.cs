namespace Korkut.Web.Errors;

/// <summary>One field-level validation failure, matching the Python/FastAPI shape.</summary>
public sealed record ValidationError(string Type, IReadOnlyList<string> Loc, string Msg, object? Input);

/// <summary>422 body: <c>{ "detail": [ { type, loc, msg, input }, ... ] }</c>.</summary>
public sealed record ValidationProblem(IReadOnlyList<ValidationError> Detail);
