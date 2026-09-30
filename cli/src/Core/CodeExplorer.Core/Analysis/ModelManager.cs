using System.Net.Http;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Core.Analysis;

public static class ModelManager
{
    public const string ModelFileName = "ce-intent-v2-q4_k_m.gguf";
    public const string DefaultDownloadUrl = "https://huggingface.co/vmikhailov77/code-intent/resolve/main/ce-intent-v2-q4_k_m.gguf";

    public static string DefaultCacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeexplorer", "models");

    public static string DefaultModelPath =>
        Path.Combine(DefaultCacheDirectory, ModelFileName);

    public static string? ResolveModelPath(string? hostWorkspacePath = null)
    {
        // 1. Explicit env var
        var envPath = Environment.GetEnvironmentVariable("CODE_INTENT_MODEL_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            return Path.GetFullPath(envPath);
        }

        // 2. Default user profile cache
        if (File.Exists(DefaultModelPath))
        {
            return DefaultModelPath;
        }

        // 3. Local workspace / dev paths
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(hostWorkspacePath))
        {
            candidates.Add(Path.Combine(hostWorkspacePath, "..", "code-intent-distill", "models", ModelFileName));
            candidates.Add(Path.Combine(hostWorkspacePath, "..", "..", "code-intent-distill", "models", ModelFileName));
        }

        candidates.Add(Path.Combine(Environment.CurrentDirectory, "..", "code-intent-distill", "models", ModelFileName));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "code-intent-distill", "models", ModelFileName));

        foreach (var cand in candidates)
        {
            try
            {
                var full = Path.GetFullPath(cand);
                if (File.Exists(full))
                {
                    return full;
                }
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        return null;
    }

    public static (bool Exists, string? Path, long SizeBytes) GetModelStatus(string? hostWorkspacePath = null)
    {
        var resolved = ResolveModelPath(hostWorkspacePath);
        if (resolved != null && File.Exists(resolved))
        {
            var info = new FileInfo(resolved);
            return (true, resolved, info.Length);
        }

        return (false, null, 0);
    }

    public static async Task<string?> EnsureModelAvailableAsync(ParsingContext ctx, CancellationToken cancellationToken = default)
    {
        var existing = ResolveModelPath(ctx.HostWorkspacePath);
        if (existing != null)
        {
            return existing;
        }

        var downloadUrl = Environment.GetEnvironmentVariable("CODE_INTENT_DOWNLOAD_URL") ?? DefaultDownloadUrl;
        var autoDownload = Environment.GetEnvironmentVariable("CODE_INTENT_AUTO_DOWNLOAD") == "1";

        if (!autoDownload)
        {
            ctx.Log($"[CodeIntent] Architectural intent model '{ModelFileName}' (~940MB) not found.");
            ctx.Log($"[CodeIntent] Run 'ce model download' or set CODE_INTENT_AUTO_DOWNLOAD=1 to download it.");
            return null;
        }

        return await DownloadModelAsync(downloadUrl, DefaultModelPath, ctx, progress: null, cancellationToken: cancellationToken);
    }

    public static async Task<string?> DownloadModelAsync(
        string downloadUrl,
        string destinationPath,
        ParsingContext? ctx = null,
        IProgress<(long DownloadedBytes, long TotalBytes)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var tempPath = destinationPath + ".downloading";
        ctx?.Log($"[CodeIntent] Downloading {ModelFileName} from {downloadUrl}...");

        try
        {
            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromHours(1) })
            {
                using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead,
                           cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    {
                        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
                                         FileShare.None, 81920, true))
                        {
                            var buffer = new byte[81920];
                            long downloadedBytes = 0;
                            int bytesRead;
                            var lastReportedMb = 0L;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                                downloadedBytes += bytesRead;
                                progress?.Report((downloadedBytes, totalBytes));

                                var currentMb = downloadedBytes / (1024 * 1024);

                                if (currentMb - lastReportedMb >= 50)
                                {
                                    lastReportedMb = currentMb;

                                    if (totalBytes > 0)
                                    {
                                        var pct = (double)downloadedBytes / totalBytes * 100.0;

                                        ctx?.Log(
                                            $"[CodeIntent] Downloading model: {currentMb} MB / {totalBytes / (1024 * 1024)} MB ({pct:F0}%)...");
                                    }
                                    else
                                    {
                                        ctx?.Log($"[CodeIntent] Downloading model: {currentMb} MB...");
                                    }
                                }
                            }
                        }

                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }

                        File.Move(tempPath, destinationPath);
                        ctx?.Log($"[CodeIntent] Successfully downloaded model to '{destinationPath}'.");
                        return destinationPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ctx?.LogWarning($"[CodeIntent] Failed to download model: {ex.Message}");
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* Ignore */ }
            return null;
        }
    }
}
