namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private static void ResolveInstantiatesReference(
        Reference refItem,
        Layer5Context ctx,
        SymbolLookups symbols,
        ImportLookups imports,
        List<Relationship> referenceRelationships)
    {
        var rawTarget = refItem.TargetName?.Trim('\'', '"', '`');
        if (string.IsNullOrWhiteSpace(rawTarget)) return;

        var callerFile = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);
        var callerProj = ConstantRegistry.ExtractProjectName(callerFile);

        var cleanAsset = rawTarget.Replace('\\', '/');
        var assetFileName = Path.GetFileNameWithoutExtension(cleanAsset);
        if (string.IsNullOrWhiteSpace(assetFileName)) return;

        // If target was a viewId (e.g. codeexplorer.views.graphInspector), extract last segment
        if (assetFileName.Contains('.'))
        {
            assetFileName = assetFileName[(assetFileName.LastIndexOf('.') + 1)..];
        }

        // Find candidate source files in the project/workspace that match the asset
        var candidateFiles = new List<string>();
        var filesToScan = callerProj != null && symbols.FilesByProject.TryGetValue(callerProj, out var pFiles)
            ? (IEnumerable<string>)pFiles
            : symbols.KnownFiles;

        foreach (var file in filesToScan)
        {
            var norm = file.Replace('\\', '/');
            if (callerProj != null)
            {
                var fileProj = ConstantRegistry.ExtractProjectName(norm);
                if (!string.Equals(fileProj, callerProj, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // Exclude build/dist output files
            if (norm.Contains("/dist/") || norm.Contains("/out/") || norm.Contains("/build/") || norm.Contains("/.build/"))
            {
                continue;
            }

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(norm);
            var parentDir = Path.GetFileName(Path.GetDirectoryName(norm));

            if (string.Equals(fileNameWithoutExt, assetFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parentDir, assetFileName, StringComparison.OrdinalIgnoreCase) ||
                (assetFileName.Contains("webview", StringComparison.OrdinalIgnoreCase) && norm.Contains("webview", StringComparison.OrdinalIgnoreCase)))
            {
                candidateFiles.Add(norm);
            }
        }

        if (candidateFiles.Count == 0) return;

        // Sort candidate files: entry files (index / main) first
        var sortedFiles = candidateFiles
            .OrderByDescending(f => f.Contains("index.", StringComparison.OrdinalIgnoreCase) || f.Contains("main.", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var primaryFile = sortedFiles[0];

        // Resolve caller host class ID if refItem.ScopeSymbolId is a method
        var hostId = refItem.ScopeSymbolId;
        if (callerFile != null)
        {
            var normCaller = callerFile.IndexOf('\\') >= 0 ? callerFile.Replace('\\', '/') : callerFile;
            if (symbols.SymbolsByFile.TryGetValue(normCaller, out var callerSyms))
            {
                for (var i = 0; i < callerSyms.Count; i++)
                {
                    var (kind, name, symId) = callerSyms[i];
                    if (kind == "Class" &&
                        (refItem.ScopeSymbolId.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                         refItem.ScopeSymbolId.StartsWith(symId, StringComparison.OrdinalIgnoreCase)))
                    {
                        hostId = symId;
                        break;
                    }
                }
            }
        }

        var targetNodeIds = new HashSet<string>(StringComparer.Ordinal);

        // 1. Look for EntryPoint nodes in the candidate files
        foreach (var file in sortedFiles)
        {
            if (!symbols.SymbolsByFile.TryGetValue(file, out var fileSyms)) continue;
            for (var i = 0; i < fileSyms.Count; i++)
            {
                var (kind, _, symId) = fileSyms[i];
                if (kind == OntologyConstants.NodeLabels.EntryPoint)
                {
                    targetNodeIds.Add(symId);
                }
            }
        }

        // 2. Look for Class / Component / Function nodes in the candidate files
        foreach (var file in sortedFiles)
        {
            if (!symbols.SymbolsByFile.TryGetValue(file, out var fileSyms)) continue;
            for (var i = 0; i < fileSyms.Count; i++)
            {
                var (kind, _, symId) = fileSyms[i];
                if (kind is "Class" or "Component" || kind == OntologyConstants.NodeLabels.Function)
                {
                    targetNodeIds.Add(symId);
                    if (targetNodeIds.Count >= 2) break;
                }
            }
            if (targetNodeIds.Count >= 2) break;
        }

        // 3. Fallback to FunctionSymbolsByFile
        if (targetNodeIds.Count == 0 && symbols.SymbolsByFile.Count == 0)
        {
            foreach (var file in sortedFiles)
            {
                var func = symbols.FunctionSymbolsByFile.FirstOrDefault(kvp => kvp.Key.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(func.Value))
                {
                    targetNodeIds.Add(func.Value);
                    break;
                }
            }
        }

        if (targetNodeIds.Count == 0) return;

        foreach (var targetId in targetNodeIds)
        {
            var rel = new Relationship(hostId, targetId, OntologyConstants.Relationships.Instantiates)
            {
                Category = "ui_host",
                DependencyType = "instantiates",
                Asset = cleanAsset,
                FilePath = callerFile ?? "",
                TargetFile = primaryFile,
                EvidenceKind = "instantiates",
                Line = refItem.Line > 0 ? refItem.Line : 0
            };
            referenceRelationships.Add(rel);
        }
    }
}

