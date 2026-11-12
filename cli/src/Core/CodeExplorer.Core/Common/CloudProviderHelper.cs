namespace CodeExplorer.Core.Common;

public enum CloudVendorTier
{
    Hyperscaler,
    Edge,
    ManagedData,
    Saas
}

public readonly record struct CloudProviderMatch(string ProviderId, string DisplayName, CloudVendorTier Tier)
{
    public string TierSlug => Tier switch
    {
        CloudVendorTier.Hyperscaler => "hyperscaler",
        CloudVendorTier.Edge => "edge",
        CloudVendorTier.ManagedData => "managed_data",
        _ => "saas"
    };
}

/// <summary>
/// Resolves canonical cloud platform and SaaS vendor identities (used for :Cloud nodes and -[:HOSTED_IN]-> edges)
/// from provider strings, SDK names, database engines, or outbound hostnames.
/// </summary>
public static class CloudProviderHelper
{
    private static readonly (string Token, CloudProviderMatch Match)[] KnownProviders =
    [
        ("amazonaws.com", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("aws-sdk", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("awssdk", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("aws", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("dynamodb", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("s3", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("sqs", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("sns", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("kinesis", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("eventbridge", new("aws", "AWS", CloudVendorTier.Hyperscaler)),
        ("bedrock", new("aws", "AWS", CloudVendorTier.Hyperscaler)),

        ("googleapis.com", new("gcp", "GCP", CloudVendorTier.Hyperscaler)),
        ("google.cloud", new("gcp", "GCP", CloudVendorTier.Hyperscaler)),
        ("google-cloud", new("gcp", "GCP", CloudVendorTier.Hyperscaler)),
        ("gcp", new("gcp", "GCP", CloudVendorTier.Hyperscaler)),
        ("bigquery", new("gcp", "GCP", CloudVendorTier.Hyperscaler)),
        ("firestore", new("gcp", "GCP", CloudVendorTier.Hyperscaler)),
        ("pubsub", new("gcp", "GCP", CloudVendorTier.Hyperscaler)),

        ("windows.net", new("azure", "Azure", CloudVendorTier.Hyperscaler)),
        ("azure.com", new("azure", "Azure", CloudVendorTier.Hyperscaler)),
        ("azure", new("azure", "Azure", CloudVendorTier.Hyperscaler)),
        ("cosmosdb", new("azure", "Azure", CloudVendorTier.Hyperscaler)),
        ("servicebus", new("azure", "Azure", CloudVendorTier.Hyperscaler)),

        ("cloudflarestorage", new("cloudflare", "Cloudflare", CloudVendorTier.Edge)),
        ("workers.dev", new("cloudflare", "Cloudflare", CloudVendorTier.Edge)),
        ("cloudflare", new("cloudflare", "Cloudflare", CloudVendorTier.Edge)),

        ("supabase.co", new("supabase", "Supabase", CloudVendorTier.ManagedData)),
        ("supabase", new("supabase", "Supabase", CloudVendorTier.ManagedData)),
        ("firebaseio.com", new("firebase", "Firebase", CloudVendorTier.ManagedData)),
        ("firebase", new("firebase", "Firebase", CloudVendorTier.ManagedData)),

        ("stripe.com", new("stripe", "Stripe", CloudVendorTier.Saas)),
        ("stripe", new("stripe", "Stripe", CloudVendorTier.Saas)),
        ("auth0.com", new("auth0", "Auth0", CloudVendorTier.Saas)),
        ("auth0", new("auth0", "Auth0", CloudVendorTier.Saas)),
        ("openai.com", new("openai", "OpenAI", CloudVendorTier.Saas)),
        ("openai", new("openai", "OpenAI", CloudVendorTier.Saas)),
        ("anthropic.com", new("anthropic", "Anthropic", CloudVendorTier.Saas)),
        ("anthropic", new("anthropic", "Anthropic", CloudVendorTier.Saas)),
        ("twilio.com", new("twilio", "Twilio", CloudVendorTier.Saas)),
        ("twilio", new("twilio", "Twilio", CloudVendorTier.Saas)),
        ("sendgrid.com", new("sendgrid", "SendGrid", CloudVendorTier.Saas)),
        ("sendgrid", new("sendgrid", "SendGrid", CloudVendorTier.Saas))
    ];

    public static CloudProviderMatch? TryResolve(string? nameOrHost, string? explicitProvider = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            var fromExplicit = MatchSingle(explicitProvider);
            if (fromExplicit.HasValue) return fromExplicit;
        }

        return !string.IsNullOrWhiteSpace(nameOrHost) ? MatchSingle(nameOrHost) : null;
    }

    public static CloudProviderMatch ResolveOrFallback(string nameOrHost, string? explicitProvider = null)
    {
        var resolved = TryResolve(nameOrHost, explicitProvider);
        if (resolved.HasValue) return resolved.Value;

        var raw = !string.IsNullOrWhiteSpace(explicitProvider) ? explicitProvider.Trim() : nameOrHost.Trim();
        var slug = NormalizeSlug(raw);
        return new CloudProviderMatch(slug, raw, CloudVendorTier.Saas);
    }

    public static string BuildCloudNodeId(string workspaceId, string providerId) =>
        $"{workspaceId}:{OntologyConstants.IdPrefixes.Cloud}:{NormalizeSlug(providerId)}";

    public static string NormalizeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        var s = new string(chars).Trim('_');
        return string.IsNullOrEmpty(s) ? "unknown" : s;
    }

    private static CloudProviderMatch? MatchSingle(string raw)
    {
        var lower = raw.Trim().ToLowerInvariant();
        foreach (var (token, match) in KnownProviders)
        {
            if (lower.Equals(token, StringComparison.Ordinal))
            {
                return match;
            }

            if (token.Length <= 4)
            {
                if (ContainsTokenWithBoundary(lower, token))
                {
                    return match;
                }
            }
            else if (lower.Contains(token, StringComparison.Ordinal))
            {
                return match;
            }
        }
        return null;
    }

    private static bool ContainsTokenWithBoundary(string text, string token)
    {
        var idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
        {
            var leftOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            var rightIdx = idx + token.Length;
            var rightOk = rightIdx >= text.Length || !char.IsLetterOrDigit(text[rightIdx]);
            if (leftOk && rightOk)
            {
                return true;
            }
            idx++;
        }
        return false;
    }
}
