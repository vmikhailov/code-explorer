namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private static void ResolveInstantiatesReference(
        Reference refItem,
        ParsingContext ctx,
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
        foreach (var file in symbols.KnownFiles)
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
            var normCaller = callerFile.Replace('\\', '/');
            foreach (var ((filePath, kind, name), symId) in ctx.FileScopedSymbols)
            {
                var norm = filePath.Replace('\\', '/');
                if (norm.Equals(normCaller, StringComparison.OrdinalIgnoreCase) && kind == "Class")
                {
                    if (refItem.ScopeSymbolId.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                        refItem.ScopeSymbolId.StartsWith(symId, StringComparison.OrdinalIgnoreCase))
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
            foreach (var ((filePath, kind, name), symId) in ctx.FileScopedSymbols)
            {
                var norm = filePath.Replace('\\', '/');
                if (norm.Equals(file, StringComparison.OrdinalIgnoreCase) && kind == OntologyConstants.NodeLabels.EntryPoint)
                {
                    targetNodeIds.Add(symId);
                }
            }
        }

        // 2. Look for Class / Component / Function nodes in the candidate files
        foreach (var file in sortedFiles)
        {
            foreach (var ((filePath, kind, name), symId) in ctx.FileScopedSymbols)
            {
                var norm = filePath.Replace('\\', '/');
                if (norm.Equals(file, StringComparison.OrdinalIgnoreCase) &&
                    (kind is "Class" or "Component" || kind == OntologyConstants.NodeLabels.Function))
                {
                    targetNodeIds.Add(symId);
                    if (targetNodeIds.Count >= 2) break;
                }
            }
            if (targetNodeIds.Count >= 2) break;
        }

        // 3. Fallback to FunctionSymbolsByFile
        if (targetNodeIds.Count == 0)
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
            var relProps = new Dictionary<string, object>
            {
                ["category"] = "ui_host",
                ["dependency_type"] = "instantiates",
                ["asset"] = cleanAsset,
                [OntologyConstants.PropertyNames.FilePath] = callerFile ?? "",
                ["target_file"] = primaryFile,
                [OntologyConstants.PropertyNames.EvidenceKind] = "instantiates"
            };

            if (refItem.Line > 0)
            {
                relProps[OntologyConstants.PropertyNames.Line] = refItem.Line;
            }

            var rel = new Relationship(hostId, targetId, OntologyConstants.Relationships.Instantiates, relProps);
            referenceRelationships.Add(rel);
        }
    }
}

