namespace Korkut.Core;

/// <summary>
/// Mints and hashes the opaque tokens that travel by e-mail or link (invites, password
/// resets, refresh tokens). Only the hash is ever stored, so possession of the database is
/// not possession of the token.
/// </summary>
public interface ITokenHasher
{
    string Hash(string rawToken);

    string NewRawToken();
}
