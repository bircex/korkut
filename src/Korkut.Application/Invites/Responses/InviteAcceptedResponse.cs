namespace Korkut.Application.Invites.Responses;

/// <summary>The outcome of accepting an invitation: which e-mail may now sign in.</summary>
public sealed record InviteAcceptedResponse(bool Ok, string Email);
