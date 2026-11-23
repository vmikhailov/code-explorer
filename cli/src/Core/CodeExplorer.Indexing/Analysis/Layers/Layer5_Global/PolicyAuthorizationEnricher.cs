using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Layers;

internal static class PolicyAuthorizationEnricher
{
    private const string AllowAnonymousToken = "AllowAnonymous";
    private const string AllowAnyToken = "AllowAny";
    private const string PermitAllToken = "PermitAll";
    private const string PublicAttrToken = "Public";
    private const string AnonymousAllowedToken = "AnonymousAllowed";
    private const string AuthorizeToken = "Authorize";
    private const string RequireAuthMethod = "RequireAuthorization";
    private const string PreAuthorizeToken = "PreAuthorize";
    private const string SecuredToken = "Secured";
    private const string RolesAllowedToken = "RolesAllowed";
    private const string RolesAttrToken = "Roles";
    private const string IsAdminUserToken = "IsAdminUser";
    private const string IsGrantedToken = "IsGranted";
    private const string HasPermissionToken = "HasPermission";
    private const string PermissionsToken = "Permissions";
    private const string PermissionSuffix = "Permission";
    private const string PermissionsSuffix = "Permissions";
    private const string UseGuardsToken = "UseGuards";
    private const string LoginRequiredToken = "login_required";
    private const string PermissionRequiredToken = "permission_required";
    private const string GetCurrentUserToken = "get_current_user";
    private const string IsAuthenticatedToken = "IsAuthenticated";
    private const string AuthMiddlewareToken = "AuthMiddleware";
    private const string RequireAuthToken = "RequireAuth";
    private const string CanActivateMethod = "canActivate";
    private const string HandleRequirementMethod = "HandleRequirementAsync";
    private const string GuardSuffix = "Guard";
    private const string AuthGuardSuffix = "AuthGuard";
    private const string AdminToken = "Admin";
    private const string RoleToken = "Role";
    private const string SuperUserToken = "SuperUser";
    private const string DefaultAuthenticatedName = "Authenticated";
    private const string DefaultAnonymousName = "Anonymous";
    private const string FrameworkAspNetAuth = "aspnetcore-auth";
    private const string FrameworkNestGuards = "nestjs-guards";
    private const string FrameworkSpringSec = "spring-security";
    private const string FrameworkDrfAuth = "drf-permissions";
    private const string FrameworkFastApiSec = "fastapi-security";
    private const string FrameworkGenericAuth = "auth-policy";

    private static readonly string[] NonSecurityGuardTokens =
    [
        "Throttler", "RateLimit", "LogGuard", "LoggingGuard", "ValidationGuard", "CacheGuard", "FeatureFlagGuard"
    ];

    internal sealed record PolicyEnrichmentOutput(List<PolicyNode> Policies, List<Relationship> Relationships);

    public static PolicyEnrichmentOutput EnrichPolicies(
        Layer4Result? l4Result,
        ParsingContext ctx,
        List<MessageNode> messages,
        Dictionary<string, List<Reference>> refsByScopeId,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var policiesById = new Dictionary<string, PolicyNode>(StringComparer.Ordinal);
        var rels = new List<Relationship>();
        if (l4Result == null) return new PolicyEnrichmentOutput([], rels);

        var syntaxStruct = l4Result.Prev?.SyntaxStructure ?? ctx.SyntaxStructure;
        if (syntaxStruct == null) return new PolicyEnrichmentOutput([], rels);

        var filesByPath = SemanticEnrichmentHelpers.CollectFilesByPath(l4Result);
        var modules = SemanticEnrichmentHelpers.CollectModules(l4Result.SemanticStructure);
        var index = SemanticEnrichmentHelpers.CollectTypesAndMethods(syntaxStruct);
        var ownerTypeByFnId = BuildOwnerTypeByFnMap(index);
        var guardsByImportedFile = BuildGuardTypesByImportingFile(ctx, index);

        var (endpoints, treeParentHandlers) = CollectEndpoints(l4Result, syntaxStruct);
        var (epHandlers, epOwnerTypes) = MapEndpointHandlers(endpoints, l4Result.SemanticRelationships, referenceRelationships, ctx.TreeRelationships, index);
        foreach (var (epId, parentFn) in treeParentHandlers)
        {
            epHandlers.TryAdd(epId, parentFn);
        }

        for (var i = 0; i < endpoints.Count; i++)
        {
            var ep = endpoints[i];
            epHandlers.TryGetValue(ep.Id, out var handlerFn);
            TypeNode? controllerType = null;
            if (handlerFn != null) ownerTypeByFnId.TryGetValue(handlerFn.Id, out controllerType);
            if (controllerType == null) epOwnerTypes.TryGetValue(ep.Id, out controllerType);

            EnrichEndpointPolicy(
                ctx, ep, handlerFn, controllerType, refsByScopeId, guardsByImportedFile, index, filesByPath, modules, policiesById, rels, existingRefRels);
        }

        var msgHandlers = MapMessageHandlers(messages, referenceRelationships, index);
        for (var i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            index.TypesByName.TryGetValue(msg.Name, out var msgType);
            msgHandlers.TryGetValue(msg.Id, out var handlerFn);
            TypeNode? handlerType = null;
            if (handlerFn != null) ownerTypeByFnId.TryGetValue(handlerFn.Id, out handlerType);

            EnrichMessagePolicy(
                ctx, msg, msgType, handlerFn, handlerType, refsByScopeId, guardsByImportedFile, index, filesByPath, modules, policiesById, rels, existingRefRels);
        }

        return new PolicyEnrichmentOutput(policiesById.Values.ToList(), rels);
    }

    private static Dictionary<string, TypeNode> BuildOwnerTypeByFnMap(SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index)
    {
        var map = new Dictionary<string, TypeNode>(StringComparer.Ordinal);
        foreach (var (typeId, methods) in index.MethodsByTypeId)
        {
            if (!index.TypesById.TryGetValue(typeId, out var tn)) continue;
            for (var i = 0; i < methods.Count; i++)
            {
                map[methods[i].Id] = tn;
            }
        }
        return map;
    }

    private static (List<EndpointNode> Endpoints, Dictionary<string, FunctionNode> ParentHandlers) CollectEndpoints(
        Layer4Result l4Result,
        IOntologyNode syntaxStruct)
    {
        var byId = new Dictionary<string, EndpointNode>(StringComparer.Ordinal);
        var parentHandlers = new Dictionary<string, FunctionNode>(StringComparer.Ordinal);
        void Walk(IOntologyNode? n, FunctionNode? currentFn)
        {
            if (n == null) return;
            var nextFn = n is FunctionNode fn ? fn : currentFn;
            if (n is EndpointNode ep)
            {
                byId.TryAdd(ep.Id, ep);
                if (currentFn != null) parentHandlers.TryAdd(ep.Id, currentFn);
            }
            for (var i = 0; i < n.Children.Count; i++) Walk(n.Children[i], nextFn);
        }
        Walk(l4Result.Prev?.Prev?.Prev?.Workspace, null);
        Walk(syntaxStruct, null);
        Walk(l4Result.SemanticStructure, null);
        for (var i = 0; i < l4Result.SemanticNodes.Count; i++)
        {
            if (l4Result.SemanticNodes[i] is EndpointNode ep) byId.TryAdd(ep.Id, ep);
        }
        return (byId.Values.ToList(), parentHandlers);
    }

    private static (Dictionary<string, FunctionNode> Handlers, Dictionary<string, TypeNode> OwnerTypes) MapEndpointHandlers(
        List<EndpointNode> endpoints,
        List<Relationship> semanticRels,
        List<Relationship> refRels,
        List<Relationship> treeRels,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index)
    {
        var map = new Dictionary<string, FunctionNode>(StringComparer.Ordinal);
        var ownerTypes = new Dictionary<string, TypeNode>(StringComparer.Ordinal);
        var epSet = endpoints.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        void Scan(List<Relationship> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r.Kind is RelationshipKind.Triggers or RelationshipKind.ExposedBy && epSet.Contains(r.From))
                {
                    if (index.FunctionsById.TryGetValue(r.To, out var fn))
                    {
                        map.TryAdd(r.From, fn);
                    }
                    else if (index.TypesById.TryGetValue(r.To, out var tn))
                    {
                        ownerTypes.TryAdd(r.From, tn);
                    }
                }
            }
        }
        Scan(semanticRels);
        Scan(refRels);
        Scan(treeRels);
        return (map, ownerTypes);
    }

    private static Dictionary<string, FunctionNode> MapMessageHandlers(
        List<MessageNode> messages,
        List<Relationship> refRels,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index)
    {
        var map = new Dictionary<string, FunctionNode>(StringComparer.Ordinal);
        var msgSet = messages.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < refRels.Count; i++)
        {
            var r = refRels[i];
            if (r.Kind == RelationshipKind.Triggers && msgSet.Contains(r.From) && index.FunctionsById.TryGetValue(r.To, out var fn))
            {
                map.TryAdd(r.From, fn);
            }
        }
        return map;
    }

    private static void EnrichEndpointPolicy(
        ParsingContext ctx,
        EndpointNode ep,
        FunctionNode? handlerFn,
        TypeNode? controllerType,
        Dictionary<string, List<Reference>> refsByScopeId,
        Dictionary<string, List<string>> guardsByImportedFile,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, PolicyNode> policiesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var fnAttrs = handlerFn?.Attributes ?? string.Empty;
        var clsAttrs = controllerType?.Attributes ?? string.Empty;
        var fileNorm = (ep.FilePath ?? handlerFn?.FilePath ?? ep.Path).Replace('\\', '/');
        var line = ep.StartLine ?? handlerFn?.StartLine ?? 1;

        if (HasAnonymousMarker(fnAttrs) || (string.IsNullOrEmpty(fnAttrs) && HasAnonymousMarker(clsAttrs)) || HasFluentCall(handlerFn, refsByScopeId, AllowAnonymousToken))
        {
            var anon = GetOrCreatePolicy(ctx, DefaultAnonymousName, PolicyKind.Anonymous, null, FrameworkGenericAuth, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(ep.Id, anon.Id), rels, existingRefRels);
            return;
        }

        if (!string.IsNullOrWhiteSpace(ep.RequiredRoles))
        {
            foreach (var role in SplitTokens(ep.RequiredRoles))
            {
                var p = GetOrCreatePolicy(ctx, role, PolicyKind.Role, role, FrameworkGenericAuth, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
                SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(ep.Id, p.Id), rels, existingRefRels);
            }
        }

        if (!string.IsNullOrWhiteSpace(ep.Policies))
        {
            foreach (var pol in SplitTokens(ep.Policies))
            {
                var p = GetOrCreatePolicy(ctx, pol, PolicyKind.Permission, pol, FrameworkGenericAuth, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
                SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(ep.Id, p.Id), rels, existingRefRels);
            }
        }

        AttachPoliciesFromAttributesAndCalls(
            ctx, ep.Id, fnAttrs, clsAttrs, handlerFn, controllerType, fileNorm, line, refsByScopeId, guardsByImportedFile, index, filesByPath, modules, policiesById, rels, existingRefRels);
    }

    private static void EnrichMessagePolicy(
        ParsingContext ctx,
        MessageNode msg,
        TypeNode? msgType,
        FunctionNode? handlerFn,
        TypeNode? handlerType,
        Dictionary<string, List<Reference>> refsByScopeId,
        Dictionary<string, List<string>> guardsByImportedFile,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, PolicyNode> policiesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var combinedFnAttrs = $"{msgType?.Attributes} {handlerFn?.Attributes}".Trim();
        var clsAttrs = handlerType?.Attributes ?? string.Empty;
        var fileNorm = (msg.FilePath ?? handlerFn?.FilePath ?? msg.Path).Replace('\\', '/');
        var line = msg.StartLine ?? handlerFn?.StartLine ?? 1;

        if (HasAnonymousMarker(combinedFnAttrs))
        {
            var anon = GetOrCreatePolicy(ctx, DefaultAnonymousName, PolicyKind.Anonymous, null, FrameworkGenericAuth, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(msg.Id, anon.Id), rels, existingRefRels);
            return;
        }

        AttachPoliciesFromAttributesAndCalls(
            ctx, msg.Id, combinedFnAttrs, clsAttrs, handlerFn, handlerType, fileNorm, line, refsByScopeId, guardsByImportedFile, index, filesByPath, modules, policiesById, rels, existingRefRels);
    }

    private static bool AttachPoliciesFromAttributesAndCalls(
        ParsingContext ctx,
        string targetNodeId,
        string fnAttrs,
        string clsAttrs,
        FunctionNode? handlerFn,
        TypeNode? ownerType,
        string fileNorm,
        int line,
        Dictionary<string, List<Reference>> refsByScopeId,
        Dictionary<string, List<string>> guardsByImportedFile,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, PolicyNode> policiesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var attrs = $"{fnAttrs} {clsAttrs}".Trim();
        var attached = false;

        if (attrs.Contains(UseGuardsToken, StringComparison.Ordinal))
        {
            var guards = ExtractGuardNames(handlerFn, ownerType, fileNorm, refsByScopeId, guardsByImportedFile, index);
            if (guards.Count == 0)
            {
                var p = GetOrCreatePolicy(ctx, UseGuardsToken, PolicyKind.Authenticated, UseGuardsToken, FrameworkNestGuards, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
                SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
                attached = true;
            }
            else
            {
                for (var i = 0; i < guards.Count; i++)
                {
                    var gName = guards[i];
                    if (IsNonSecurityGuard(gName)) continue;
                    var gKind = ClassifyGuardSubKind(gName);
                    var p = GetOrCreatePolicy(ctx, gName, gKind, gName, FrameworkNestGuards, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
                    SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
                    attached = true;
                }
            }
        }

        if (attrs.Contains(PreAuthorizeToken, StringComparison.Ordinal) ||
            attrs.Contains(HasPermissionToken, StringComparison.Ordinal) ||
            HasStandaloneWord(attrs, PermissionsToken) ||
            attrs.Contains(IsGrantedToken, StringComparison.Ordinal) ||
            attrs.Contains(PermissionRequiredToken, StringComparison.OrdinalIgnoreCase))
        {
            var req = ExtractHeaderStringLiteral(handlerFn) ?? PreAuthorizeToken;
            var p = GetOrCreatePolicy(ctx, req, PolicyKind.Permission, req, FrameworkSpringSec, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
            attached = true;
        }
        else if (attrs.Contains(SecuredToken, StringComparison.Ordinal) ||
                 attrs.Contains(RolesAllowedToken, StringComparison.Ordinal) ||
                 attrs.Contains(RolesAttrToken, StringComparison.Ordinal))
        {
            var role = ExtractHeaderStringLiteral(handlerFn) ?? RolesAttrToken;
            var p = GetOrCreatePolicy(ctx, role, PolicyKind.Role, role, FrameworkSpringSec, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
            attached = true;
        }
        else if (attrs.Contains(AuthorizeToken, StringComparison.Ordinal) ||
                 attrs.Contains(LoginRequiredToken, StringComparison.OrdinalIgnoreCase) ||
                 attrs.Contains(IsAuthenticatedToken, StringComparison.Ordinal) ||
                 HasFluentCall(handlerFn, refsByScopeId, RequireAuthMethod) ||
                 HasFluentCall(handlerFn, refsByScopeId, AuthMiddlewareToken) ||
                 HasFluentCall(handlerFn, refsByScopeId, RequireAuthToken))
        {
            var polName = ExtractFluentPolicyArg(handlerFn, RequireAuthMethod) ?? DefaultAuthenticatedName;
            var kind = polName == DefaultAuthenticatedName ? PolicyKind.Authenticated : PolicyKind.Permission;
            var p = GetOrCreatePolicy(ctx, polName, kind, polName, FrameworkAspNetAuth, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
            attached = true;
        }

        if (HasFastApiUserDependency(handlerFn, refsByScopeId))
        {
            var p = GetOrCreatePolicy(ctx, GetCurrentUserToken, PolicyKind.Authenticated, GetCurrentUserToken, FrameworkFastApiSec, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
            attached = true;
        }

        if (!string.IsNullOrWhiteSpace(attrs))
        {
            foreach (var tok in SplitTokens(attrs))
            {
                if (string.Equals(tok, IsAdminUserToken, StringComparison.Ordinal))
                {
                    var p = GetOrCreatePolicy(ctx, IsAdminUserToken, PolicyKind.Role, IsAdminUserToken, FrameworkDrfAuth, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
                    SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
                    attached = true;
                }
                else if (tok.Length > PermissionsSuffix.Length &&
                         (tok.EndsWith(PermissionsSuffix, StringComparison.Ordinal) || tok.EndsWith(PermissionSuffix, StringComparison.Ordinal)) &&
                         !string.Equals(tok, HasPermissionToken, StringComparison.Ordinal))
                {
                    var p = GetOrCreatePolicy(ctx, tok, PolicyKind.Permission, tok, FrameworkDrfAuth, fileNorm, line, index, filesByPath, modules, policiesById, rels, existingRefRels);
                    SemanticEnrichmentHelpers.AddUniqueRel(new GuardedByRelationship(targetNodeId, p.Id), rels, existingRefRels);
                    attached = true;
                }
            }
        }

        return attached;
    }

    private static bool HasAnonymousMarker(string? attrs) =>
        !string.IsNullOrEmpty(attrs) &&
        (attrs.Contains(AllowAnonymousToken, StringComparison.Ordinal) ||
         attrs.Contains(AllowAnyToken, StringComparison.Ordinal) ||
         attrs.Contains(PermitAllToken, StringComparison.Ordinal) ||
         attrs.Contains(AnonymousAllowedToken, StringComparison.Ordinal) ||
         HasStandaloneWord(attrs, PublicAttrToken));

    private static bool HasStandaloneWord(string text, string word)
    {
        var idx = text.IndexOf(word, StringComparison.Ordinal);
        if (idx < 0) return false;
        var beforeOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
        var afterIdx = idx + word.Length;
        var afterOk = afterIdx >= text.Length || !char.IsLetterOrDigit(text[afterIdx]);
        return beforeOk && afterOk;
    }

    private static Dictionary<string, List<string>> BuildGuardTypesByImportingFile(
        ParsingContext ctx,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index)
    {
        var guardTypes = new List<(string Name, string Stem)>();
        foreach (var (_, tn) in index.TypesById)
        {
            if (tn.IsTest || !tn.Name.EndsWith(GuardSuffix, StringComparison.Ordinal)) continue;
            var fn = Path.GetFileNameWithoutExtension(tn.FilePath);
            guardTypes.Add((tn.Name, fn));
        }

        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (guardTypes.Count == 0) return map;

        for (var i = 0; i < ctx.RawImports.Count; i++)
        {
            var imp = ctx.RawImports[i];
            if (string.IsNullOrEmpty(imp.FilePath) || string.IsNullOrEmpty(imp.Path)) continue;
            var fileNorm = imp.FilePath.Replace('\\', '/');
            for (var g = 0; g < guardTypes.Count; g++)
            {
                var (gName, gStem) = guardTypes[g];
                if (imp.Path.Contains(gStem, StringComparison.OrdinalIgnoreCase) ||
                    imp.Path.Contains(gName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!map.TryGetValue(fileNorm, out var list))
                    {
                        list = [];
                        map[fileNorm] = list;
                    }
                    if (!list.Contains(gName)) list.Add(gName);
                }
            }
        }
        return map;
    }

    private static bool HasFluentCall(FunctionNode? fn, Dictionary<string, List<Reference>> refsByScopeId, string methodToken)
    {
        if (fn == null) return false;
        var refs = SemanticEnrichmentHelpers.GetScopeReferences(fn.Id, fn.References, refsByScopeId);
        for (var i = 0; i < refs.Count; i++)
        {
            var r = refs[i];
            if (r.TargetName.Contains(methodToken, StringComparison.OrdinalIgnoreCase) ||
                r.MemberName.Contains(methodToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasFastApiUserDependency(FunctionNode? fn, Dictionary<string, List<Reference>> refsByScopeId)
    {
        if (fn == null) return false;
        var refs = SemanticEnrichmentHelpers.GetScopeReferences(fn.Id, fn.References, refsByScopeId);
        for (var i = 0; i < refs.Count; i++)
        {
            if (refs[i].TargetName.Contains(GetCurrentUserToken, StringComparison.OrdinalIgnoreCase)) return true;
        }
        for (var i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is CallArgumentNode ca && ca.RawExpression.Contains(GetCurrentUserToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static List<string> ExtractGuardNames(
        FunctionNode? fn,
        TypeNode? ownerType,
        string fileNorm,
        Dictionary<string, List<Reference>> refsByScopeId,
        Dictionary<string, List<string>> guardsByImportedFile,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index)
    {
        var list = new List<string>();
        void ScanRefs(IReadOnlyList<Reference>? refs)
        {
            if (refs == null) return;
            for (var i = 0; i < refs.Count; i++)
            {
                var t = refs[i].TargetName;
                if (t.EndsWith(GuardSuffix, StringComparison.Ordinal) && index.TypesByName.ContainsKey(t) && !list.Contains(t))
                {
                    list.Add(t);
                }
            }
        }

        if (fn != null)
        {
            ScanRefs(SemanticEnrichmentHelpers.GetScopeReferences(fn.Id, fn.References, refsByScopeId));
            for (var i = 0; i < fn.Children.Count; i++)
            {
                if (fn.Children[i] is CallArgumentNode ca &&
                    ca.RawExpression.EndsWith(GuardSuffix, StringComparison.Ordinal) &&
                    index.TypesByName.ContainsKey(ca.RawExpression) &&
                    !list.Contains(ca.RawExpression))
                {
                    list.Add(ca.RawExpression);
                }
            }
        }

        if (ownerType != null)
        {
            ScanRefs(SemanticEnrichmentHelpers.GetScopeReferences(ownerType.Id, ownerType.References, refsByScopeId));
        }

        if (list.Count == 0 && guardsByImportedFile.TryGetValue(fileNorm, out var importedGuards))
        {
            for (var i = 0; i < importedGuards.Count; i++)
            {
                if (!list.Contains(importedGuards[i])) list.Add(importedGuards[i]);
            }
        }

        return list;
    }

    private static string? ExtractHeaderStringLiteral(FunctionNode? fn)
    {
        if (fn == null) return null;
        for (var i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is CallLiteralNode lit && lit.Line <= fn.StartLine + 1 && !string.IsNullOrWhiteSpace(lit.Value))
            {
                return lit.Value.Trim('"', '\'', ' ');
            }
        }
        return null;
    }

    private static string? ExtractFluentPolicyArg(FunctionNode? fn, string calleeToken)
    {
        if (fn == null) return null;
        for (var i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is CallArgumentNode ca &&
                ca.Callee.Contains(calleeToken, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(ca.ResolvedValue))
            {
                return ca.ResolvedValue.Trim('"', '\'', ' ');
            }
        }
        return null;
    }

    private static IEnumerable<string> SplitTokens(string raw) =>
        raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static PolicyNode GetOrCreatePolicy(
        ParsingContext ctx,
        string name,
        PolicyKind subKind,
        string? requirement,
        string framework,
        string fileNorm,
        int line,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, PolicyNode> policiesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var kindKey = subKind.ToCanonicalString();
        var policyId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Policy}:{kindKey}:{name}";

        if (!policiesById.TryGetValue(policyId, out var node))
        {
            index.TypesByName.TryGetValue(name, out var guardType);
            var declFile = (guardType?.FilePath ?? fileNorm).Replace('\\', '/');

            node = new PolicyNode(policyId, name, declFile, subKind)
            {
                Requirement = requirement,
                Framework = framework,
                FilePath = declFile,
                StartLine = guardType?.StartLine ?? line
            };
            policiesById[policyId] = node;

            if (guardType != null)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new ImplementedByRelationship(policyId, guardType.Id), rels, existingRefRels);
                if (index.MethodsByTypeId.TryGetValue(guardType.Id, out var gMethods))
                {
                    for (var m = 0; m < gMethods.Count; m++)
                    {
                        var gm = gMethods[m];
                        if (gm.Name.EndsWith(CanActivateMethod, StringComparison.OrdinalIgnoreCase) ||
                            gm.Name.EndsWith(HandleRequirementMethod, StringComparison.OrdinalIgnoreCase))
                        {
                            SemanticEnrichmentHelpers.AddUniqueRel(new EnforcedByRelationship(policyId, gm.Id), rels, existingRefRels);
                        }
                    }
                }
            }
            else if (name == GetCurrentUserToken)
            {
                foreach (var (_, fn) in index.FunctionsById)
                {
                    if (!fn.IsTest && fn.Name.Contains(GetCurrentUserToken, StringComparison.OrdinalIgnoreCase))
                    {
                        SemanticEnrichmentHelpers.AddUniqueRel(new EnforcedByRelationship(policyId, fn.Id), rels, existingRefRels);
                        break;
                    }
                }
            }

            if (filesByPath.TryGetValue(declFile, out var fNode))
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new DeclaredInRelationship(policyId, fNode.Id), rels, existingRefRels);
            }
            var ownerMod = SemanticEnrichmentHelpers.FindOwningModule(declFile, modules);
            if (ownerMod != null)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new ContainsRelationship(ownerMod.Id, policyId), rels, existingRefRels);
            }
        }
        return node;
    }

    private static bool IsNonSecurityGuard(string guardName)
    {
        for (var i = 0; i < NonSecurityGuardTokens.Length; i++)
        {
            if (guardName.Contains(NonSecurityGuardTokens[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static PolicyKind ClassifyGuardSubKind(string guardName)
    {
        if (guardName.Contains(AdminToken, StringComparison.OrdinalIgnoreCase) ||
            guardName.Contains(RoleToken, StringComparison.OrdinalIgnoreCase) ||
            guardName.Contains(SuperUserToken, StringComparison.OrdinalIgnoreCase))
        {
            return PolicyKind.Role;
        }

        if (guardName.Contains(PermissionSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return PolicyKind.Permission;
        }

        if (guardName.EndsWith(AuthGuardSuffix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(guardName, GuardSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return PolicyKind.Authenticated;
        }

        return PolicyKind.CustomGuard;
    }
}
