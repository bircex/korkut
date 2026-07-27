using Korkut.Contract.Notes;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Notes;

/// <summary>Self-registration for the Notes module: binds the repository + service ports.</summary>
public sealed class NotesModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteService, NoteService>();
    }
}
