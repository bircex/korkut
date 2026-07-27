namespace Korkut.Contract;

/// <summary>
/// A failure a service returns: a stable <see cref="Code"/> callers can branch on and a localized
/// <see cref="Detail"/> for the user. Carried by a non-Ok <see cref="Result"/> / <see cref="Result{T}"/>.
/// </summary>
public sealed record Error(string Code, string Detail);
