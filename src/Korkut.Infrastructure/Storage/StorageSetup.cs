using Korkut.Application.Documents;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;

namespace Korkut.Infrastructure.Storage;

/// <summary>
/// Registers MinIO object storage + the document pipeline services (KDM-025..028).
/// The MinIO client is built from env (never connects at construction, so container
/// validation is safe); buckets are ensured best-effort on startup so a host without
/// MinIO still boots (uploads then fail loudly, non-document endpoints are unaffected).
/// </summary>
public static partial class StorageSetup
{
    public static IServiceCollection AddKorkutStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["KORKUT_MINIO_ENDPOINT"] ?? "localhost:9000";
        var accessKey = configuration["KORKUT_MINIO_ACCESS_KEY"] ?? "minioadmin";
        var secretKey = configuration["KORKUT_MINIO_SECRET_KEY"] ?? "minioadmin";
        var secure = configuration["KORKUT_MINIO_SECURE"] == "1";
        var host = endpoint.Split(':', 2);

        services.AddSingleton<IMinioClient>(_ =>
        {
            var client = new MinioClient().WithEndpoint(host[0], host.Length > 1 ? int.Parse(host[1]) : 9000)
                .WithCredentials(accessKey, secretKey);
            if (secure)
            {
                client = client.WithSSL();
            }

            return client.Build();
        });
        services.AddSingleton<IObjectStore, MinioObjectStore>();

        // Upload pipeline primitives.
        services.AddSingleton<UploadValidator>();
        services.AddSingleton<IMalwareScanner, SignatureMalwareScanner>();
        services.AddSingleton<ITextExtractor, PlainTextExtractor>();

        // Document services (scoped — they take the per-request DbContext).
        services.AddScoped<DocumentService>();
        services.AddScoped<SecureUploadService>();
        services.AddScoped<DocumentLifecycleService>();
        services.AddScoped<DocumentRelationService>();
        services.AddScoped<ExtractionService>();

        services.AddHostedService<EnsureBucketsHostedService>();
        return services;
    }

    private sealed partial class EnsureBucketsHostedService(IObjectStore store, ILogger<EnsureBucketsHostedService> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await store.EnsureBucketsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort: a host without a reachable MinIO still starts; uploads
                // will surface the failure when actually attempted.
                BucketsUnavailable(logger, ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not ensure object-storage buckets at startup.")]
        private static partial void BucketsUnavailable(ILogger logger, Exception ex);
    }
}
