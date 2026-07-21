using System.Text.Json;
using Korkut.Agent.Tools;
using Korkut.Agent.Tools.Office;
using Korkut.Application.Features.OfficeCore;
using Korkut.Domain.OfficeCore;

namespace Korkut.Agent.Tests;

/// <summary>
/// The office-domain tools (matters + clients): they wrap the operational services, run inside a
/// per-office scope, and expose args as JSON. Pure unit tests over a pass-through office scope +
/// fake services (no DB / container).
/// </summary>
public class OfficeToolsTests
{
    // Tools don't self-check permission (the catalog gates that); a direct-invoke test needs any set.
    private static readonly ToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());
    private static readonly JsonElement NoArgs = JsonSerializer.SerializeToElement(new { });

    // Runs the work directly — the real OfficeScope would open a tenant transaction; tests skip that.
    private sealed class PassThroughScope : IOfficeScope
    {
        public Task<T> RunAsync<T>(Guid officeId, Func<CancellationToken, Task<T>> work, CancellationToken ct = default) => work(ct);
    }

    private sealed class FakeMatters(params Matter[] seed) : IMatterService
    {
        public List<Matter> Created { get; } = [];

        public Task<IReadOnlyList<Matter>> ListAsync(Guid officeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Matter>>(seed);

        public Task<Matter> CreateAsync(Guid officeId, string title, Guid? clientId, Guid? courtId, CancellationToken ct = default)
        {
            var m = new Matter(Guid.CreateVersion7(), officeId, title, clientId, null, null, null, courtId, null, "open", null, null, DateTimeOffset.UtcNow);
            Created.Add(m);
            return Task.FromResult(m);
        }

        public Task<MatterDetail?> GetDetailAsync(Guid officeId, Guid id, CancellationToken ct = default)
        {
            var m = seed.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(m is null ? null : new MatterDetail(m, []));
        }

        public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<Hearing?> AddHearingAsync(Guid officeId, Guid matterId, DateTimeOffset scheduledAt, string? type, string? location, string? notes, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeClients(params Client[] seed) : IClientService
    {
        public List<Client> Created { get; } = [];

        public Task<IReadOnlyList<Client>> ListAsync(Guid officeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Client>>(seed);

        public Task<Client> CreateAsync(Guid officeId, string name, string kind, string? email, string? phone, CancellationToken ct = default)
        {
            var c = new Client(Guid.CreateVersion7(), officeId, name, kind, email, phone, null, DateTimeOffset.UtcNow);
            Created.Add(c);
            return Task.FromResult(c);
        }

        public Task<Client?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Client?> UpdateAsync(Guid officeId, Guid id, string name, string kind, string? email, string? phone, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static Matter M(string title) =>
        new(Guid.CreateVersion7(), Guid.NewGuid(), title, null, null, null, null, null, null, "open", null, null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task List_matters_returns_the_offices_matters()
    {
        var tool = new ListMattersTool(new PassThroughScope(), new FakeMatters(M("Kira davası"), M("İş davası")));
        var result = await tool.InvokeAsync(NoArgs, Ctx);
        Assert.False(result.IsError);
        Assert.Contains("Kira davası", result.Content);
        Assert.Contains("İş davası", result.Content);
    }

    [Fact]
    public async Task Create_matter_requires_a_title_and_otherwise_creates_it()
    {
        var svc = new FakeMatters();
        var tool = new CreateMatterTool(new PassThroughScope(), svc);

        Assert.True((await tool.InvokeAsync(NoArgs, Ctx)).IsError);

        var ok = await tool.InvokeAsync(JsonSerializer.SerializeToElement(new { title = "Yeni dosya" }), Ctx);
        Assert.False(ok.IsError);
        Assert.Contains("Yeni dosya", ok.Content);
        Assert.Single(svc.Created);
    }

    [Fact]
    public async Task Get_matter_returns_detail_or_not_found_and_validates_the_id()
    {
        var found = M("Bulunan dosya");
        var tool = new GetMatterTool(new PassThroughScope(), new FakeMatters(found));

        Assert.True((await tool.InvokeAsync(JsonSerializer.SerializeToElement(new { matter_id = "not-a-guid" }), Ctx)).IsError);
        Assert.Contains("bulunamadı", (await tool.InvokeAsync(JsonSerializer.SerializeToElement(new { matter_id = Guid.NewGuid().ToString() }), Ctx)).Content);
        Assert.Contains("Bulunan dosya", (await tool.InvokeAsync(JsonSerializer.SerializeToElement(new { matter_id = found.Id.ToString() }), Ctx)).Content);
    }

    [Fact]
    public async Task Create_client_requires_a_name_and_defaults_the_kind()
    {
        var svc = new FakeClients();
        var tool = new CreateClientTool(new PassThroughScope(), svc);

        Assert.True((await tool.InvokeAsync(NoArgs, Ctx)).IsError);

        var ok = await tool.InvokeAsync(JsonSerializer.SerializeToElement(new { name = "Ahmet Yılmaz" }), Ctx);
        Assert.False(ok.IsError);
        Assert.Single(svc.Created);
        Assert.Equal("individual", svc.Created[0].Kind);
    }

    [Fact]
    public async Task List_clients_returns_the_offices_clients()
    {
        var tool = new ListClientsTool(new PassThroughScope(),
            new FakeClients(new Client(Guid.CreateVersion7(), Guid.NewGuid(), "Mehmet Demir", "individual", null, null, null, DateTimeOffset.UtcNow)));
        var result = await tool.InvokeAsync(NoArgs, Ctx);
        Assert.Contains("Mehmet Demir", result.Content);
    }
}
