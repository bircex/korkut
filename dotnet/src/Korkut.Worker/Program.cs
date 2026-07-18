using Korkut.Infrastructure.Observability;
using Korkut.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Same OTel traces/metrics + redacted logs as the API (KDM-007). Jobs carry the
// shared ActivitySource so worker spans correlate with the request that queued them.
builder.AddKorkutObservability("Korkut.Worker");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
