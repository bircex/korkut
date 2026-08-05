using Korkut.Contract.LawClients;
using Korkut.Core;
using Korkut.LawClients.Services;
using Korkut.LawClients.Services.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.LawClients.Extensions;

/// <summary>
/// Self-registration for the LawClients module: every source client + the search/document
/// services over them. HTTP transport (named clients, resilience, rate limiting) is registered
/// here too — one place, so both the agent tool loop and this module's own Controller share the
/// same protection against the upstream public databases.
/// </summary>
public sealed class LawClientsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        // Simple source clients: one instance, no per-source parameters.
        services.AddScoped<AnayasaClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<AnayasaClient>());
        services.AddScoped<GibClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<GibClient>());
        services.AddScoped<KikClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<KikClient>());
        services.AddScoped<MevzuatClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<MevzuatClient>());
        services.AddScoped<RekabetClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<RekabetClient>());
        services.AddScoped<SayistayClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<SayistayClient>());
        services.AddScoped<SigortaTahkimClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<SigortaTahkimClient>());
        services.AddScoped<UyusmazlikClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<UyusmazlikClient>());

        // Tavily-backed sources (KVKK, BDDK): need the shared Tavily search client too.
        services.AddScoped<TavilyClient>();
        services.AddScoped<KvkkClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<KvkkClient>());
        services.AddScoped<BddkClient>();
        services.AddScoped<ISourceClient>(sp => sp.GetRequiredService<BddkClient>());

        // Bedesten (Ministry of Justice, bedesten.adalet.gov.tr) is a base-relative API shared by
        // four court-decision sources, each pinning its own itemTypeList (the API's court-type
        // enum). Yargıtay/Danıştay support chamber (daire) filtering; Emsal/KYB do not.
        void Bedesten(string source, string[] itemTypes, bool chamber) =>
            services.AddScoped<ISourceClient>(sp => new BedestenClient(
                sp.GetRequiredService<IHttpClientFactory>(), source, itemTypes, chamber));
        Bedesten("yargitay", ["YARGITAYKARARI"], chamber: true);
        Bedesten("danistay", ["DANISTAYKARAR"], chamber: true);
        Bedesten("emsal", ["YERELHUKUK", "ISTINAFHUKUK"], chamber: false);
        Bedesten("kyb", ["KYB"], chamber: false);

        services.AddScoped<LawClientsService>();
        services.AddScoped<ILawClientsSearchService>(sp => sp.GetRequiredService<LawClientsService>());
        services.AddScoped<ILawClientsDocumentService>(sp => sp.GetRequiredService<LawClientsService>());

        LawClientsHttp.Register(services);
    }
}
