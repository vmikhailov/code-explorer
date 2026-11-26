using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer5TriggersResolver
{
    internal static void ResolveTriggersReference(
        Reference refItem,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var targetName = refItem.TargetName;
        var shortName = refItem.MemberName;
        var callerFile = Layer5AnalysisParser.ExtractFilePathFromSymbolId(refItem.ScopeSymbolId)
                         ?? (!string.IsNullOrEmpty(refItem.ArgFlow) && (refItem.ArgFlow.Contains('/') || refItem.ArgFlow.Contains('\\')) ? refItem.ArgFlow.Replace('\\', '/') : null);
        var callerProj = callerFile != null ? ConstantRegistry.ExtractProjectName(callerFile) : ExtractProjectFromScopeId(refItem.ScopeSymbolId, symbols);

        string? targetId = null;

        if (callerFile != null)
        {
            if (symbols.FunctionSymbolsByFile.TryGetValue((callerFile, targetName), out targetId) ||
                symbols.FunctionSymbolsByFile.TryGetValue((callerFile, shortName), out targetId))
            {
                // Found in same file
            }
        }

        if (targetId == null && callerProj != null && symbols.FunctionsByProject.TryGetValue(callerProj, out var projFns))
        {
            if (projFns.TryGetValue(targetName, out var ids) && (ids.Count == 1 || callerFile != null))
            {
                targetId = ids[0];
            }
            else if (projFns.TryGetValue(shortName, out ids) && (ids.Count == 1 || callerFile != null))
            {
                targetId = ids[0];
            }
        }

        if (targetId == null)
        {
            if (symbols.FunctionsByName.TryGetValue(targetName, out var candidates) ||
                symbols.FunctionsByName.TryGetValue(shortName, out candidates))
            {
                if (callerProj != null)
                {
                    var projMatches = candidates.Where(c =>
                    {
                        var cFile = Layer5AnalysisParser.ExtractFilePathFromSymbolId(c);
                        return cFile != null && string.Equals(ConstantRegistry.ExtractProjectName(cFile), callerProj, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                    if (projMatches.Count == 1 || (projMatches.Count > 0 && callerFile != null))
                    {
                        targetId = projMatches[0];
                    }
                }
                else if (candidates.Count == 1)
                {
                    targetId = candidates[0];
                }
            }
        }

        if (targetId == null && callerProj == null)
        {
            targetId = symbols.FunctionSymbols.GetValueOrDefault(targetName)
                       ?? symbols.FunctionSymbols.GetValueOrDefault(shortName);
        }

        if (targetId == null)
        {
            if (callerProj != null && symbols.FilesByProject.ContainsKey(callerProj))
            {
                if (symbols.TypeSymbols.TryGetValue(targetName, out var typeCand) ||
                    symbols.TypeSymbols.TryGetValue(shortName, out typeCand))
                {
                    var tFile = Layer5AnalysisParser.ExtractFilePathFromSymbolId(typeCand);
                    if (tFile != null && string.Equals(ConstantRegistry.ExtractProjectName(tFile), callerProj, StringComparison.OrdinalIgnoreCase))
                    {
                        targetId = typeCand;
                    }
                }
            }
            if (targetId == null)
            {
                targetId = symbols.TypeSymbols.GetValueOrDefault(targetName)
                           ?? symbols.TypeSymbols.GetValueOrDefault(shortName);
            }
        }

        if (targetId != null)
        {
            if (existingRefRels.Add((refItem.ScopeSymbolId, targetId, OntologyConstants.Relationships.Triggers)))
            {
                referenceRelationships.Add(
                    Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetId)));
            }
            if (refItem.ScopeSymbolId.Contains($":{OntologyConstants.IdPrefixes.Endpoint}:", StringComparison.Ordinal) &&
                existingRefRels.Add((refItem.ScopeSymbolId, targetId, OntologyConstants.Relationships.ExposedBy)))
            {
                referenceRelationships.Add(
                    Relationship.FromRelationship(new ExposedByRelationship(refItem.ScopeSymbolId, targetId)));
            }
        }
    }

    private static string? ExtractProjectFromScopeId(string scopeId, Layer5AnalysisParser.SymbolLookups symbols)
    {
        var parts = scopeId.Split(':');
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] is "endpoint" or "ep" or "topic" or "service" or "entry" or "entrypoint")
            {
                var next = parts[i + 1];
                if (symbols.FilesByProject.ContainsKey(next))
                {
                    return next;
                }
            }
        }

        foreach (var proj in symbols.ProjectsSortedByLengthDesc)
        {
            if (scopeId.Contains($":{proj}:", StringComparison.OrdinalIgnoreCase))
            {
                return proj;
            }
        }

        return null;
    }
}
