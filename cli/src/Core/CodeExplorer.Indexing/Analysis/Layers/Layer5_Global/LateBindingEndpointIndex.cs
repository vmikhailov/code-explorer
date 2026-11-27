using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using static CodeExplorer.Core.Parser.Layers.Layer5AnalysisParser;

namespace CodeExplorer.Core.Parser.Layers;

internal sealed class LateBindingEndpointIndex
{
    private const string GraphQlProtocol = "graphql";
    private const string GrpcProtocol = "grpc";
    private const string DubboProtocol = "dubbo";
    private const string TriProtocol = "tri";
    private const string RpcProtocol = "rpc";
    private const string ServiceSuffix = "service";
    private const string WildcardSegment = "*";
    private const string UnknownServiceDomain = "unknown-service";
    private const string Hyphen = "-";
    private const string Underscore = "_";
    private const int InitialBucketCapacity = 2;

    public Dictionary<string, List<PreparedEndpoint>> ByShape { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ProjectsWithEndpoints { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<PreparedEndpoint>> _graphQlByRoute = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PreparedEndpoint> _rpcEndpoints = [];
    private readonly List<PreparedEndpoint> _httpEndpoints = [];
    private readonly Dictionary<string, List<PreparedEndpoint>> _httpByProjectId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PreparedEndpoint> _unscopedHttpEndpoints = [];
    private readonly List<PreparedEndpoint> _paramOnlyHttpEndpoints = [];
    private readonly Dictionary<string, List<PreparedEndpoint>> _httpByRouteStem = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProjectNode> _distinctTargetProjects = [];
    private readonly Dictionary<string, List<string>> _matchingProjectIdsByDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PreparedEndpoint> _scratchList = [];
    private readonly HashSet<PreparedEndpoint> _scratchSeen = new(ReferenceEqualityComparer.Instance);

    public HashSet<string> ProjectsWithEntryPoints { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PreparedEntryPoint> _allEntryPoints = [];
    private readonly Dictionary<string, List<PreparedEntryPoint>> _entryPointsByProjectId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PreparedEntryPoint> _unscopedEntryPoints = [];
    private readonly List<PreparedEntryPoint> _paramOnlyEntryPoints = [];
    private readonly Dictionary<string, List<PreparedEntryPoint>> _entryPointsByRouteStem = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<PreparedEntryPoint>> _entryPointsByNorm = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProjectNode> _distinctEntryPointProjects = [];
    private readonly Dictionary<string, List<string>> _matchingEntryPointProjectIdsByDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PreparedEntryPoint> _entryScratchList = [];
    private readonly HashSet<PreparedEntryPoint> _entryScratchSeen = new(ReferenceEqualityComparer.Instance);

    public void AddEntryPoint(PreparedEntryPoint prep)
    {
        _allEntryPoints.Add(prep);
        AddEntryPointToBucket(_entryPointsByNorm, prep.EntryNorm, prep);

        if (prep.TargetProj != null)
        {
            if (ProjectsWithEntryPoints.Add(prep.TargetProj.Id))
            {
                _distinctEntryPointProjects.Add(prep.TargetProj);
            }
            AddEntryPointToBucket(_entryPointsByProjectId, prep.TargetProj.Id, prep);
        }
        else
        {
            _unscopedEntryPoints.Add(prep);
        }

        var addedStem = false;
        for (var i = 0; i < prep.Segments.Length; i++)
        {
            var seg = prep.Segments[i];
            if (IsParameterSegment(seg))
            {
                continue;
            }

            var stem = NormalizeRouteStem(seg);
            if (stem.Length == 0)
            {
                continue;
            }

            AddEntryPointToBucketUnique(_entryPointsByRouteStem, stem, prep);
            addedStem = true;
        }

        if (!addedStem)
        {
            _paramOnlyEntryPoints.Add(prep);
        }
    }

    public IReadOnlyList<PreparedEntryPoint> GetEntryPointCandidates(
        PreparedExternalService ext,
        Func<ProjectNode, string, bool> projectMatcher)
    {
        if (_allEntryPoints.Count == 0 ||
            string.Equals(ext.Service.Protocol, GraphQlProtocol, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if ((ext.RawDomain is WildcardSegment or UnknownServiceDomain or "" || ext.IsGenericHostOrService) &&
            ext.IsRootOrGenericPath)
        {
            return [];
        }

        if (!ext.IsPathSegment && !ext.IsGenericHostOrService)
        {
            return CollectDomainScopedEntryPoints(ext, projectMatcher);
        }

        if (ext.IsRootOrGenericPath)
        {
            return CollectRootDomainEntryPoints(ext, projectMatcher);
        }

        return CollectStemEntryPoints(ext);
    }

    private IReadOnlyList<PreparedEntryPoint> CollectDomainScopedEntryPoints(
        PreparedExternalService ext,
        Func<ProjectNode, string, bool> projectMatcher)
    {
        var matchingProjIds = GetMatchingEntryPointProjectIds(ext.Service.DomainOrService, projectMatcher);
        if (matchingProjIds.Count == 0 && (ext.IsRootOrGenericPath || _unscopedEntryPoints.Count == 0))
        {
            return [];
        }

        _entryScratchList.Clear();
        for (var i = 0; i < matchingProjIds.Count; i++)
        {
            if (_entryPointsByProjectId.TryGetValue(matchingProjIds[i], out var list))
            {
                _entryScratchList.AddRange(list);
            }
        }

        if (!ext.IsRootOrGenericPath && _unscopedEntryPoints.Count > 0)
        {
            _entryScratchList.AddRange(_unscopedEntryPoints);
        }

        return _entryScratchList;
    }

    private IReadOnlyList<PreparedEntryPoint> CollectRootDomainEntryPoints(
        PreparedExternalService ext,
        Func<ProjectNode, string, bool> projectMatcher)
    {
        var matchingProjIds = GetMatchingEntryPointProjectIds(ext.Service.DomainOrService, projectMatcher);
        if (matchingProjIds.Count == 0)
        {
            return [];
        }

        _entryScratchList.Clear();
        for (var i = 0; i < matchingProjIds.Count; i++)
        {
            if (_entryPointsByProjectId.TryGetValue(matchingProjIds[i], out var list))
            {
                _entryScratchList.AddRange(list);
            }
        }

        return _entryScratchList;
    }

    private IReadOnlyList<PreparedEntryPoint> CollectStemEntryPoints(PreparedExternalService ext)
    {
        _entryScratchList.Clear();
        _entryScratchSeen.Clear();
        var anyStatic = AddEntryPointStemMatches(ext.Segments);

        if (!string.IsNullOrEmpty(ext.ServiceDomainNorm) &&
            ext.ServiceDomainNorm != WildcardSegment &&
            ext.ServiceDomainNorm != UnknownServiceDomain)
        {
            if (_entryPointsByNorm.TryGetValue(ext.ServiceDomainNorm, out var exactNorm))
            {
                AddUniqueEntryPoints(exactNorm);
            }

            if (ext.ServiceDomainNorm.StartsWith('/'))
            {
                var domainSegs = ext.ServiceDomainNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (AddEntryPointStemMatches(domainSegs))
                {
                    anyStatic = true;
                }
            }
        }

        if (!anyStatic)
        {
            return _allEntryPoints;
        }

        AddUniqueEntryPoints(_paramOnlyEntryPoints);
        return _entryScratchList;
    }

    private bool AddEntryPointStemMatches(string[] segments)
    {
        var foundStatic = false;
        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (IsParameterSegment(seg))
            {
                continue;
            }

            foundStatic = true;
            var stem = NormalizeRouteStem(seg);
            if (stem.Length > 0 && _entryPointsByRouteStem.TryGetValue(stem, out var matches))
            {
                AddUniqueEntryPoints(matches);
            }
        }
        return foundStatic;
    }

    private void AddUniqueEntryPoints(List<PreparedEntryPoint> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (_entryScratchSeen.Add(item))
            {
                _entryScratchList.Add(item);
            }
        }
    }

    private List<string> GetMatchingEntryPointProjectIds(string? domain, Func<ProjectNode, string, bool> projectMatcher)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return [];
        }

        if (_matchingEntryPointProjectIdsByDomain.TryGetValue(domain, out var cached))
        {
            return cached;
        }

        var matches = new List<string>();
        for (var i = 0; i < _distinctEntryPointProjects.Count; i++)
        {
            var proj = _distinctEntryPointProjects[i];
            if (projectMatcher(proj, domain))
            {
                matches.Add(proj.Id);
            }
        }

        _matchingEntryPointProjectIdsByDomain[domain] = matches;
        return matches;
    }

    private static void AddEntryPointToBucket(
        Dictionary<string, List<PreparedEntryPoint>> index,
        string key,
        PreparedEntryPoint ep)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<PreparedEntryPoint>(InitialBucketCapacity);
            index[key] = list;
        }
        list.Add(ep);
    }

    private static void AddEntryPointToBucketUnique(
        Dictionary<string, List<PreparedEntryPoint>> index,
        string key,
        PreparedEntryPoint ep)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<PreparedEntryPoint>(InitialBucketCapacity);
            index[key] = list;
            list.Add(ep);
            return;
        }

        if (!ReferenceEquals(list[^1], ep))
        {
            list.Add(ep);
        }
    }

    public void Add(PreparedEndpoint prep)
    {
        if (prep.TargetProj != null && ProjectsWithEndpoints.Add(prep.TargetProj.Id))
        {
            _distinctTargetProjects.Add(prep.TargetProj);
        }

        if (prep.HasStaticSegment)
        {
            AddToBucket(ByShape, prep.Canonical.CanonicalShape, prep);
        }

        var proto = prep.Endpoint.Protocol;
        if (string.Equals(proto, GraphQlProtocol, StringComparison.OrdinalIgnoreCase))
        {
            AddToBucket(_graphQlByRoute, prep.RouteNorm, prep);
            return;
        }

        if (IsRpcProtocolName(proto))
        {
            _rpcEndpoints.Add(prep);
            return;
        }

        _httpEndpoints.Add(prep);
        if (prep.TargetProj != null)
        {
            AddToBucket(_httpByProjectId, prep.TargetProj.Id, prep);
        }
        else
        {
            _unscopedHttpEndpoints.Add(prep);
        }

        IndexHttpRouteStems(prep);
    }

    public IReadOnlyList<PreparedEndpoint> GetFallbackCandidates(
        PreparedExternalService ext,
        IReadOnlyList<PreparedEndpoint> allEndpoints,
        Func<ProjectNode, string, bool> projectMatcher)
    {
        var extProto = ext.Service.Protocol;
        if (string.Equals(extProto, GraphQlProtocol, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrEmpty(ext.ServicePathNorm) &&
                   _graphQlByRoute.TryGetValue(ext.ServicePathNorm, out var gqlMatches)
                ? gqlMatches
                : [];
        }

        if ((ext.RawDomain is WildcardSegment or UnknownServiceDomain or "" || ext.IsGenericHostOrService) &&
            ext.IsRootOrGenericPath)
        {
            return [];
        }

        if (IsRpcProtocolName(extProto))
        {
            return allEndpoints;
        }

        if (!ext.IsPathSegment && !ext.IsGenericHostOrService)
        {
            return CollectDomainScopedCandidates(ext, projectMatcher);
        }

        if (ext.IsRootOrGenericPath)
        {
            return CollectRootDomainCandidates(ext, projectMatcher);
        }

        return CollectRouteStemCandidates(ext);
    }

    private IReadOnlyList<PreparedEndpoint> CollectDomainScopedCandidates(
        PreparedExternalService ext,
        Func<ProjectNode, string, bool> projectMatcher)
    {
        var matchingProjIds = GetMatchingProjectIds(ext.Service.DomainOrService, projectMatcher);
        if (matchingProjIds.Count == 0 &&
            (ext.IsRootOrGenericPath || _unscopedHttpEndpoints.Count == 0) &&
            _rpcEndpoints.Count == 0)
        {
            return [];
        }

        _scratchList.Clear();
        for (var i = 0; i < matchingProjIds.Count; i++)
        {
            if (_httpByProjectId.TryGetValue(matchingProjIds[i], out var projEndpoints))
            {
                _scratchList.AddRange(projEndpoints);
            }
        }

        if (!ext.IsRootOrGenericPath && _unscopedHttpEndpoints.Count > 0)
        {
            _scratchList.AddRange(_unscopedHttpEndpoints);
        }

        if (_rpcEndpoints.Count > 0)
        {
            _scratchList.AddRange(_rpcEndpoints);
        }

        return _scratchList;
    }

    private IReadOnlyList<PreparedEndpoint> CollectRootDomainCandidates(
        PreparedExternalService ext,
        Func<ProjectNode, string, bool> projectMatcher)
    {
        var matchingProjIds = GetMatchingProjectIds(ext.Service.DomainOrService, projectMatcher);
        if (matchingProjIds.Count == 0 && _rpcEndpoints.Count == 0)
        {
            return [];
        }

        _scratchList.Clear();
        for (var i = 0; i < matchingProjIds.Count; i++)
        {
            if (_httpByProjectId.TryGetValue(matchingProjIds[i], out var projEndpoints))
            {
                _scratchList.AddRange(projEndpoints);
            }
        }

        if (_rpcEndpoints.Count > 0)
        {
            _scratchList.AddRange(_rpcEndpoints);
        }

        return _scratchList;
    }

    private IReadOnlyList<PreparedEndpoint> CollectRouteStemCandidates(PreparedExternalService ext)
    {
        _scratchList.Clear();
        _scratchSeen.Clear();
        var anyNonParamStem = AddStemMatchesForSegments(ext.Segments);

        if (!string.IsNullOrEmpty(ext.ServiceDomainNorm) &&
            ext.ServiceDomainNorm != WildcardSegment &&
            ext.ServiceDomainNorm != UnknownServiceDomain &&
            ext.ServiceDomainNorm.StartsWith('/'))
        {
            var domainSegs = ext.ServiceDomainNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (AddStemMatchesForSegments(domainSegs))
            {
                anyNonParamStem = true;
            }
        }

        if (!anyNonParamStem)
        {
            return _httpEndpoints.Count > 0 || _rpcEndpoints.Count > 0
                ? CombineHttpAndRpc()
                : [];
        }

        AddUniqueRange(_paramOnlyHttpEndpoints);
        AddUniqueRange(_rpcEndpoints);
        return _scratchList;
    }

    private IReadOnlyList<PreparedEndpoint> CombineHttpAndRpc()
    {
        if (_rpcEndpoints.Count == 0)
        {
            return _httpEndpoints;
        }

        _scratchList.Clear();
        _scratchList.AddRange(_httpEndpoints);
        _scratchList.AddRange(_rpcEndpoints);
        return _scratchList;
    }

    private bool AddStemMatchesForSegments(string[] segments)
    {
        var foundStatic = false;
        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (IsParameterSegment(seg))
            {
                continue;
            }

            foundStatic = true;
            var stem = NormalizeRouteStem(seg);
            if (stem.Length > 0 && _httpByRouteStem.TryGetValue(stem, out var matches))
            {
                AddUniqueRange(matches);
            }
        }
        return foundStatic;
    }

    private void AddUniqueRange(List<PreparedEndpoint> endpoints)
    {
        for (var i = 0; i < endpoints.Count; i++)
        {
            var ep = endpoints[i];
            if (_scratchSeen.Add(ep))
            {
                _scratchList.Add(ep);
            }
        }
    }

    private List<string> GetMatchingProjectIds(string? domain, Func<ProjectNode, string, bool> projectMatcher)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return [];
        }

        if (_matchingProjectIdsByDomain.TryGetValue(domain, out var cached))
        {
            return cached;
        }

        var matches = new List<string>();
        for (var i = 0; i < _distinctTargetProjects.Count; i++)
        {
            var proj = _distinctTargetProjects[i];
            if (projectMatcher(proj, domain))
            {
                matches.Add(proj.Id);
            }
        }

        _matchingProjectIdsByDomain[domain] = matches;
        return matches;
    }

    private void IndexHttpRouteStems(PreparedEndpoint prep)
    {
        var addedAnyStem = false;
        for (var i = 0; i < prep.Segments.Length; i++)
        {
            var seg = prep.Segments[i];
            if (IsParameterSegment(seg))
            {
                continue;
            }

            var stem = NormalizeRouteStem(seg);
            if (stem.Length == 0)
            {
                continue;
            }

            AddToBucketUnique(_httpByRouteStem, stem, prep);
            addedAnyStem = true;
        }

        if (!addedAnyStem)
        {
            _paramOnlyHttpEndpoints.Add(prep);
        }
    }

    private static bool IsParameterSegment(string seg) =>
        seg == WildcardSegment ||
        seg.StartsWith(':') ||
        (seg.StartsWith('{') && seg.EndsWith('}'));

    private static string NormalizeRouteStem(string seg)
    {
        var clean = seg.Replace(Hyphen, string.Empty).Replace(Underscore, string.Empty).ToLowerInvariant();
        if (clean.EndsWith(ServiceSuffix, StringComparison.Ordinal))
        {
            clean = clean[..^ServiceSuffix.Length];
        }
        return clean.TrimEnd('s');
    }

    private static bool IsRpcProtocolName(string? protocol) =>
        string.Equals(protocol, GrpcProtocol, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(protocol, DubboProtocol, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(protocol, TriProtocol, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(protocol, RpcProtocol, StringComparison.OrdinalIgnoreCase);

    private static void AddToBucket(
        Dictionary<string, List<PreparedEndpoint>> index,
        string key,
        PreparedEndpoint endpoint)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<PreparedEndpoint>(InitialBucketCapacity);
            index[key] = list;
        }
        list.Add(endpoint);
    }

    private static void AddToBucketUnique(
        Dictionary<string, List<PreparedEndpoint>> index,
        string key,
        PreparedEndpoint endpoint)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<PreparedEndpoint>(InitialBucketCapacity);
            index[key] = list;
            list.Add(endpoint);
            return;
        }

        if (!ReferenceEquals(list[^1], endpoint))
        {
            list.Add(endpoint);
        }
    }
}
