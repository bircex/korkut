using Korkut.Contract;
using Korkut.Contract.Settings;
using Korkut.Core;
using Korkut.Settings.Entities;
using Korkut.Settings.Repositories;

namespace Korkut.Settings.Services;

/// <summary>Office-persona orchestration over the repository port; the first save creates it. Returns Result.</summary>
internal sealed class PersonaService(
    IPersonaRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IPersonaService
{
    public Task<Result<PersonaResponse>> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<PersonaResponse>.Ok(ToResponse(await repository.FindAsync(office, ct), CanEdit)), ct);

    public Task<Result<PersonaResponse>> SaveAsync(SavePersonaRequest request, CancellationToken ct = default)
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
            return Result<PersonaResponse>.Ok(ToResponse(persona, canEdit: true));
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
