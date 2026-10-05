namespace CodeExplorer.Parser.Android;

public record AndroidManifestInfo(
    string? PackageName,
    string? AppLabel,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<AndroidActivityInfo> Activities,
    IReadOnlyList<AndroidDeepLinkInfo> DeepLinks,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> Receivers
);

public record AndroidActivityInfo(
    string Name,
    bool IsLauncher,
    bool Exported
);

public record AndroidDeepLinkInfo(
    string Scheme,
    string? Host,
    string? Path,
    string Action,
    string ActivityName
);
