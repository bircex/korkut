using Korkut.Contract.Documents;
using Korkut.Core;
using Korkut.Documents.Repositories;
using Korkut.Contract.Documents.OCR;
using Korkut.Contract.Documents.Storage;
using Korkut.Contract.Upload.Malware;
using Korkut.Contract.Upload.SafeUpload;
using Korkut.Contract.Upload.Validator;
using Korkut.Documents.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;

namespace Korkut.Documents.Storage;

/// <summary>
/// Registers object storage + the document pipeline services (KDM-025..028).
/// <c>KORKUT_STORAGE</c> picks the store: <c>filesystem</c> (a directory, <c>KORKUT_STORAGE_PATH</c>)
/// or <c>s3</c> (MinIO or any S3-compatible server, <c>KORKUT_MINIO_*</c>). Unset, an install that
/// already names an S3 endpoint keeps using it and everything else gets the filesystem, so an
/// existing deployment never silently starts writing somewhere new. Buckets are ensured
/// best-effort on startup so a host without storage still boots (uploads then fail loudly,
/// non-document endpoints are unaffected).
/// </summary>
public static partial class StorageSetup
{
    public const string DefaultStoragePath = "/var/lib/korkut/objects";

    public static IServiceCollection AddKorkutStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = configuration["KORKUT_STORAGE"]?.Trim().ToLowerInvariant();
        mode ??= string.IsNullOrWhiteSpace(configuration["KORKUT_MINIO_ENDPOINT"]) ? "filesystem" : "s3";
        switch (mode)
        {
            case "filesystem":
                AddFileSystemStore(services, configuration);
                break;
            case "s3":
                AddS3Store(services, configuration);
                break;
            default:
                throw new InvalidOperationException($"KORKUT_STORAGE must be 'filesystem' or 's3', not '{mode}'.");
        }

        // Upload pipeline primitives.
        services.AddSingleton<IUploadValidatorService, UploadValidatorService>();
        services.AddSingleton<IMalwareScanService, MalwareScannerService>();
        AddTextExtraction(services, configuration);

        // Document services (scoped — they take the per-request DbContext). The upload,
        // relation and lifecycle pipelines are what the Documents use-case orchestrates.
        services.AddScoped<DocumentStorageService>();
        services.AddScoped<SafeUploadService>();
        services.AddScoped<ISafeUploadService>(sp => sp.GetRequiredService<SafeUploadService>());
        services.AddScoped<DocumentLifecycleService>();
        services.AddScoped<IDocumentLifecycleService>(sp => sp.GetRequiredService<DocumentLifecycleService>());
        services.AddScoped<DocumentRelationService>();
        services.AddScoped<IDocumentRelationService>(sp => sp.GetRequiredService<DocumentRelationService>());
        services.AddScoped<ExtractionService>();
        services.AddScoped<DocumentTextService>();
        services.AddScoped<Korkut.Documents.Repositories.IDocumentRepository, Korkut.Documents.Repositories.DocumentRepository>();
        services.AddScoped<IDocumentFileService, DocumentFileService>();

        services.AddHostedService<EnsureBucketsHostedService>();
        return services;
    }

    private static void AddFileSystemStore(IServiceCollection services, IConfiguration configuration)
    {
        var path = configuration["KORKUT_STORAGE_PATH"];
        var secret = configuration["KORKUT_SECRET"] ?? string.Empty;
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(sp => new ObjectUrlSigner(secret, sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new FileSystemObjectStoreService(
            string.IsNullOrWhiteSpace(path) ? DefaultStoragePath : path, sp.GetRequiredService<ObjectUrlSigner>()));
        services.AddSingleton<IObjectStoreService>(sp => sp.GetRequiredService<FileSystemObjectStoreService>());
    }

    private static void AddS3Store(IServiceCollection services, IConfiguration configuration)
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
        services.AddSingleton<IObjectStoreService, MinioObjectStoreService>();
    }

    /// <summary>
    /// Text extraction, and the OCR fallback behind it. The plain extractor only reads an EMBEDDED
    /// text layer, so a scanned document (no text layer at all) needs the OCR host: when
    /// <c>KORKUT_OCR_URL</c> points at Korkut.Ocr the extractor is wrapped so those documents are
    /// recognised instead of coming back empty. With no URL configured the registration is exactly
    /// what it was — text-layer only — so a host without OCR still behaves and boots.
    /// </summary>
    private static void AddTextExtraction(IServiceCollection services, IConfiguration configuration)
    {
        var ocrUrl = configuration["KORKUT_OCR_URL"]?.Trim();
        if (string.IsNullOrWhiteSpace(ocrUrl))
        {
            services.AddSingleton<ITextExtractorService, PlainTextExtractorService>();
            return;
        }

        services.AddHttpContextAccessor();
        services.AddHttpClient(OcrHttpClient.Name, client => client.Timeout = OcrHttpClient.Timeout);
        services.AddSingleton<IOcrService>(sp => new HttpOcrService(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            ocrUrl));
        services.AddSingleton<ITextExtractorService>(sp => new OcrFallbackTextExtractorService(
            new PlainTextExtractorService(sp.GetRequiredService<ILogger<PlainTextExtractorService>>()),
            sp.GetRequiredService<IOcrService>(),
            sp.GetRequiredService<ILogger<OcrFallbackTextExtractorService>>()));
    }

    private sealed partial class EnsureBucketsHostedService(IObjectStoreService storeService, ILogger<EnsureBucketsHostedService> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await storeService.EnsureBucketsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort: a host without reachable storage still starts; uploads
                // will surface the failure when actually attempted.
                BucketsUnavailable(logger, ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not ensure object-storage buckets at startup.")]
        private static partial void BucketsUnavailable(ILogger logger, Exception ex);
    }
}
