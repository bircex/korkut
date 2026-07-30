using Korkut.Contract.Settings;
using Korkut.Core;
using Korkut.Settings.Repositories;
using Korkut.Settings.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Settings.Extensions;

/// <summary>
/// Self-registration for the Settings module: persona, drafting-style and memory repositories +
/// </summary>
public sealed class SettingsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPersonaRepository, PersonaRepository>();
        services.AddScoped<IPersonaService, PersonaService>();
        services.AddScoped<IStyleProfileRepository, StyleProfileRepository>();
        services.AddScoped<IStyleService, StyleService>();
        services.AddScoped<IMemoryRepository, MemoryRepository>();
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IMailPreferenceRepository, MailPreferenceRepository>();
        services.AddScoped<IMailPreferenceService, MailPreferenceService>();
    }
}
