namespace Korkut.Contract;

/// <summary>
/// The outcome of a service operation with no return value: a <see cref="Status"/> and, when it
/// failed, an <see cref="Error"/>. Services RETURN this instead of throwing, so the flow is
/// explicit and the host maps <see cref="Status"/> to an HTTP status uniformly.
/// </summary>
public class Result
{
    protected Result(ResultStatus status, Error? error)
    {
        Status = status;
        Error = error;
    }

    public ResultStatus Status { get; }

    public Error? Error { get; }

    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result Ok() => new(ResultStatus.Ok, null);

    public static Result Fail(ResultStatus status, string code, string detail) => new(status, new Error(code, detail));

    public static Result NotFound(string detail, string code = "not_found") => Fail(ResultStatus.NotFound, code, detail);

    public static Result BadRequest(string detail, string code = "bad_request") => Fail(ResultStatus.BadRequest, code, detail);

    public static Result Conflict(string detail, string code = "conflict") => Fail(ResultStatus.Conflict, code, detail);

    public static Result Forbidden(string detail, string code = "forbidden") => Fail(ResultStatus.Forbidden, code, detail);

    public static Result Unauthorized(string detail, string code = "unauthorized") => Fail(ResultStatus.Unauthorized, code, detail);

    public static Result Unprocessable(string detail, string code = "unprocessable") => Fail(ResultStatus.Unprocessable, code, detail);
}
