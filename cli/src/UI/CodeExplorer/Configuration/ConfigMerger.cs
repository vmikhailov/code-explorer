using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration;

public enum MergeResultStatus
{
    Created,
    Updated,
    Removed,
    Unchanged,
    FileNotFound
}

public record MergeResult(MergeResultStatus Status, string FilePath, string? Details = null);

public static class ConfigMerger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static MergeResult Merge(
        string filePath,
        string serverKey,
        string sectionKey,
        JsonObject serverConfig,
        bool remove = false,
        bool dryRun = false)
    {
        var fileExists = File.Exists(filePath);

        if (remove)
        {
            if (!fileExists)
            {
                return new MergeResult(MergeResultStatus.FileNotFound, filePath, "Config file does not exist.");
            }

            try
            {
                var content = File.ReadAllText(filePath);
                var rootNode = JsonNode.Parse(content) as JsonObject;
                if (rootNode == null)
                {
                    return new MergeResult(MergeResultStatus.Unchanged, filePath, "Invalid JSON structure.");
                }

                if (rootNode[sectionKey] is JsonObject section && section.ContainsKey(serverKey))
                {
                    section.Remove(serverKey);

                    if (!dryRun)
                    {
                        CreateBackup(filePath);
                        var updatedJson = rootNode.ToJsonString(JsonOptions);
                        File.WriteAllText(filePath, updatedJson + "\n");
                    }
                    return new MergeResult(MergeResultStatus.Removed, filePath, $"Removed '{serverKey}' from '{sectionKey}'.");
                }

                return new MergeResult(MergeResultStatus.Unchanged, filePath, $"'{serverKey}' was not present in '{sectionKey}'.");
            }
            catch (Exception ex)
            {
                return new MergeResult(MergeResultStatus.Unchanged, filePath, $"Error reading file: {ex.Message}");
            }
        }

        // Add or Update
        JsonObject root;
        if (fileExists)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                root = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        if (root[sectionKey] is not JsonObject sectionObj)
        {
            sectionObj = new JsonObject();
            root[sectionKey] = sectionObj;
        }

        var isUpdate = sectionObj.ContainsKey(serverKey);
        sectionObj[serverKey] = serverConfig.DeepClone();

        if (!dryRun)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (fileExists)
            {
                CreateBackup(filePath);
            }

            var updatedJson = root.ToJsonString(JsonOptions);
            File.WriteAllText(filePath, updatedJson + "\n");
        }

        var status = fileExists ? (isUpdate ? MergeResultStatus.Updated : MergeResultStatus.Created) : MergeResultStatus.Created;
        return new MergeResult(status, filePath, isUpdate ? $"Updated '{serverKey}' in '{sectionKey}'." : $"Added '{serverKey}' to '{sectionKey}'.");
    }

    private static void CreateBackup(string filePath)
    {
        try
        {
            var bakPath = filePath + ".bak";
            File.Copy(filePath, bakPath, overwrite: true);
        }
        catch
        {
            // Ignore backup failures
        }
    }
}
