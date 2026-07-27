namespace Korkut.Api.Contracts;

/// <summary>
/// The bare acknowledgement the client's session calls expect: everything that matters
/// (the cookies) travelled in the headers.
/// </summary>
public sealed record OkResponse(bool Ok)
{
    public static readonly OkResponse Done = new(true);
}
