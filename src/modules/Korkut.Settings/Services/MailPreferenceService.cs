using Korkut.Contract;
using Korkut.Contract.Settings;
using Korkut.Core;
using Korkut.Settings.Entities;
using Korkut.Settings.Repositories;

namespace Korkut.Settings.Services;

/// <summary>Office mail-preference orchestration over the repository port; the first save creates it. Returns Result.</summary>
internal sealed class MailPreferenceService(
    IMailPreferenceRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IMailPreferenceService
{
    public Task<Result<MailPreferenceResponse>> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<MailPreferenceResponse>.Ok(ToResponse(await repository.FindAsync(office, ct), CanEdit)), ct);

    public Task<Result<MailPreferenceResponse>> SaveAsync(SaveMailPreferenceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var preference = await repository.FindForUpdateAsync(office, ct);
            if (preference is null)
            {
                preference = new MailPreference(office);
                await repository.AddAsync(preference, ct);
            }

            preference.Update(request.NotificationsEnabled, request.NotifyEmail, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return Result<MailPreferenceResponse>.Ok(ToResponse(preference, canEdit: true));
        }, ct);

    private bool CanEdit => currentUser.Permissions.Contains(Permissions.OfficeSettings);

    private static MailPreferenceResponse ToResponse(MailPreference? preference, bool canEdit) =>
        new(preference?.NotificationsEnabled ?? true, preference?.NotifyEmail, canEdit);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
