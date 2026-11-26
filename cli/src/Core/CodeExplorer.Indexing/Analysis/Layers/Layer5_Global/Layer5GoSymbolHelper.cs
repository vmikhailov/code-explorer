using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer5GoSymbolHelper
{
    internal const string GoInterfaceMethodMarker = "__go_iface_method__";
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';
    private const char DotChar = '.';
    private const char ColonChar = ':';
    private const string GoExt = ".go";
    private const string GoTestExt = "_test.go";
    private const string SelfKeyword = "self";
    private const string ThisKeyword = "this";
    private const string EndpointLongMarker = ":endpoint:";

    internal static void RegisterWorkspaceGoModules(
        ParsingContext ctx,
        Dictionary<string, Layer5AnalysisParser.PackageTargetInfo> workspacePackages)
    {
        foreach (var (modName, relDir) in ctx.WorkspaceGoModules)
        {
            if (!string.IsNullOrEmpty(modName))
            {
                workspacePackages.TryAdd(modName, new Layer5AnalysisParser.PackageTargetInfo(relDir, null));
            }
        }
    }

    internal static string StripGoPackageQualifier(string typeName)
    {
        var dot = typeName.LastIndexOf(DotChar);
        return dot > 0 && dot < typeName.Length - 1 ? typeName[(dot + 1)..] : typeName;
    }

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
        Layer5AnalysisParser.BindingLookups bindings,
        ParsingContext? ctx = null)
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
                nextType = MatchFieldTypeForOwner(currentType, fieldCandidates, ctx);
            }
            if (string.IsNullOrEmpty(nextType)) return null;
            currentType = nextType;
        }

        return currentType;
    }

    private static string? MatchFieldTypeForOwner(
        string ownerType,
        List<Layer5AnalysisParser.IndexedBinding> fieldCandidates,
        ParsingContext? ctx)
    {
        var dot = ownerType.LastIndexOf(DotChar);
        var pkgPrefix = dot > 0 ? ownerType[..dot] : null;
        var shortOwner = dot > 0 ? ownerType[(dot + 1)..] : ownerType;

        string? fallback = null;
        for (var i = 0; i < fieldCandidates.Count; i++)
        {
            var fc = fieldCandidates[i];
            if (!string.Equals(fc.ScopeId, shortOwner, StringComparison.Ordinal)) continue;
            if (string.IsNullOrEmpty(pkgPrefix)) return fc.TypeName;

            if (ctx != null && !string.IsNullOrEmpty(fc.FilePath) &&
                ctx.FileNamespaces.TryGetValue(fc.FilePath, out var fileNs) &&
                string.Equals(fileNs, pkgPrefix, StringComparison.Ordinal))
            {
                return fc.TypeName;
            }
            fallback ??= fc.TypeName;
        }
        return fallback;
    }

    internal static bool MatchesTypeMethodOwner(string methodId, string typeFile, string targetTypeName, ParsingContext ctx)
    {
        if (!typeFile.EndsWith(GoExt, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!ctx.SymbolScopes.TryGetValue(methodId, out var scope) || string.IsNullOrEmpty(scope.EnclosingTypeName))
        {
            return false;
        }

        var shortTarget = StripGoPackageQualifier(targetTypeName);
        return string.Equals(StripGoPackageQualifier(scope.EnclosingTypeName), shortTarget, StringComparison.Ordinal);
    }

    internal static List<string>? FindGoMethodsForReceiverType(
        Layer5AnalysisParser.SymbolLookups symbols,
        ParsingContext ctx,
        string receiverTypeName,
        string methodName)
    {
        if (!symbols.FunctionsByName.TryGetValue(methodName, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        var shortReceiver = StripGoPackageQualifier(receiverTypeName);
        List<string>? matched = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            var id = candidates[i];
            if (ctx.SymbolScopes.TryGetValue(id, out var scope) &&
                !string.IsNullOrEmpty(scope.EnclosingTypeName) &&
                string.Equals(StripGoPackageQualifier(scope.EnclosingTypeName), shortReceiver, StringComparison.Ordinal))
            {
                matched ??= [];
                matched.Add(id);
            }
        }
        return matched;
    }

    internal static void ResolveImplicitGoInterfaces(
        ParsingContext ctx,
        Layer5AnalysisParser.SymbolLookups symbols,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To)> inheritanceRels,
        Dictionary<string, List<string>> interfaceToImplementors)
    {
        var ifaceMethods = CollectGoInterfaceMethodSets(ctx);
        if (ifaceMethods.Count == 0) return;

        var structMethods = CollectGoStructMethodSets(ctx);
        if (structMethods.Count == 0) return;

        foreach (var ((ifaceFile, ifaceName), requiredSet) in ifaceMethods)
        {
            if (requiredSet.Count == 0) continue;
            var ifaceTypeId = TryFindTypeIdInFile(ifaceFile, ifaceName, symbols);
            if (ifaceTypeId == null) continue;

            foreach (var ((pkgDir, structName), implSet) in structMethods)
            {
                if (!requiredSet.IsSubsetOf(implSet)) continue;
                var structTypeId = TryFindGoStructTypeIdInPackage(pkgDir, structName, symbols);
                if (structTypeId == null || structTypeId == ifaceTypeId) continue;

                if (inheritanceRels.Add((structTypeId, ifaceTypeId)))
                {
                    referenceRelationships.Add(Relationship.FromRelationship(new ImplementsRelationship(structTypeId, ifaceTypeId)));
                    symbols.InheritanceRels.Add((structTypeId, ifaceTypeId));
                }
                symbols.InheritedTypeNames.Add((structName, ifaceName));
                if (!interfaceToImplementors.TryGetValue(ifaceName, out var implList))
                {
                    implList = [];
                    interfaceToImplementors[ifaceName] = implList;
                }
                if (!implList.Contains(structName))
                {
                    implList.Add(structName);
                }
            }
        }
    }

    private static Dictionary<(string FilePath, string IfaceName), HashSet<string>> CollectGoInterfaceMethodSets(ParsingContext ctx)
    {
        var result = new Dictionary<(string FilePath, string IfaceName), HashSet<string>>();
        foreach (var b in ctx.RawTypeBindings)
        {
            if (!string.Equals(b.VariableName, GoInterfaceMethodMarker, StringComparison.Ordinal) ||
                string.IsNullOrEmpty(b.ScopeId) ||
                string.IsNullOrEmpty(b.TypeName) ||
                string.IsNullOrEmpty(b.FilePath))
            {
                continue;
            }

            var key = (b.FilePath.NormalizeSlashes(), b.ScopeId);
            if (!result.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                result[key] = set;
            }
            set.Add(b.TypeName);
        }
        return result;
    }

    private static Dictionary<(string PkgDir, string StructName), HashSet<string>> CollectGoStructMethodSets(ParsingContext ctx)
    {
        var result = new Dictionary<(string PkgDir, string StructName), HashSet<string>>();
        foreach (var (symbolId, scope) in ctx.SymbolScopes)
        {
            if (scope.IsTest ||
                string.IsNullOrEmpty(scope.EnclosingTypeName) ||
                string.IsNullOrEmpty(scope.FilePath) ||
                !scope.FilePath.EndsWith(GoExt, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fnMarkerIdx = symbolId.LastIndexOf(ColonChar);
            if (fnMarkerIdx <= 0 || fnMarkerIdx >= symbolId.Length - 1) continue;
            var fnName = symbolId[(fnMarkerIdx + 1)..];
            var normFile = scope.FilePath.NormalizeSlashes();
            var slashIdx = normFile.LastIndexOf(SlashChar);
            var pkgDir = slashIdx > 0 ? normFile[..slashIdx] : string.Empty;
            var key = (pkgDir, StripGoPackageQualifier(scope.EnclosingTypeName));

            if (!result.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                result[key] = set;
            }
            set.Add(fnName);
        }
        return result;
    }

    private static string? TryFindTypeIdInFile(string normFile, string typeName, Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (!symbols.TypesByName.TryGetValue(typeName, out var cands)) return null;
        for (var i = 0; i < cands.Count; i++)
        {
            var fp = Layer5AnalysisParser.ExtractFilePathFromSymbolId(cands[i])?.NormalizeSlashes();
            if (string.Equals(fp, normFile, StringComparison.OrdinalIgnoreCase))
            {
                return cands[i];
            }
        }
        return null;
    }

    private static string? TryFindGoStructTypeIdInPackage(string pkgDir, string structName, Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (!symbols.TypesByName.TryGetValue(structName, out var cands)) return null;
        for (var i = 0; i < cands.Count; i++)
        {
            var fp = Layer5AnalysisParser.ExtractFilePathFromSymbolId(cands[i])?.NormalizeSlashes();
            if (string.IsNullOrEmpty(fp) || fp.EndsWith(GoTestExt, StringComparison.OrdinalIgnoreCase)) continue;
            var sIdx = fp.LastIndexOf(SlashChar);
            var dir = sIdx > 0 ? fp[..sIdx] : string.Empty;
            if (string.Equals(dir, pkgDir, StringComparison.OrdinalIgnoreCase))
            {
                return cands[i];
            }
        }
        return null;
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

        var shortTargetType = StripGoPackageQualifier(targetTypeName);
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
            if (!ctx.SymbolScopes.TryGetValue(candMethodId, out var candMethodScope) ||
                string.IsNullOrEmpty(candMethodScope.EnclosingTypeName) ||
                !string.Equals(StripGoPackageQualifier(candMethodScope.EnclosingTypeName), shortTargetType, StringComparison.Ordinal))
            {
                continue;
            }
            if (Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(callerSymbolId, normCallerFile, candMethodId, hasExplicitReceiver: true, shortTargetType, symbols, ctx))
            {
                return candMethodId;
            }
        }

        return null;
    }
}
