using System.Text;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Canonical;

public readonly record struct CanonicalRouteSegment(
    string Text,
    bool IsParameter,
    RouteParamType ParamType = RouteParamType.Any,
    bool HasExplicitType = false,
    bool IsLiteral = false,
    bool IsWildcard = false)
{
    public static CanonicalRouteSegment Static(string text) =>
        new(text, IsParameter: false);

    public static CanonicalRouteSegment Parameter(
        string name,
        RouteParamType paramType = RouteParamType.Any,
        bool hasExplicitType = false,
        bool isLiteral = false,
        bool isWildcard = false) =>
        new(
            name,
            IsParameter: true,
            paramType,
            hasExplicitType,
            isLiteral,
            isWildcard || paramType == RouteParamType.WildcardPath);
}

public sealed record CanonicalRoute(
    string RawRoute,
    string CanonicalTemplate,
    string CanonicalShape,
    string TypedShape,
    IReadOnlyList<CanonicalRouteSegment> Segments,
    bool HasLeadingSlash,
    bool HasTrailingSlash,
    string? QueryString = null);

/// <summary>
/// Zero-regex, bijectively reversible canonical route sub-parser and dialect formatter.
/// Normalizes route parameter syntax ({id:guid}, :id, &lt;int:id&gt;, [id], [...slug], /42) into a single
/// canonical grammar ({name:type} / {=literal:type} / {*name:path}) while strictly preserving
/// static segment names (/users and /user are never collapsed).
/// </summary>
public static class RouteCanonicalParser
{
    private const char SlashChar = '/';
    private const char QuestionChar = '?';
    private const char ColonChar = ':';
    private const char OpenBraceChar = '{';
    private const char CloseBraceChar = '}';
    private const char OpenAngleChar = '<';
    private const char CloseAngleChar = '>';
    private const char OpenBracketChar = '[';
    private const char CloseBracketChar = ']';
    private const char OpenParenChar = '(';
    private const char CloseParenChar = ')';
    private const char StarChar = '*';
    private const char DollarChar = '$';
    private const char EqualsChar = '=';
    private const char DotChar = '.';
    private const char DashChar = '-';
    private const char PercentChar = '%';
    private const char ZeroChar = '0';
    private const char NineChar = '9';

    private const string RootSlash = "/";
    private const string EmptySlot = "{}";
    private const string WildcardSlot = "{*}";
    private const string TypedWildcardSlot = "{*:path}";
    private const string EllipsisPrefix = "...";
    private const string OptionalCatchAllPrefix = "[...";
    private const string OptionalCatchAllSuffix = "]";
    private const string DollarBracePrefix = "${";
    private const string PathTypeToken = "path";
    private const string GuidConstraintToken = "guid";
    private const string DateTimeConstraintToken = "datetime";
    private const string AlphaConstraintToken = "alpha";
    private const string WildcardSegmentStr = "*";
    private const string TrueLiteral = "true";
    private const string FalseLiteral = "false";
    private const string ParseUuidPipeMarker = "ParseUUIDPipe";
    private const string ParseIntPipeMarker = "ParseIntPipe";
    private const string ParseFloatPipeMarker = "ParseFloatPipe";
    private const string ParseBoolPipeMarker = "ParseBoolPipe";
    private const string RegexDigitsPattern = "[0-9]+";
    private const string RegexDigitShorthand = "\\d+";

    public static bool MayHaveNamedParameters(string? rawRoute)
    {
        if (string.IsNullOrEmpty(rawRoute)) return false;
        var span = rawRoute.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (c is OpenBraceChar or ColonChar or OpenAngleChar or OpenBracketChar)
            {
                return true;
            }
        }
        return false;
    }

    public static CanonicalRoute Parse(
        string? rawRoute,
        RouteDialect dialect = RouteDialect.Auto,
        IReadOnlyDictionary<string, RouteParamType>? handlerParamTypes = null)
    {
        if (string.IsNullOrEmpty(rawRoute))
        {
            return new CanonicalRoute(
                string.Empty,
                RootSlash,
                RootSlash,
                RootSlash,
                Array.Empty<CanonicalRouteSegment>(),
                HasLeadingSlash: true,
                HasTrailingSlash: false);
        }

        var span = rawRoute.AsSpan().Trim();
        if (span.IsEmpty)
        {
            return new CanonicalRoute(
                rawRoute,
                RootSlash,
                RootSlash,
                RootSlash,
                Array.Empty<CanonicalRouteSegment>(),
                HasLeadingSlash: true,
                HasTrailingSlash: false);
        }

        string? queryString = null;
        var qIdx = span.IndexOf(QuestionChar);
        if (qIdx >= 0)
        {
            queryString = span[(qIdx + 1)..].ToString();
            span = span[..qIdx];
        }

        if (span.IsEmpty || (span.Length == 1 && span[0] == SlashChar))
        {
            return new CanonicalRoute(
                rawRoute,
                RootSlash,
                RootSlash,
                RootSlash,
                Array.Empty<CanonicalRouteSegment>(),
                HasLeadingSlash: true,
                HasTrailingSlash: false,
                queryString);
        }

        var hasLeadingSlash = span[0] == SlashChar;
        var hasTrailingSlash = span.Length > 1 && span[^1] == SlashChar;

        var trimmed = span.Trim(SlashChar);
        var segments = new List<CanonicalRouteSegment>(8);

        var start = 0;
        for (var i = 0; i <= trimmed.Length; i++)
        {
            if (i == trimmed.Length || trimmed[i] == SlashChar)
            {
                var segSpan = trimmed[start..i];
                if (!segSpan.IsEmpty)
                {
                    var parsedSeg = ParseSegment(segSpan, dialect, handlerParamTypes);
                    segments.Add(parsedSeg);
                }
                start = i + 1;
            }
        }

        var canonicalTemplate = BuildCanonicalString(segments, hasLeadingSlash, hasTrailingSlash, ModeCanonicalTemplate);
        var canonicalShape = BuildCanonicalString(segments, hasLeadingSlash, hasTrailingSlash, ModeCanonicalShape);
        var typedShape = BuildCanonicalString(segments, hasLeadingSlash, hasTrailingSlash, ModeTypedShape);

        return new CanonicalRoute(
            rawRoute,
            canonicalTemplate,
            canonicalShape,
            typedShape,
            segments,
            hasLeadingSlash,
            hasTrailingSlash,
            queryString);
    }

    public static string ToCanonical(
        string rawRoute,
        RouteDialect dialect = RouteDialect.Auto,
        IReadOnlyDictionary<string, RouteParamType>? handlerParamTypes = null) =>
        Parse(rawRoute, dialect, handlerParamTypes).CanonicalTemplate;

    public static string ToCanonicalShape(
        string rawRoute,
        RouteDialect dialect = RouteDialect.Auto) =>
        Parse(rawRoute, dialect).CanonicalShape;

    public static string FromCanonical(string canonicalRoute, RouteDialect targetDialect)
    {
        var parsed = Parse(canonicalRoute, RouteDialect.Canonical);
        return Format(parsed, targetDialect);
    }

    public static string Format(CanonicalRoute route, RouteDialect targetDialect)
    {
        if (route.Segments.Count == 0)
        {
            return AppendQuery(RootSlash, route.QueryString);
        }

        var sb = new StringBuilder(route.CanonicalTemplate.Length + 8);
        if (route.HasLeadingSlash)
        {
            sb.Append(SlashChar);
        }

        for (var i = 0; i < route.Segments.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(SlashChar);
            }

            var seg = route.Segments[i];
            if (!seg.IsParameter)
            {
                sb.Append(seg.Text);
                continue;
            }

            FormatParameterSegment(sb, seg, targetDialect);
        }

        if (route.HasTrailingSlash)
        {
            sb.Append(SlashChar);
        }

        return AppendQuery(sb.ToString(), route.QueryString);
    }

    public static bool AreTypesCompatible(RouteParamType callerType, RouteParamType endpointType)
    {
        if (callerType == RouteParamType.Any || endpointType == RouteParamType.Any)
        {
            return true;
        }

        if (callerType == RouteParamType.WildcardPath || endpointType == RouteParamType.WildcardPath)
        {
            return true;
        }

        if (callerType == endpointType)
        {
            return true;
        }

        // String parameter on endpoint accepts any scalar value
        if (endpointType == RouteParamType.String)
        {
            return true;
        }

        // Float endpoint accepts integer arguments
        if (endpointType == RouteParamType.Float && callerType == RouteParamType.Int)
        {
            return true;
        }

        return false;
    }

    public static bool AreRoutesTypeCompatible(CanonicalRoute callerRoute, CanonicalRoute endpointRoute)
    {
        var minLen = Math.Min(callerRoute.Segments.Count, endpointRoute.Segments.Count);
        for (var i = 1; i <= minLen; i++)
        {
            var callerSeg = callerRoute.Segments[^i];
            var epSeg = endpointRoute.Segments[^i];

            if (callerSeg.IsParameter && epSeg.IsParameter)
            {
                if (!AreTypesCompatible(callerSeg.ParamType, epSeg.ParamType))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static RouteParamType MapLanguageTypeToRouteParamType(string? rawTypeText, string? decoratorOrPipeText = null)
    {
        if (!string.IsNullOrEmpty(decoratorOrPipeText))
        {
            if (decoratorOrPipeText.Contains(ParseUuidPipeMarker, StringComparison.OrdinalIgnoreCase))
                return RouteParamType.Uuid;
            if (decoratorOrPipeText.Contains(ParseIntPipeMarker, StringComparison.OrdinalIgnoreCase))
                return RouteParamType.Int;
            if (decoratorOrPipeText.Contains(ParseFloatPipeMarker, StringComparison.OrdinalIgnoreCase))
                return RouteParamType.Float;
            if (decoratorOrPipeText.Contains(ParseBoolPipeMarker, StringComparison.OrdinalIgnoreCase))
                return RouteParamType.Bool;
        }

        if (string.IsNullOrWhiteSpace(rawTypeText))
        {
            return RouteParamType.Any;
        }

        var span = rawTypeText.AsSpan().Trim();

        // Unwrap generic wrappers like Path<Uuid>, web::Path<i64>, Optional<UUID>, Nullable<Guid>
        var openAngle = span.LastIndexOf(OpenAngleChar);
        var closeAngle = span.IndexOf(CloseAngleChar);
        if (openAngle >= 0 && closeAngle > openAngle)
        {
            span = span.Slice(openAngle + 1, closeAngle - openAngle - 1).Trim();
        }

        // Strip nullable suffix '?'
        if (span.Length > 1 && span[^1] == QuestionChar)
        {
            span = span[..^1].Trim();
        }

        // Strip namespace/module prefix (e.g. System.Guid -> Guid, uuid.UUID -> UUID)
        var lastDot = span.LastIndexOf(DotChar);
        if (lastDot >= 0 && lastDot + 1 < span.Length)
        {
            span = span[(lastDot + 1)..];
        }
        var lastColon = span.LastIndexOf(ColonChar);
        if (lastColon >= 0 && lastColon + 1 < span.Length)
        {
            span = span[(lastColon + 1)..];
        }

        return TaxonomyEnumExtendedExtensions.ParseRouteParamType(span.ToString());
    }

    private const string ParameterSubToken = "parameter";
    private const char SingleQuoteChar = '\'';
    private const char DoubleQuoteChar = '"';

    public static IReadOnlyDictionary<string, RouteParamType>? ExtractHandlerParameterTypes(SyntaxNode node)
    {
        if (!node.IsValid()) return null;

        FunctionDeclSyntax? fnDecl = null;
        var curr = node;
        while (curr.IsValid())
        {
            if (curr is FunctionDeclSyntax fn)
            {
                fnDecl = fn;
                break;
            }
            curr = curr.Parent;
        }

        if (fnDecl == null)
        {
            foreach (var desc in node.Descendants())
            {
                if (desc is FunctionDeclSyntax descFn)
                {
                    fnDecl = descFn;
                    break;
                }
            }
        }

        if (fnDecl == null || !fnDecl.IsValid()) return null;

        Dictionary<string, RouteParamType>? map = null;
        if (fnDecl.Parameters.Count > 0)
        {
            foreach (var p in fnDecl.Parameters)
            {
                var pName = p.Name;
                var pType = p.ParameterType?.Text;
                var mapped = MapLanguageTypeToRouteParamType(pType, p.Text);
                if (!string.IsNullOrEmpty(pName) && mapped != RouteParamType.Any)
                {
                    map ??= new Dictionary<string, RouteParamType>(StringComparer.OrdinalIgnoreCase);
                    map[pName] = mapped;
                }
                TryExtractAliasParamName(p, mapped, ref map);
            }
        }

        var paramList = fnDecl.GetField(SyntaxFields.Parameters);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.Children)
            {
                if (param is not ParameterDeclSyntax && !param.Type.Contains(ParameterSubToken, StringComparison.Ordinal))
                {
                    continue;
                }

                var nameNode = param.GetField(SyntaxFields.Name) ??
                               param.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                var typeNode = param.GetField(SyntaxFields.Type);
                var pName = nameNode.IsValid() ? nameNode.Text : null;
                var pType = typeNode.IsValid() ? typeNode.Text : null;
                var mapped = MapLanguageTypeToRouteParamType(pType, param.Text);
                if (!string.IsNullOrEmpty(pName) && mapped != RouteParamType.Any)
                {
                    map ??= new Dictionary<string, RouteParamType>(StringComparer.OrdinalIgnoreCase);
                    map[pName] = mapped;
                }
                TryExtractAliasParamName(param, mapped, ref map);
            }
        }

        return map;
    }

    private static void TryExtractAliasParamName(
        SyntaxNode paramNode,
        RouteParamType mappedType,
        ref Dictionary<string, RouteParamType>? map)
    {
        if (mappedType == RouteParamType.Any || !paramNode.IsValid()) return;

        foreach (var child in paramNode.Descendants())
        {
            if (DeclarativeSemanticExtension.IsStringLiteral(child))
            {
                var alias = child.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                if (!string.IsNullOrEmpty(alias))
                {
                    map ??= new Dictionary<string, RouteParamType>(StringComparer.OrdinalIgnoreCase);
                    map[alias] = mappedType;
                }
            }
        }
    }

    private const int ModeCanonicalTemplate = 0;
    private const int ModeCanonicalShape = 1;
    private const int ModeTypedShape = 2;

    private static string BuildCanonicalString(
        List<CanonicalRouteSegment> segments,
        bool hasLeadingSlash,
        bool hasTrailingSlash,
        int mode)
    {
        if (segments.Count == 0)
        {
            return RootSlash;
        }

        var sb = new StringBuilder(segments.Count * 12);
        if (hasLeadingSlash)
        {
            sb.Append(SlashChar);
        }

        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(SlashChar);
            }

            var seg = segments[i];
            if (!seg.IsParameter)
            {
                sb.Append(seg.Text);
                continue;
            }

            if (mode == ModeCanonicalShape)
            {
                sb.Append(seg.IsWildcard ? WildcardSlot : EmptySlot);
            }
            else if (mode == ModeTypedShape)
            {
                if (seg.IsWildcard)
                {
                    sb.Append(TypedWildcardSlot);
                }
                else if (seg.ParamType != RouteParamType.Any)
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(ColonChar);
                    sb.Append(seg.ParamType.ToCanonicalString());
                    sb.Append(CloseBraceChar);
                }
                else
                {
                    sb.Append(EmptySlot);
                }
            }
            else
            {
                FormatParameterSegment(sb, seg, RouteDialect.Canonical);
            }
        }

        if (hasTrailingSlash)
        {
            sb.Append(SlashChar);
        }

        return sb.ToString();
    }

    private static void FormatParameterSegment(StringBuilder sb, CanonicalRouteSegment seg, RouteDialect targetDialect)
    {
        switch (targetDialect)
        {
            case RouteDialect.ColonParam:
                if (seg.IsWildcard)
                {
                    sb.Append(StarChar);
                    sb.Append(seg.Text);
                }
                else if (seg.HasExplicitType && seg.ParamType != RouteParamType.Any)
                {
                    sb.Append(ColonChar);
                    sb.Append(seg.Text);
                    sb.Append(OpenParenChar);
                    sb.Append(seg.ParamType.ToCanonicalString());
                    sb.Append(CloseParenChar);
                }
                else
                {
                    sb.Append(ColonChar);
                    sb.Append(seg.Text);
                }
                break;

            case RouteDialect.FlaskAngle:
                if (seg.IsWildcard)
                {
                    sb.Append(OpenAngleChar);
                    sb.Append(PathTypeToken);
                    sb.Append(ColonChar);
                    sb.Append(seg.Text);
                    sb.Append(CloseAngleChar);
                }
                else if (seg.HasExplicitType && seg.ParamType != RouteParamType.Any)
                {
                    sb.Append(OpenAngleChar);
                    sb.Append(seg.ParamType.ToCanonicalString());
                    sb.Append(ColonChar);
                    sb.Append(seg.Text);
                    sb.Append(CloseAngleChar);
                }
                else
                {
                    sb.Append(OpenAngleChar);
                    sb.Append(seg.Text);
                    sb.Append(CloseAngleChar);
                }
                break;

            case RouteDialect.NextBracket:
                if (seg.IsWildcard)
                {
                    sb.Append(OpenBracketChar);
                    sb.Append(EllipsisPrefix);
                    sb.Append(seg.Text);
                    sb.Append(CloseBracketChar);
                }
                else if (seg.HasExplicitType && seg.ParamType != RouteParamType.Any)
                {
                    sb.Append(OpenBracketChar);
                    sb.Append(seg.Text);
                    sb.Append(ColonChar);
                    sb.Append(seg.ParamType.ToCanonicalString());
                    sb.Append(CloseBracketChar);
                }
                else
                {
                    sb.Append(OpenBracketChar);
                    sb.Append(seg.Text);
                    sb.Append(CloseBracketChar);
                }
                break;

            case RouteDialect.AspNet:
                if (seg.IsWildcard)
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(StarChar);
                    sb.Append(seg.Text);
                    sb.Append(CloseBraceChar);
                }
                else if (seg.HasExplicitType && seg.ParamType != RouteParamType.Any)
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(seg.Text);
                    sb.Append(ColonChar);
                    sb.Append(MapToAspNetConstraint(seg.ParamType));
                    sb.Append(CloseBraceChar);
                }
                else
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(seg.Text);
                    sb.Append(CloseBraceChar);
                }
                break;

            case RouteDialect.OutboundClient:
                if (seg.IsLiteral || seg.Text == WildcardSegmentStr)
                {
                    sb.Append(seg.Text);
                }
                else if (seg.HasExplicitType && seg.ParamType != RouteParamType.Any)
                {
                    sb.Append(DollarBracePrefix);
                    sb.Append(seg.Text);
                    sb.Append(ColonChar);
                    sb.Append(seg.ParamType.ToCanonicalString());
                    sb.Append(CloseBraceChar);
                }
                else
                {
                    sb.Append(DollarBracePrefix);
                    sb.Append(seg.Text);
                    sb.Append(CloseBraceChar);
                }
                break;

            case RouteDialect.Canonical:
            case RouteDialect.Auto:
            default:
                if (seg.IsWildcard)
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(StarChar);
                    sb.Append(seg.Text);
                    sb.Append(ColonChar);
                    sb.Append(PathTypeToken);
                    sb.Append(CloseBraceChar);
                }
                else if (seg.IsLiteral)
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(EqualsChar);
                    sb.Append(seg.Text);
                    sb.Append(ColonChar);
                    sb.Append(seg.ParamType.ToCanonicalString());
                    sb.Append(CloseBraceChar);
                }
                else if (seg.HasExplicitType && seg.ParamType != RouteParamType.Any)
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(seg.Text);
                    sb.Append(ColonChar);
                    sb.Append(seg.ParamType.ToCanonicalString());
                    sb.Append(CloseBraceChar);
                }
                else
                {
                    sb.Append(OpenBraceChar);
                    sb.Append(seg.Text);
                    sb.Append(CloseBraceChar);
                }
                break;
        }
    }

    private static string MapToAspNetConstraint(RouteParamType paramType) => paramType switch
    {
        RouteParamType.Uuid => GuidConstraintToken,
        RouteParamType.Date => DateTimeConstraintToken,
        RouteParamType.String => AlphaConstraintToken,
        _ => paramType.ToCanonicalString()
    };

    private static CanonicalRouteSegment ParseSegment(
        ReadOnlySpan<char> seg,
        RouteDialect dialect,
        IReadOnlyDictionary<string, RouteParamType>? handlerParamTypes)
    {
        // 1. JS / Template interpolation: ${id} or ${id:type}
        if (seg.Length >= 4 && seg[0] == DollarChar && seg[1] == OpenBraceChar && seg[^1] == CloseBraceChar)
        {
            return ParseBraceBody(seg[2..^1], handlerParamTypes);
        }

        // 2. Brace syntax: {id}, {id:guid}, {*slug}, {**slug}, {*slug:path}, {=42:int}
        if (seg.Length >= 2 && seg[0] == OpenBraceChar && seg[^1] == CloseBraceChar)
        {
            var inner = seg[1..^1];
            if (inner.IsEmpty)
            {
                return CanonicalRouteSegment.Parameter(string.Empty);
            }
            return ParseBraceBody(inner, handlerParamTypes);
        }

        // 3. Flask / Werkzeug angle syntax: <id>, <int:id>, <path:subpath>
        if (seg.Length >= 3 && seg[0] == OpenAngleChar && seg[^1] == CloseAngleChar)
        {
            var inner = seg[1..^1];
            var colonIdx = inner.IndexOf(ColonChar);
            if (colonIdx > 0)
            {
                var typeSpan = inner[..colonIdx];
                var nameSpan = inner[(colonIdx + 1)..];
                var paramName = nameSpan.ToString();
                var parsedType = TaxonomyEnumExtendedExtensions.ParseRouteParamType(typeSpan.ToString());
                var isWild = parsedType == RouteParamType.WildcardPath;
                return CanonicalRouteSegment.Parameter(paramName, parsedType, hasExplicitType: true, isLiteral: false, isWildcard: isWild);
            }
            else
            {
                var paramName = inner.ToString();
                return EnrichWithHandlerType(paramName, RouteParamType.Any, hasExplicitType: false, handlerParamTypes);
            }
        }

        // 4. Next.js / SvelteKit bracket syntax: [id], [...slug], [[...slug]], [id:int]
        if (seg.Length >= 3 && seg[0] == OpenBracketChar && seg[^1] == CloseBracketChar)
        {
            var inner = seg[1..^1];
            if (inner.StartsWith(OptionalCatchAllPrefix.AsSpan(), StringComparison.Ordinal) &&
                inner.EndsWith(OptionalCatchAllSuffix.AsSpan(), StringComparison.Ordinal) &&
                inner.Length > 5)
            {
                var catchAllName = inner[4..^1].ToString();
                return CanonicalRouteSegment.Parameter(catchAllName, RouteParamType.WildcardPath, hasExplicitType: true, isLiteral: false, isWildcard: true);
            }
            if (inner.StartsWith(EllipsisPrefix.AsSpan(), StringComparison.Ordinal) && inner.Length > 3)
            {
                var catchAllName = inner[3..].ToString();
                return CanonicalRouteSegment.Parameter(catchAllName, RouteParamType.WildcardPath, hasExplicitType: true, isLiteral: false, isWildcard: true);
            }

            var colonIdx = inner.IndexOf(ColonChar);
            if (colonIdx > 0)
            {
                var paramName = inner[..colonIdx].ToString();
                var parsedType = TaxonomyEnumExtendedExtensions.ParseRouteParamType(inner[(colonIdx + 1)..].ToString());
                return CanonicalRouteSegment.Parameter(paramName, parsedType, hasExplicitType: true);
            }

            return EnrichWithHandlerType(inner.ToString(), RouteParamType.Any, hasExplicitType: false, handlerParamTypes);
        }

        // 5. Express / NestJS / Gin colon syntax: :id, :id(int), :id([0-9]+)
        if (seg.Length >= 2 && seg[0] == ColonChar)
        {
            var body = seg[1..];
            var parenOpen = body.IndexOf(OpenParenChar);
            if (parenOpen > 0 && body[^1] == CloseParenChar)
            {
                var paramName = body[..parenOpen].ToString();
                var constraint = body.Slice(parenOpen + 1, body.Length - parenOpen - 2);
                var constraintStr = constraint.ToString();
                var parsedType = (constraintStr is RegexDigitsPattern or RegexDigitShorthand)
                    ? RouteParamType.Int
                    : TaxonomyEnumExtendedExtensions.ParseRouteParamType(constraintStr);
                return CanonicalRouteSegment.Parameter(paramName, parsedType, hasExplicitType: parsedType != RouteParamType.Any);
            }

            return EnrichWithHandlerType(body.ToString(), RouteParamType.Any, hasExplicitType: false, handlerParamTypes);
        }

        // 6. Wildcard segment: * or *filepath
        if (seg[0] == StarChar)
        {
            if (seg.Length == 1)
            {
                return CanonicalRouteSegment.Parameter(WildcardSegmentStr, RouteParamType.Any, hasExplicitType: false);
            }
            var wildName = seg[1..].ToString();
            return CanonicalRouteSegment.Parameter(wildName, RouteParamType.WildcardPath, hasExplicitType: true, isLiteral: false, isWildcard: true);
        }

        // 7. Format specifier in printf/Sprintf outbound calls: %d, %s, %v, %f
        if (seg.Length == 2 && seg[0] == PercentChar)
        {
            var spec = char.ToLowerInvariant(seg[1]);
            var specStr = seg.ToString();
            return spec switch
            {
                'd' or 'i' or 'u' => CanonicalRouteSegment.Parameter(specStr, RouteParamType.Int, hasExplicitType: true),
                'f' or 'g' => CanonicalRouteSegment.Parameter(specStr, RouteParamType.Float, hasExplicitType: true),
                's' or 'v' => CanonicalRouteSegment.Parameter(specStr, RouteParamType.Any, hasExplicitType: false),
                _ => CanonicalRouteSegment.Static(specStr)
            };
        }

        // 8. OutboundClient literal value classification (/users/42/orders, /users/550e8400.../orders)
        if (dialect == RouteDialect.OutboundClient && TryClassifyOutboundLiteral(seg, out var litType))
        {
            return CanonicalRouteSegment.Parameter(
                seg.ToString(),
                litType,
                hasExplicitType: true,
                isLiteral: true,
                isWildcard: false);
        }

        // 9. Static segment — preserved 100% verbatim (/users != /user)
        return CanonicalRouteSegment.Static(seg.ToString());
    }

    private static CanonicalRouteSegment ParseBraceBody(
        ReadOnlySpan<char> inner,
        IReadOnlyDictionary<string, RouteParamType>? handlerParamTypes)
    {
        // Canonical literal slot: {=42:int}
        if (inner[0] == EqualsChar && inner.Length > 1)
        {
            var rest = inner[1..];
            var colonIdx = rest.LastIndexOf(ColonChar);
            if (colonIdx > 0)
            {
                var litText = rest[..colonIdx].ToString();
                var litType = TaxonomyEnumExtendedExtensions.ParseRouteParamType(rest[(colonIdx + 1)..].ToString());
                return CanonicalRouteSegment.Parameter(litText, litType, hasExplicitType: true, isLiteral: true);
            }
            return CanonicalRouteSegment.Parameter(rest.ToString(), RouteParamType.Any, hasExplicitType: false, isLiteral: true);
        }

        // Wildcard slot: {*slug}, {**slug}, {*slug:path}
        if (inner[0] == StarChar)
        {
            var rest = inner[1..];
            if (!rest.IsEmpty && rest[0] == StarChar)
            {
                rest = rest[1..];
            }
            var colonIdx = rest.IndexOf(ColonChar);
            var wildName = (colonIdx > 0 ? rest[..colonIdx] : rest).ToString();
            if (string.IsNullOrEmpty(wildName))
            {
                wildName = WildcardSegmentStr;
            }
            return CanonicalRouteSegment.Parameter(wildName, RouteParamType.WildcardPath, hasExplicitType: true, isLiteral: false, isWildcard: true);
        }

        // Standard {name:constraint} or {name}
        var cIdx = inner.IndexOf(ColonChar);
        if (cIdx > 0)
        {
            var paramName = inner[..cIdx].ToString();
            var constraintSpan = inner[(cIdx + 1)..];
            var parsedType = TaxonomyEnumExtendedExtensions.ParseRouteParamType(constraintSpan.ToString());
            var isWild = parsedType == RouteParamType.WildcardPath;
            return CanonicalRouteSegment.Parameter(
                paramName,
                parsedType,
                hasExplicitType: parsedType != RouteParamType.Any,
                isLiteral: false,
                isWildcard: isWild);
        }

        return EnrichWithHandlerType(inner.ToString(), RouteParamType.Any, hasExplicitType: false, handlerParamTypes);
    }

    private static CanonicalRouteSegment EnrichWithHandlerType(
        string paramName,
        RouteParamType defaultType,
        bool hasExplicitType,
        IReadOnlyDictionary<string, RouteParamType>? handlerParamTypes)
    {
        if (!hasExplicitType &&
            handlerParamTypes != null &&
            handlerParamTypes.TryGetValue(paramName, out var enrichedType) &&
            enrichedType != RouteParamType.Any)
        {
            var isWild = enrichedType == RouteParamType.WildcardPath;
            return CanonicalRouteSegment.Parameter(
                paramName,
                enrichedType,
                hasExplicitType: true,
                isLiteral: false,
                isWildcard: isWild);
        }

        return CanonicalRouteSegment.Parameter(paramName, defaultType, hasExplicitType);
    }

    public static bool TryClassifyOutboundLiteral(ReadOnlySpan<char> s, out RouteParamType paramType)
    {
        if (IsUuidLiteral(s))
        {
            paramType = RouteParamType.Uuid;
            return true;
        }

        if (IsIsoDateLiteral(s))
        {
            paramType = RouteParamType.Date;
            return true;
        }

        if (IsIntegerLiteral(s))
        {
            paramType = RouteParamType.Int;
            return true;
        }

        if (IsFloatLiteral(s))
        {
            paramType = RouteParamType.Float;
            return true;
        }

        if (IsBoolLiteral(s))
        {
            paramType = RouteParamType.Bool;
            return true;
        }

        paramType = RouteParamType.Any;
        return false;
    }

    private static bool IsIntegerLiteral(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty || s.Length > 20) return false;
        var start = 0;
        if (s[0] == DashChar)
        {
            if (s.Length == 1) return false;
            start = 1;
        }

        for (var i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (c < ZeroChar || c > NineChar) return false;
        }

        return true;
    }

    private static bool IsFloatLiteral(ReadOnlySpan<char> s)
    {
        if (s.Length < 3 || s.Length > 24) return false;
        var start = 0;
        if (s[0] == DashChar)
        {
            if (s.Length < 4) return false;
            start = 1;
        }

        var dotCount = 0;
        for (var i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (c == DotChar)
            {
                if (i == start || i == s.Length - 1) return false;
                dotCount++;
                if (dotCount > 1) return false;
            }
            else if (c < ZeroChar || c > NineChar)
            {
                return false;
            }
        }

        return dotCount == 1;
    }

    private static bool IsUuidLiteral(ReadOnlySpan<char> s)
    {
        if (s.Length != 36) return false;
        if (s[8] != DashChar || s[13] != DashChar || s[18] != DashChar || s[23] != DashChar)
        {
            return false;
        }

        for (var i = 0; i < 36; i++)
        {
            if (i is 8 or 13 or 18 or 23) continue;
            if (!char.IsAsciiHexDigit(s[i])) return false;
        }

        return true;
    }

    private static bool IsIsoDateLiteral(ReadOnlySpan<char> s)
    {
        if (s.Length != 10) return false;
        if (s[4] != DashChar || s[7] != DashChar) return false;

        for (var i = 0; i < 10; i++)
        {
            if (i is 4 or 7) continue;
            var c = s[i];
            if (c < ZeroChar || c > NineChar) return false;
        }

        return true;
    }

    private static bool IsBoolLiteral(ReadOnlySpan<char> s) =>
        s.Equals(TrueLiteral.AsSpan(), StringComparison.OrdinalIgnoreCase) ||
        s.Equals(FalseLiteral.AsSpan(), StringComparison.OrdinalIgnoreCase);

    private static string AppendQuery(string path, string? queryString) =>
        string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";
}
