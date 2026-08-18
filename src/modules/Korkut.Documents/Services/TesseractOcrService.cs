using System.Diagnostics;
using Korkut.Contract.Documents.OCR;
using Korkut.Core;

namespace Korkut.Documents.Services;

/// <summary>
/// Real OCR backed by the native Tesseract CLI (with poppler's <c>pdftoppm</c> to rasterise PDF
/// pages). Images are OCR'd directly; PDFs are rendered to per-page PNGs, each OCR'd, and the text
/// concatenated. Enabled at deploy by installing the native binaries (see docker/Dockerfile.ocr) and
/// registering this over the <see cref="DisabledOcrService"/> scaffold. Languages default to
/// Turkish+English and are overridable via <c>KORKUT_OCR_LANGS</c>.
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    /// <summary>Ceiling on concurrent per-page tesseract processes (see <c>OcrPdfAsync</c>).</summary>
    private const int MaxPageParallelism = 4;

    private readonly string _langs;
    private readonly int _dpi;
    private readonly TimeSpan _perPageTimeout;

    public TesseractOcrService(string? langs = null, int dpi = 200, TimeSpan? perPageTimeout = null)
    {
        _langs = string.IsNullOrWhiteSpace(langs) ? "tur+eng" : langs!;
        _dpi = dpi <= 0 ? 200 : dpi;
        _perPageTimeout = perPageTimeout ?? TimeSpan.FromMinutes(2);
    }

    public bool IsAvailable => true;

    public async Task<OcrServiceResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        var work = Directory.CreateTempSubdirectory("korkut-ocr");
        try
        {
            var input = Path.Combine(work.FullName, "input");
            await using (var fs = File.Create(input))
            {
                await content.CopyToAsync(fs, ct);
            }

            var isPdf = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || await LooksLikePdfAsync(input, ct);
            return isPdf
                ? await OcrPdfAsync(work.FullName, input, ct)
                : new OcrServiceResult(await OcrImageAsync(input, ct), 1);
        }
        finally
        {
            try
            {
                work.Delete(recursive: true);
            }
            catch
            {
                // Best-effort temp cleanup; a leftover temp dir must never fail the request.
            }
        }
    }

    // Rasterise every PDF page to a PNG (pdftoppm), OCR each, and join the page texts.
    private async Task<OcrServiceResult> OcrPdfAsync(string workDir, string pdfPath, CancellationToken ct)
    {
        var prefix = Path.Combine(workDir, "page");
        await RunAsync("pdftoppm", ["-png", "-r", _dpi.ToString(), pdfPath, prefix], ct);

        var pages = Directory.GetFiles(workDir, "page*.png").OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (pages.Count == 0)
        {
            throw new OcrUnavailableException("PDF sayfaları görüntüye dönüştürülemedi.");
        }

        // Recognition is CPU-bound and per-page independent, and a lawyer waits on this inside a
        // chat turn — so pages run concurrently rather than one after another (a 9-page scan is
        // otherwise ~9x a single page). Capped: the host serves every tenant's OCR, so one
        // document must not take the whole box. Results are written by index, keeping page order.
        var texts = new string[pages.Count];
        var parallelism = Math.Clamp(System.Environment.ProcessorCount, 1, MaxPageParallelism);
        await Parallel.ForEachAsync(
            Enumerable.Range(0, pages.Count),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
            async (index, token) => texts[index] = await OcrImageAsync(pages[index], token));

        return new OcrServiceResult(string.Join("\n\n", texts).Trim(), pages.Count, texts);
    }

    // tesseract <image> stdout -l <langs> → recognised text on stdout. Sanitized here (the one
    // place raw OCR output enters this service) so neither this nor any future caller that
    // persists it to Postgres can be taken down by a stray control byte in the scan.
    private async Task<string> OcrImageAsync(string imagePath, CancellationToken ct)
        => PostgresTextSanitizer.StripInvalidChars((await RunAsync("tesseract", [imagePath, "stdout", "-l", _langs], ct)).Trim());

    private static async Task<bool> LooksLikePdfAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        // Read in a loop — Stream.ReadAsync may return fewer bytes than requested in a single
        // call even when more data follows, which could misdetect a real PDF as non-PDF.
        var head = new byte[4];
        var filled = 0;
        while (filled < head.Length)
        {
            var read = await fs.ReadAsync(head.AsMemory(filled), ct);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled == head.Length && head[0] == '%' && head[1] == 'P' && head[2] == 'D' && head[3] == 'F';
    }

    private async Task<string> RunAsync(string fileName, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new OcrUnavailableException($"OCR aracı '{fileName}' çalıştırılamadı: {ex.Message}");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_perPageTimeout);

        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new OcrUnavailableException($"OCR aracı '{fileName}' zaman aşımına uğradı.");
        }

        if (process.ExitCode != 0)
        {
            throw new OcrUnavailableException($"OCR aracı '{fileName}' hata verdi ({process.ExitCode}): {(await stderr).Trim()}");
        }

        return await stdout;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Nothing more we can do; the OS reaps it.
        }
    }
}
