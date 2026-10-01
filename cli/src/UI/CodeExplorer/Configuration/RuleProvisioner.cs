namespace CodeExplorer.Configuration;

public enum RuleResultStatus
{
    Created,
    Updated,
    Removed,
    Unchanged
}

public record RuleResult(RuleResultStatus Status, string FilePath, string? Details = null);

public static class RuleProvisioner
{
    public static RuleResult Provision(RuleConfigInfo info, bool remove = false, bool dryRun = false)
    {
        var fileExists = File.Exists(info.FilePath);

        if (remove)
        {
            if (!fileExists)
            {
                return new RuleResult(RuleResultStatus.Unchanged, info.FilePath, "Rule file does not exist.");
            }

            var text = File.ReadAllText(info.FilePath);
            var headerIdx = text.IndexOf(info.HeaderMarker, StringComparison.Ordinal);
            if (headerIdx < 0)
            {
                return new RuleResult(RuleResultStatus.Unchanged, info.FilePath, "CodeExplorer rule section not found.");
            }

            // Find where this section ends (next header or end of file)
            var nextHeaderIdx = text.IndexOf("\n## ", headerIdx + info.HeaderMarker.Length, StringComparison.Ordinal);
            if (nextHeaderIdx < 0)
            {
                nextHeaderIdx = text.IndexOf("\n# ", headerIdx + info.HeaderMarker.Length, StringComparison.Ordinal);
            }

            string updatedText;
            if (nextHeaderIdx > 0)
            {
                updatedText = (text[..headerIdx].TrimEnd() + "\n\n" + text[nextHeaderIdx..].TrimStart()).Trim();
            }
            else
            {
                updatedText = text[..headerIdx].Trim();
            }

            if (!dryRun)
            {
                if (string.IsNullOrWhiteSpace(updatedText))
                {
                    File.Delete(info.FilePath);
                }
                else
                {
                    File.WriteAllText(info.FilePath, updatedText + "\n");
                }
            }

            return new RuleResult(RuleResultStatus.Removed, info.FilePath, "Removed CodeExplorer rules section.");
        }

        // Add or Update
        if (!fileExists)
        {
            if (!dryRun)
            {
                var dir = Path.GetDirectoryName(info.FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(info.FilePath, info.Content.Trim() + "\n");
            }
            return new RuleResult(RuleResultStatus.Created, info.FilePath, "Created agent rules file.");
        }

        var existingText = File.ReadAllText(info.FilePath);
        var existingHeaderIdx = existingText.IndexOf(info.HeaderMarker, StringComparison.Ordinal);

        if (existingHeaderIdx >= 0)
        {
            // Replace existing section
            var nextHeaderIdx = existingText.IndexOf("\n## ", existingHeaderIdx + info.HeaderMarker.Length, StringComparison.Ordinal);
            if (nextHeaderIdx < 0)
            {
                nextHeaderIdx = existingText.IndexOf("\n# ", existingHeaderIdx + info.HeaderMarker.Length, StringComparison.Ordinal);
            }

            string updated;
            if (nextHeaderIdx > 0)
            {
                var before = existingText[..existingHeaderIdx].TrimEnd();
                var after = existingText[nextHeaderIdx..].TrimStart();
                updated = (before + "\n\n" + info.Content.Trim() + "\n\n" + after).Trim();
            }
            else
            {
                var before = existingText[..existingHeaderIdx].TrimEnd();
                updated = (before + "\n\n" + info.Content.Trim()).Trim();
            }

            if (!dryRun)
            {
                File.WriteAllText(info.FilePath, updated + "\n");
            }
            return new RuleResult(RuleResultStatus.Updated, info.FilePath, "Updated CodeExplorer rules section.");
        }
        else
        {
            // Append
            var updated = (existingText.TrimEnd() + "\n\n" + info.Content.Trim()).Trim() + "\n";
            if (!dryRun)
            {
                File.WriteAllText(info.FilePath, updated);
            }
            return new RuleResult(RuleResultStatus.Updated, info.FilePath, "Appended CodeExplorer rules section.");
        }
    }
}
