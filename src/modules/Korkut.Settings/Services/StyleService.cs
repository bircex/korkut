using Korkut.Settings.Entities;
using Korkut.Contract;
using Korkut.Contract.Settings;
using Korkut.Core;

namespace Korkut.Settings;

/// <summary>
/// Drafting-style orchestration over the repository port. The overlay rule lives on
/// <see cref="StyleFields"/>; this decides which layer a write lands on, and the user layer is always
/// the caller's own id. Returns Result.
/// </summary>
internal sealed class StyleService(
    IStyleProfileRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IStyleService
{
    public Task<Result<StyleResponse>> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var canEditOffice = currentUser.Permissions.Contains(Permissions.OfficeSettings);
            var officeLayer = await FindFieldsAsync(office, StyleScope.Office, Guid.Empty, ct);
            var userLayer = await FindFieldsAsync(office, StyleScope.User, currentUser.UserId, ct);
            return Result<StyleResponse>.Ok(new StyleResponse(
                ToResponse(officeLayer), ToResponse(userLayer), ToResponse(userLayer.Over(officeLayer)), canEditOffice));
        }, ct);

    public Task<Result<StyleFieldsResponse>> SaveUserAsync(SaveStyleRequest request, CancellationToken ct = default)
        => SaveAsync(StyleScope.User, currentUser.UserId, request, ct);

    public Task<Result<StyleFieldsResponse>> SaveOfficeAsync(SaveStyleRequest request, CancellationToken ct = default)
        => SaveAsync(StyleScope.Office, Guid.Empty, request, ct);

    private Task<Result<StyleFieldsResponse>> SaveAsync(string scope, Guid userId, SaveStyleRequest request, CancellationToken ct)
        => InScopeAsync(async office =>
        {
            var fields = ToFields(request);
            var profile = await repository.FindForUpdateAsync(office, scope, userId, ct);
            if (profile is null)
            {
                profile = new StyleProfile(office, scope, userId);
                await repository.AddAsync(profile, ct);
            }

            profile.Apply(fields, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return Result<StyleFieldsResponse>.Ok(ToResponse(fields));
        }, ct);

    private async Task<StyleFields> FindFieldsAsync(Guid officeId, string scope, Guid userId, CancellationToken ct)
        => (await repository.FindAsync(officeId, scope, userId, ct))?.Fields ?? StyleFields.Empty;

    private static StyleFields ToFields(SaveStyleRequest request) =>
        new(request.Letterhead ?? "", request.Signature ?? "", request.Tone ?? "", request.Boilerplate ?? "");

    private static StyleFieldsResponse ToResponse(StyleFields f) =>
        new(f.Letterhead, f.Signature, f.Tone, f.Boilerplate);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
