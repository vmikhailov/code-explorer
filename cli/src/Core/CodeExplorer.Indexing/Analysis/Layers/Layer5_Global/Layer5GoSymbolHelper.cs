using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer5GoSymbolHelper
{
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';
    private const char DotChar = '.';
    private const string GoExt = ".go";
    private const string GoTestExt = "_test.go";
    private const string SelfKeyword = "self";
    private const string ThisKeyword = "this";
    private const string EndpointLongMarker = ":endpoint:";

    internal static (string? MatchedKey, Layer5AnalysisParser.PackageTargetInfo? MatchedInfo) FindLongestWorkspaceModuleMatch(
        string importPath,
        IReadOnlyDictionary<string, Layer5AnalysisParser.PackageTargetInfo> workspacePackages)
    {
        string? matchedKey = null;
        Layer5AnalysisParser.PackageTargetInfo? matchedInfo = null;
        foreach (var (wsKey, wsInfo) in workspacePackages)
        {
            if (importPath.Length > wsKey.Length &&
                importPath[wsKey.Length] == SlashChar &&
                importPath.StartWithIC(wsKey) &&
                (matchedKey == null || wsKey.Length > matchedKey.Length))
            {
                matchedKey = wsKey;
                matchedInfo = wsInfo;
            }
        }
        return (matchedKey, matchedInfo);
    }

    internal static void AddGoPackageDirectoryCandidates(
        string dirPath,
        List<string> results,
        Layer5AnalysisParser.SymbolLookups symbols)
    {
        var cleanDir = dirPath.Trim(SlashChar);
        foreach (var known in symbols.KnownFiles)
        {
            if (!known.EndsWith(GoExt, StringComparison.OrdinalIgnoreCase) ||
                known.EndsWith(GoTestExt, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lastSlash = known.LastIndexOf(SlashChar);
            var fileDir = lastSlash > 0 ? known[..lastSlash] : string.Empty;
            if (string.Equals(fileDir, cleanDir, StringComparison.OrdinalIgnoreCase) && !results.Contains(known))
            {
                results.Add(known);
            }
        }
    }

    internal static void ResolveUnqualifiedHandlerOrAccessorFallback(
        Reference refItem,
        string targetName,
        bool isImpl,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        if (!symbols.FunctionsByName.TryGetValue(targetName, out var candidateFnList) || candidateFnList.Count == 0)
        {
            return;
        }

        string? resolvedFnId = null;
        if (candidateFnList.Count == 1)
        {
            resolvedFnId = candidateFnList[0];
        }
        else
        {
            var refFile = !string.IsNullOrEmpty(refItem.ArgFlow) &&
                          (refItem.ArgFlow.IndexOf(SlashChar) >= 0 || refItem.ArgFlow.IndexOf(BackslashChar) >= 0)
                ? refItem.ArgFlow.NormalizeSlashes()
                : Layer5AnalysisParser.ExtractFilePathFromSymbolId(refItem.ScopeSymbolId)?.NormalizeSlashes();

            if (!string.IsNullOrEmpty(refFile))
            {
                resolvedFnId = candidateFnList.FirstOrDefault(id =>
                    string.Equals(Layer5AnalysisParser.ExtractFilePathFromSymbolId(id)?.NormalizeSlashes(), refFile, StringComparison.OrdinalIgnoreCase));

                if (resolvedFnId == null)
                {
                    var lastSlash = refFile.LastIndexOf(SlashChar);
                    var refDir = lastSlash > 0 ? refFile[..lastSlash] : string.Empty;
                    if (!string.IsNullOrEmpty(refDir))
                    {
                        resolvedFnId = candidateFnList.FirstOrDefault(id =>
                        {
                            var fp = Layer5AnalysisParser.ExtractFilePathFromSymbolId(id)?.NormalizeSlashes();
                            if (string.IsNullOrEmpty(fp)) return false;
                            var sIdx = fp.LastIndexOf(SlashChar);
                            var dir = sIdx > 0 ? fp[..sIdx] : string.Empty;
                            return string.Equals(dir, refDir, StringComparison.OrdinalIgnoreCase);
                        });
                    }
                }
            }
        }

        if (resolvedFnId == null || refItem.ScopeSymbolId == resolvedFnId) return;

        var rel = isImpl
            ? Relationship.FromRelationship(new ImplementedByRelationship(refItem.ScopeSymbolId, resolvedFnId))
            : Relationship.FromRelationship(new AccessedByRelationship(refItem.ScopeSymbolId, resolvedFnId));
        referenceRelationships.Add(rel);

        var epToken = $":{OntologyConstants.IdPrefixes.Endpoint}:";
        if (isImpl &&
            (refItem.ScopeSymbolId.Contains(epToken, StringComparison.Ordinal) ||
             refItem.ScopeSymbolId.Contains(EndpointLongMarker, StringComparison.OrdinalIgnoreCase)))
        {
            referenceRelationships.Add(Relationship.FromRelationship(new ExposedByRelationship(refItem.ScopeSymbolId, resolvedFnId)));
        }
    }

    internal static string? TryResolveChainedFieldReceiverType(
        string chainedReceiver,
        string? normCallerFile,
        string scopeSymbolId,
        string? enclosingTypeName,
        Layer5AnalysisParser.BindingLookups bindings)
    {
        var parts = chainedReceiver.Split(DotChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        string? currentType = null;
        var rootVar = parts[0];
        if ((rootVar == ThisKeyword || rootVar == SelfKeyword) && !string.IsNullOrEmpty(enclosingTypeName))
        {
            currentType = enclosingTypeName;
        }
        else if (normCallerFile != null && bindings.BindingsLookup.TryGetValue((normCallerFile, rootVar), out var rootCandidates))
        {
            for (var i = 0; i < rootCandidates.Count; i++)
            {
                var c = rootCandidates[i];
                if (!string.IsNullOrEmpty(c.ScopeId))
                {
                    var midToken = $":{c.ScopeId}:";
                    var endToken = $":{c.ScopeId}";
                    if (scopeSymbolId.Contains(midToken, StringComparison.OrdinalIgnoreCase) ||
                        scopeSymbolId.EndsWith(endToken, StringComparison.OrdinalIgnoreCase))
                    {
                        currentType = c.TypeName;
                        break;
                    }
                }
            }
            currentType ??= rootCandidates[0].TypeName;
        }

        if (string.IsNullOrEmpty(currentType)) return null;

        for (var seg = 1; seg < parts.Length; seg++)
        {
            var fieldName = parts[seg];
            string? nextType = null;
            if (bindings.GlobalBindingsLookup.TryGetValue(fieldName, out var fieldCandidates))
            {
                for (var i = 0; i < fieldCandidates.Count; i++)
                {
                    var fc = fieldCandidates[i];
                    if (string.Equals(fc.ScopeId, currentType, StringComparison.Ordinal))
                    {
                        nextType = fc.TypeName;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(nextType)) return null;
            currentType = nextType;
        }

        return currentType;
    }

    internal static string? TryResolveGoSiblingPackageMethod(
        string typeFile,
        string targetTypeName,
        string methodName,
        string callerSymbolId,
        string? normCallerFile,
        Layer5AnalysisParser.SymbolLookups symbols,
        ParsingContext ctx)
    {
        if (!typeFile.EndsWith(GoExt, StringComparison.OrdinalIgnoreCase) ||
            !symbols.FunctionsByName.TryGetValue(methodName, out var goPkgMethodCandidates))
        {
            return null;
        }

        var normTypeFile = typeFile.NormalizeSlashes();
        var lastSlash = normTypeFile.LastIndexOf(SlashChar);
        var typeDir = lastSlash > 0 ? normTypeFile[..lastSlash] : string.Empty;

        foreach (var candMethodId in goPkgMethodCandidates)
        {
            var candFile = Layer5AnalysisParser.ExtractFilePathFromSymbolId(candMethodId)?.NormalizeSlashes();
            if (string.IsNullOrEmpty(candFile)) continue;
            var candSlash = candFile.LastIndexOf(SlashChar);
            var candDir = candSlash > 0 ? candFile[..candSlash] : string.Empty;
            if (!string.Equals(candDir, typeDir, StringComparison.OrdinalIgnoreCase)) continue;
            if (ctx.SymbolScopes.TryGetValue(candMethodId, out var candMethodScope) &&
                !string.IsNullOrEmpty(candMethodScope.EnclosingTypeName) &&
                !string.Equals(candMethodScope.EnclosingTypeName, targetTypeName, StringComparison.Ordinal))
            {
                continue;
            }
            if (Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(callerSymbolId, normCallerFile, candMethodId, hasExplicitReceiver: true, targetTypeName, symbols, ctx))
            {
                return candMethodId;
            }
        }

        return null;
    }
}
