using Korkut.Application.Common;
using Korkut.Application.Security;
using Korkut.Application.Settings.Requests;
using Korkut.Application.Settings.Responses;
using Korkut.Domain.Settings;
using Korkut.Domain.Time;

namespace Korkut.Application.Settings;

/// <summary>Office-persona orchestration over the repository port; the first save creates it.</summary>
public sealed class PersonaService(
    IPersonaRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IPersonaService
{
    public Task<PersonaResponse> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            ToResponse(await repository.FindAsync(office, ct), CanEdit), ct);

    public Task<PersonaResponse> SaveAsync(SavePersonaRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var persona = await repository.FindForUpdateAsync(office, ct);
            if (persona is null)
            {
                persona = new Persona(office);
                await repository.AddAsync(persona, ct);
            }

            persona.Update(request.Tone, request.Expertise, request.ExtraRules, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return ToResponse(persona, canEdit: true);
        }, ct);

    private bool CanEdit => currentUser.Permissions.Contains(Permissions.OfficeSettings);

    private static PersonaResponse ToResponse(Persona? persona, bool canEdit) =>
        new(persona?.Tone ?? "", persona?.Expertise ?? "", persona?.ExtraRules ?? "", canEdit);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
