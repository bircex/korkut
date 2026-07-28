using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Infrastructure.Observability;
using Korkut.Core;
using Korkut.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Same OTel traces/metrics + redacted logs as the API (KDM-007).
builder.AddKorkutObservability("Korkut.Worker");

// Job infrastructure (KDM-008). The in-memory stores are transitional defaults so
// the loop is exercisable now; KDM-011 replaces them with the EF-backed PostgreSQL
// outbox/inbox/audit via the same interfaces, without touching the processor loop.
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<ITenantContext, AmbientTenantContext>();
builder.Services.AddSingleton<IRetryPolicy>(new ExponentialRetryPolicy());
builder.Services.AddSingleton<InMemoryOutboxStore>();
builder.Services.AddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>());
builder.Services.AddSingleton<IInboxStore, InMemoryInboxStore>();
builder.Services.AddSingleton<IJobAudit, InMemoryJobAudit>();
builder.Services.AddSingleton(new OutboxProcessorOptions());
// Job handlers are registered by domain modules in later KDM issues (IJobHandler).
builder.Services.AddHostedService<OutboxProcessorService>();

var host = builder.Build();
host.Run();
