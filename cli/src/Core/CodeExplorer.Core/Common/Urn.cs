using System.Diagnostics.CodeAnalysis;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Strongly typed, resilient parser and model for CodeExplorer URNs and entity IDs.
/// Safely handles Windows drive letters, generic type parameters, and colons in identifiers.
/// </summary>
public readonly record struct Urn
{
    public string Raw { get; init; }
    public string Prefix { get; init; }
    public string Domain { get; init; }
    public string? SubDomain { get; init; }
    public string? Path { get; init; }
    public string? Kind { get; init; }
    public string? Name { get; init; }
    public int? Line { get; init; }

    public Urn(
        string raw,
        string prefix,
        string domain,
        string? subDomain = null,
        string? path = null,
        string? kind = null,
        string? name = null,
        int? line = null)
    {
        Raw = raw;
        Prefix = prefix;
        Domain = domain;
        SubDomain = subDomain;
        Path = path;
        Kind = kind;
        Name = name;
        Line = line;
    }

    public static bool TryParse([NotNullWhen(true)] string? raw, out Urn urn)
    {
        urn = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Trim();

        // 1. Check for urn:ce:{ws}:...
        if (text.StartsWith("urn:ce:", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseUrnCe(text, out urn);
        }

        // 2. Standard format: {prefix}:{domain}:{rest}
        var firstColon = text.IndexOf(':');
        if (firstColon <= 0 || firstColon == text.Length - 1)
        {
            urn = new Urn(text, "", text);
            return true;
        }

        var prefix = text[..firstColon];
        var remainder = text[(firstColon + 1)..];

        var secondColon = remainder.IndexOf(':');
        if (secondColon <= 0)
        {
            urn = new Urn(text, prefix, remainder);
            return true;
        }

        var domain = remainder[..secondColon];
        var body = remainder[(secondColon + 1)..];

        switch (domain.ToLowerInvariant())
        {
            case "symbol":
                return TryParseSymbol(text, prefix, domain, body, out urn);

            case "project":
                var projPath = body.TrimEnd(':');
                urn = new Urn(text, prefix, domain, path: projPath, name: System.IO.Path.GetFileName(projPath));
                return true;

            case "file":
            case "folder":
                urn = new Urn(text, prefix, domain, path: body, name: System.IO.Path.GetFileName(body));
                return true;

            case "res":
                return TryParseResource(text, prefix, domain, body, out urn);

            case "endpoint":
                var lastColon = body.LastIndexOf(':');
                if (lastColon > 0)
                {
                    urn = new Urn(text, prefix, domain, path: body[..lastColon], kind: body[(lastColon + 1)..]);
                }
                else
                {
                    urn = new Urn(text, prefix, domain, path: body);
                }
                return true;

            default:
                urn = new Urn(text, prefix, domain, path: body);
                return true;
        }
    }

    public static Urn Parse(string raw)
    {
        if (!TryParse(raw, out var urn))
        {
            throw new FormatException($"Invalid CodeExplorer URN format: '{raw}'");
        }
        return urn;
    }

    private static bool TryParseSymbol(string raw, string prefix, string domain, string body, out Urn urn)
    {
        // Body format: {filePath}:{kind}:{name}:{line} OR {filePath}:{kind}:{name}
        // Since filePath may contain a drive letter (e.g. C:/...), we parse from the right side.
        var lastColon = body.LastIndexOf(':');
        if (lastColon <= 0)
        {
            urn = new Urn(raw, prefix, domain, name: body);
            return true;
        }

        var lastSegment = body[(lastColon + 1)..];
        string name;
        int? line = null;
        string remainder;

        if (int.TryParse(lastSegment, out var parsedLine))
        {
            line = parsedLine;
            remainder = body[..lastColon];
            var secondLastColon = remainder.LastIndexOf(':');
            if (secondLastColon <= 0)
            {
                name = remainder;
                urn = new Urn(raw, prefix, domain, name: name, line: line);
                return true;
            }
            name = remainder[(secondLastColon + 1)..];
            remainder = remainder[..secondLastColon];
        }
        else
        {
            name = lastSegment;
            remainder = body[..lastColon];
        }

        // Now remainder is {filePath}:{kind}
        var kindColon = remainder.LastIndexOf(':');
        string? kind = null;
        string? filePath = null;

        if (kindColon > 0)
        {
            kind = remainder[(kindColon + 1)..];
            filePath = remainder[..kindColon];
        }
        else
        {
            filePath = remainder;
        }

        urn = new Urn(raw, prefix, domain, path: filePath, kind: kind, name: name, line: line);
        return true;
    }

    private static bool TryParseResource(string raw, string prefix, string domain, string body, out Urn urn)
    {
        // Body format: {subDomain}:{kind}:{name}
        var parts = body.Split(':', 3);
        var subDomain = parts.Length > 0 ? parts[0] : null;
        var kind = parts.Length > 1 ? parts[1] : null;
        var name = parts.Length > 2 ? parts[2] : null;

        urn = new Urn(raw, prefix, domain, subDomain: subDomain, kind: kind, name: name);
        return true;
    }

    private static bool TryParseUrnCe(string text, out Urn urn)
    {
        // Format: urn:ce:{ws}:{domain}:{subDomain}:{kind}:{name}
        var parts = text.Split(':');
        if (parts.Length < 4)
        {
            urn = new Urn(text, "urn:ce", parts.Length > 2 ? parts[2] : "");
            return true;
        }

        var prefix = string.Join(':', parts[..3]); // urn:ce:{ws}
        var domain = parts[3];
        var subDomain = parts.Length > 4 ? parts[4] : null;
        var kind = parts.Length > 5 ? parts[5] : null;
        var name = parts.Length > 6 ? string.Join(':', parts[6..]) : null;

        urn = new Urn(text, prefix, domain, subDomain: subDomain, kind: kind, name: name);
        return true;
    }

    public override string ToString() => Raw;
}
