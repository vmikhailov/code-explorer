using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Canonical;

public sealed record CanonicalRpc(
    string RawRpc,
    string CanonicalTemplate,
    string CanonicalShape,
    string ServiceName,
    string MethodName,
    string? Package = null);

/// <summary>
/// Zero-regex, bijectively reversible canonical RPC / gRPC / Dubbo / Protobuf contract sub-parser.
/// Normalizes gRPC HTTP/2 paths (/orders.v1.OrderService/CreateOrder), dot-qualified (orders.v1.OrderService.CreateOrder),
/// and colon-qualified (orders.v1.OrderService:CreateOrder) service methods without mutating service or method names.
/// </summary>
public static class RpcCanonicalParser
{
    private const char SlashChar = '/';
    private const char DotChar = '.';
    private const char ColonChar = ':';
    private const string CanonicalPrefix = "rpc://";
    private const string CanonicalShapePrefix = "rpc:";

    public static CanonicalRpc Parse(string? rawRpc, RpcDialect dialect = RpcDialect.Auto)
    {
        if (string.IsNullOrWhiteSpace(rawRpc))
        {
            return new CanonicalRpc(rawRpc ?? string.Empty, CanonicalShapePrefix, CanonicalShapePrefix, string.Empty, string.Empty);
        }

        var span = rawRpc.AsSpan().Trim();

        // 1. Canonical format: rpc://[{package}/]{service}/{method}
        if (span.StartsWith(CanonicalPrefix.AsSpan(), StringComparison.Ordinal))
        {
            var body = span[CanonicalPrefix.Length..];
            var lastSlash = body.LastIndexOf(SlashChar);
            if (lastSlash > 0)
            {
                var method = body[(lastSlash + 1)..].ToString();
                var beforeMethod = body[..lastSlash];
                var prevSlash = beforeMethod.LastIndexOf(SlashChar);
                if (prevSlash > 0)
                {
                    var pkg = beforeMethod[..prevSlash].ToString();
                    var svc = beforeMethod[(prevSlash + 1)..].ToString();
                    return BuildCanonicalRpc(rawRpc, pkg, svc, method);
                }
                return BuildCanonicalRpc(rawRpc, null, beforeMethod.ToString(), method);
            }
        }

        // 2. gRPC path format: /[package.]Service/Method
        var trimmedSlash = span.Trim(SlashChar);
        var slashIdx = trimmedSlash.LastIndexOf(SlashChar);
        if (slashIdx > 0)
        {
            var fullService = trimmedSlash[..slashIdx];
            var method = trimmedSlash[(slashIdx + 1)..].ToString();
            SplitPackageAndService(fullService, out var pkg, out var svc);
            return BuildCanonicalRpc(rawRpc, pkg, svc, method);
        }

        // 3. Colon-qualified format: [package.]Service:Method
        var colonIdx = trimmedSlash.LastIndexOf(ColonChar);
        if (colonIdx > 0)
        {
            var fullService = trimmedSlash[..colonIdx];
            var method = trimmedSlash[(colonIdx + 1)..].ToString();
            SplitPackageAndService(fullService, out var pkg, out var svc);
            return BuildCanonicalRpc(rawRpc, pkg, svc, method);
        }

        // 4. Dot-qualified format: [package.]Service.Method
        var dotIdx = trimmedSlash.LastIndexOf(DotChar);
        if (dotIdx > 0)
        {
            var fullService = trimmedSlash[..dotIdx];
            var method = trimmedSlash[(dotIdx + 1)..].ToString();
            SplitPackageAndService(fullService, out var pkg, out var svc);
            return BuildCanonicalRpc(rawRpc, pkg, svc, method);
        }

        var single = trimmedSlash.ToString();
        return BuildCanonicalRpc(rawRpc, null, single, string.Empty);
    }

    public static string ToCanonical(string rawRpc, RpcDialect dialect = RpcDialect.Auto) =>
        Parse(rawRpc, dialect).CanonicalTemplate;

    public static string ToCanonicalShape(string rawRpc, RpcDialect dialect = RpcDialect.Auto) =>
        Parse(rawRpc, dialect).CanonicalShape;

    public static string FromCanonical(string canonicalRpc, RpcDialect targetDialect) =>
        Format(Parse(canonicalRpc, RpcDialect.Canonical), targetDialect);

    public static string Format(CanonicalRpc rpc, RpcDialect targetDialect)
    {
        var qualifiedService = string.IsNullOrEmpty(rpc.Package)
            ? rpc.ServiceName
            : $"{rpc.Package}.{rpc.ServiceName}";

        return targetDialect switch
        {
            RpcDialect.GrpcPath =>
                $"/{qualifiedService}/{rpc.MethodName}",
            RpcDialect.DotQualified =>
                $"{qualifiedService}.{rpc.MethodName}",
            RpcDialect.ColonQualified =>
                $"{qualifiedService}:{rpc.MethodName}",
            _ =>
                rpc.CanonicalTemplate
        };
    }

    private static void SplitPackageAndService(ReadOnlySpan<char> fullService, out string? package, out string serviceName)
    {
        var lastDot = fullService.LastIndexOf(DotChar);
        if (lastDot > 0)
        {
            package = fullService[..lastDot].ToString();
            serviceName = fullService[(lastDot + 1)..].ToString();
        }
        else
        {
            package = null;
            serviceName = fullService.ToString();
        }
    }

    private static CanonicalRpc BuildCanonicalRpc(string rawRpc, string? package, string serviceName, string methodName)
    {
        var canonicalTemplate = string.IsNullOrEmpty(package)
            ? $"{CanonicalPrefix}{serviceName}/{methodName}"
            : $"{CanonicalPrefix}{package}/{serviceName}/{methodName}";
        var canonicalShape = string.IsNullOrEmpty(methodName)
            ? $"{CanonicalShapePrefix}{serviceName}"
            : $"{CanonicalShapePrefix}{serviceName}/{methodName}";

        return new CanonicalRpc(rawRpc, canonicalTemplate, canonicalShape, serviceName, methodName, package);
    }
}
