using System.Diagnostics.CodeAnalysis;

namespace Korkut.Contract;

/// <summary>
/// The outcome of a service operation that returns a value. On success <see cref="Value"/> is set
/// and <see cref="Result.Error"/> is null; on failure the reverse. A bare value lifts into a
/// successful result implicitly, so a service can `return client;`; failures use the factories
/// (`return Result&lt;ClientResponse&gt;.NotFound(...)`).
/// </summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Result<T> is a result monad; the Ok/NotFound/... factories on the closed type are its idiomatic, discoverable API.")]
public sealed class Result<T> : Result
{
    private Result(ResultStatus status, T? value, Error? error)
        : base(status, error)
        => Value = value;

    public T? Value { get; }

    public static Result<T> Ok(T value) => new(ResultStatus.Ok, value, null);

    public static new Result<T> Fail(ResultStatus status, string code, string detail)
        => new(status, default, new Error(code, detail));

    public static new Result<T> NotFound(string detail, string code = "not_found") => Fail(ResultStatus.NotFound, code, detail);

    public static new Result<T> BadRequest(string detail, string code = "bad_request") => Fail(ResultStatus.BadRequest, code, detail);

    public static new Result<T> Conflict(string detail, string code = "conflict") => Fail(ResultStatus.Conflict, code, detail);

    public static new Result<T> Forbidden(string detail, string code = "forbidden") => Fail(ResultStatus.Forbidden, code, detail);

    public static new Result<T> Unauthorized(string detail, string code = "unauthorized") => Fail(ResultStatus.Unauthorized, code, detail);

    public static new Result<T> Unprocessable(string detail, string code = "unprocessable") => Fail(ResultStatus.Unprocessable, code, detail);

    /// <summary>Lift a value into a successful result.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);
}
