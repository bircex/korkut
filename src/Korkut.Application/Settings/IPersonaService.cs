using Korkut.Application.Settings.Requests;
using Korkut.Application.Settings.Responses;

namespace Korkut.Application.Settings;

/// <summary>
/// Office-persona use-cases. An office that never set one reads as empty rather than
/// missing, so the settings screen always has something to show.
/// </summary>
public interface IPersonaService
{
    Task<PersonaResponse> GetAsync(CancellationToken ct = default);

    Task<PersonaResponse> SaveAsync(SavePersonaRequest request, CancellationToken ct = default);
}
