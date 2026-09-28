using CodeExplorer.Core.Analysis;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class ModelCommandHandler
{
    public static async Task<int> HandleAsync(ModelOptions opts)
    {
        var action = (opts.Action ?? "status").Trim().ToLowerInvariant();

        switch (action)
        {
            case "status":
            case "info":
                return ShowStatus();

            case "download":
            case "pull":
            case "get":
                return await DownloadModelAsync(opts.Force);

            default:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Unknown action '{opts.Action}'. Valid actions: status, download.");
                Console.ResetColor();
                return 1;
        }
    }

    private static int ShowStatus()
    {
        var (exists, path, sizeBytes) = ModelManager.GetModelStatus();
        Console.WriteLine($"Intent Model: {ModelManager.ModelFileName}");
        Console.WriteLine($"Default Path: {ModelManager.DefaultModelPath}");

        if (exists && path != null)
        {
            var sizeMb = sizeBytes / (1024.0 * 1024.0);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Status:       Present ({sizeMb:F1} MB)");
            Console.WriteLine($"Location:     {path}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Status:       Not downloaded");
            Console.WriteLine("Run 'ce model download' to download the architectural intent model (~940 MB).");
            Console.ResetColor();
        }

        return 0;
    }

    private static async Task<int> DownloadModelAsync(bool force)
    {
        var (exists, path, sizeBytes) = ModelManager.GetModelStatus();
        if (exists && path != null && !force)
        {
            var sizeMb = sizeBytes / (1024.0 * 1024.0);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Model is already downloaded at '{path}' ({sizeMb:F1} MB). Use --force to re-download.");
            Console.ResetColor();
            return 0;
        }

        var destination = ModelManager.DefaultModelPath;
        var url = Environment.GetEnvironmentVariable("CODE_INTENT_DOWNLOAD_URL") ?? ModelManager.DefaultDownloadUrl;

        Console.WriteLine($"Downloading model from {url} to {destination}...");

        var lastPercent = -1;
        var progress = new Progress<(long DownloadedBytes, long TotalBytes)>(p =>
        {
            if (p.TotalBytes > 0)
            {
                var pct = (int)((double)p.DownloadedBytes / p.TotalBytes * 100);
                if (pct != lastPercent)
                {
                    lastPercent = pct;
                    var dlMb = p.DownloadedBytes / (1024.0 * 1024.0);
                    var totMb = p.TotalBytes / (1024.0 * 1024.0);
                    Console.Write($"\rDownloading: {pct,3}% [{dlMb:F1} MB / {totMb:F1} MB]   ");
                }
            }
        });

        var result = await ModelManager.DownloadModelAsync(url, destination, progress: progress);
        Console.WriteLine();

        if (result != null && File.Exists(result))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Model successfully downloaded to {result}");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("\nFailed to download model.");
        Console.ResetColor();
        return 1;
    }
}
