namespace CodeExplorer.Core.Parser;

public readonly record struct StreamedFileResult(
    bool SkipByContent,
    string? ContentHash,
    int? LineCount,
    string? ArtifactRole,
    string? DecodedText);
