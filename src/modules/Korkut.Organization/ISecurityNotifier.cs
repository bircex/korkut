using Korkut.Organization.Entities;
namespace Korkut.Organization;

/// <summary>Security notifications (password changed). Never includes secrets or tokens.</summary>
public interface ISecurityNotifier
{
    Task NotifyPasswordChangedAsync(Guid officeId, string email, CancellationToken ct = default);
}
