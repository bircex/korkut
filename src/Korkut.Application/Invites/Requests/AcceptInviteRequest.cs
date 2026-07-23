using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Invites.Requests;

/// <summary>Body for accepting an invitation: the raw token from the invite link.</summary>
public sealed record AcceptInviteRequest
{
    [Required(ErrorMessage = "Davet kodu zorunludur.")]
    public string? Token { get; init; }
}
