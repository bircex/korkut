namespace Korkut.Application.Users.Responses;

/// <summary>
/// The signed-in user as the client's session probe expects it. <c>Username</c> repeats the
/// e-mail: the client has always shown that as the display handle.
/// </summary>
public sealed record ProfileResponse(
    string Username, string Email, string? FullName, string? BarRegistryNo, string? Office, string Role);
