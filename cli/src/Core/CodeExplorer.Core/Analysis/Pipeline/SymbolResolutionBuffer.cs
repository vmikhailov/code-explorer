using System.Collections.Concurrent;

namespace CodeExplorer.Core.Parser;

public sealed class SymbolResolutionBuffer
{
    private ConcurrentDictionary<string, byte>.AlternateLookup<ReadOnlySpan<char>>? _callableSpanLookup;
    private ConcurrentDictionary<string, byte>.AlternateLookup<ReadOnlySpan<char>>? _typeSpanLookup;

    public Dictionary<(string Kind, string Name), string> GlobalSymbols { get; }
    public Dictionary<SymbolKey, string> GlobalSymbolKeys { get; } = new();
    public Dictionary<(string FilePath, string Kind, string Name), string> FileScopedSymbols { get; } = new();
    public List<(string FilePath, string Kind, string Name, string Id)> OverloadedFileScopedSymbols { get; } = [];
    public List<Reference> GlobalReferences { get; }
    public ConcurrentDictionary<string, SymbolScopeInfo> SymbolScopes { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, string> FileNamespaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, HashSet<string>> FileImportedNamespaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, byte> KnownWorkspaceCallableNames { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, byte> KnownWorkspaceTypeNames { get; } = new(StringComparer.Ordinal);
    public List<RawImport> RawImports { get; } = [];
    public List<RawVariable> RawVariables { get; } = [];
    public List<RawTypeBinding> RawTypeBindings { get; } = [];
    public List<ProjectScanSummary> ProjectSummaries { get; } = [];
    public HashSet<string> KnownFunctionIds { get; } = new(StringComparer.Ordinal);
    public bool HasWorkspaceSymbolFilter { get; set; }

    internal ConcurrentDictionary<string, byte>.AlternateLookup<ReadOnlySpan<char>> KnownWorkspaceCallableSpanLookup =>
        _callableSpanLookup ??= KnownWorkspaceCallableNames.GetAlternateLookup<ReadOnlySpan<char>>();

    internal ConcurrentDictionary<string, byte>.AlternateLookup<ReadOnlySpan<char>> KnownWorkspaceTypeSpanLookup =>
        _typeSpanLookup ??= KnownWorkspaceTypeNames.GetAlternateLookup<ReadOnlySpan<char>>();

    public SymbolResolutionBuffer(
        Dictionary<(string Kind, string Name), string>? globalSymbols = null,
        List<Reference>? globalReferences = null)
    {
        GlobalSymbols = globalSymbols ?? new Dictionary<(string Kind, string Name), string>();
        GlobalReferences = globalReferences ?? [];
    }

    public void AddGlobalSymbol(string kind, string name, string id)
    {
        lock (GlobalSymbols)
        {
            if (!IsSecondaryOverloadSymbolId(id) || !GlobalSymbols.ContainsKey((kind, name)))
            {
                GlobalSymbols[(kind, name)] = id;
                GlobalSymbolKeys[new SymbolKey(kind, name)] = id;
            }
        }
        RegisterSymbolByKind(kind, name);
    }

    public void AddGlobalSymbol(NodeKind kind, string name, string id)
    {
        var label = kind.ToCypherLabel();
        lock (GlobalSymbols)
        {
            if (!IsSecondaryOverloadSymbolId(id) || !GlobalSymbols.ContainsKey((label, name)))
            {
                GlobalSymbols[(label, name)] = id;
                GlobalSymbolKeys[new SymbolKey(kind, name)] = id;
            }
        }
        RegisterSymbolByKind(label, name);
    }

    public void AddGlobalSymbol(SymbolKey key, string id)
    {
        var label = key.Kind.ToCypherLabel();
        lock (GlobalSymbols)
        {
            if (!IsSecondaryOverloadSymbolId(id) || !GlobalSymbols.ContainsKey((label, key.Name)))
            {
                GlobalSymbols[(label, key.Name)] = id;
                GlobalSymbolKeys[key] = id;
            }
        }
        RegisterSymbolByKind(label, key.Name);
    }

    public void AddFileScopedSymbol(string filePath, string kind, string name, string id)
    {
        lock (FileScopedSymbols)
        {
            if (!IsSecondaryOverloadSymbolId(id) || !FileScopedSymbols.ContainsKey((filePath, kind, name)))
            {
                FileScopedSymbols[(filePath, kind, name)] = id;
            }
            else
            {
                OverloadedFileScopedSymbols.Add((filePath, kind, name, id));
            }
        }
        RegisterSymbolByKind(kind, name);
    }

    public void AddGlobalReferences(IEnumerable<Reference> references)
    {
        lock (GlobalReferences)
        {
            GlobalReferences.AddRange(references);
        }
    }

    public void RegisterKnownCallableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        KnownWorkspaceCallableNames.TryAdd(name, 0);
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < name.Length)
        {
            KnownWorkspaceCallableNames.TryAdd(name[(lastDot + 1)..], 0);
        }
    }

    public void RegisterKnownTypeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        KnownWorkspaceTypeNames.TryAdd(name, 0);
        KnownWorkspaceCallableNames.TryAdd(name, 0);
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < name.Length)
        {
            var shortName = name[(lastDot + 1)..];
            KnownWorkspaceTypeNames.TryAdd(shortName, 0);
            KnownWorkspaceCallableNames.TryAdd(shortName, 0);
        }
    }

    public bool IsKnownWorkspaceCallable(string? targetName, bool isSubtreeScan)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return false;
        if (!HasWorkspaceSymbolFilter || isSubtreeScan) return true;
        if (KnownWorkspaceCallableNames.ContainsKey(targetName)) return true;
        var span = targetName.AsSpan();
        if (span.StartsWith("new ", StringComparison.Ordinal))
        {
            span = span[4..].TrimStart();
            var lt = span.IndexOf('<');
            if (lt > 0) span = span[..lt].TrimEnd();
            if (KnownWorkspaceCallableSpanLookup.ContainsKey(span)) return true;
        }
        var lastDot = span.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < span.Length)
        {
            if (KnownWorkspaceCallableSpanLookup.ContainsKey(span[(lastDot + 1)..])) return true;
        }
        return false;
    }

    public bool IsKnownWorkspaceType(string? targetName, bool isSubtreeScan)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return false;
        if (!HasWorkspaceSymbolFilter || isSubtreeScan) return true;
        if (KnownWorkspaceTypeNames.ContainsKey(targetName)) return true;
        var lastDot = targetName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < targetName.Length)
        {
            if (KnownWorkspaceTypeSpanLookup.ContainsKey(targetName.AsSpan(lastDot + 1))) return true;
        }
        return false;
    }

    public void EvictResolutionBuffers(bool lightweightAstMode)
    {
        if (lightweightAstMode) return;

        GlobalReferences.Clear();
        GlobalReferences.TrimExcess();
        RawImports.Clear();
        RawImports.TrimExcess();
        RawVariables.Clear();
        RawVariables.TrimExcess();
        RawTypeBindings.Clear();
        RawTypeBindings.TrimExcess();
        FileScopedSymbols.Clear();
        FileScopedSymbols.TrimExcess();
        OverloadedFileScopedSymbols.Clear();
        OverloadedFileScopedSymbols.TrimExcess();
        SymbolScopes.Clear();
        FileNamespaces.Clear();
        FileImportedNamespaces.Clear();
        KnownWorkspaceCallableNames.Clear();
        KnownWorkspaceTypeNames.Clear();
        GlobalSymbolKeys.Clear();
        GlobalSymbolKeys.TrimExcess();
        ProjectSummaries.Clear();
        ProjectSummaries.TrimExcess();
    }

    public void EvictGlobalSymbolsAndFunctions(bool lightweightAstMode)
    {
        if (lightweightAstMode) return;

        GlobalSymbols.Clear();
        GlobalSymbols.TrimExcess();
        KnownFunctionIds.Clear();
        KnownFunctionIds.TrimExcess();
    }

    private void RegisterSymbolByKind(string kind, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (kind is OntologyConstants.NodeLabels.Type or "Class" or "Interface" or "Struct" or "Enum")
        {
            RegisterKnownTypeName(name);
        }
        else if (kind is OntologyConstants.NodeLabels.Function or OntologyConstants.NodeLabels.Procedure or OntologyConstants.NodeLabels.Endpoint or OntologyConstants.NodeLabels.EntryPoint)
        {
            RegisterKnownCallableName(name);
        }
    }

    private static bool IsSecondaryOverloadSymbolId(string id) =>
        id.Contains('#', StringComparison.Ordinal);
}
