namespace CodeExplorer.Core.Parser.Canonical;

public sealed record CanonicalTopic(
    string RawTopic,
    string CanonicalTemplate,
    string CanonicalShape,
    string TopicName,
    string? Provider = null,
    string? ProjectOrRegion = null,
    string? AccountId = null,
    string? ServiceKind = null);

/// <summary>
/// Zero-regex, bijectively reversible canonical topic and queue sub-parser.
/// Normalizes GCP Pub/Sub paths (projects/p/topics/t), AWS SNS/SQS ARNs (arn:aws:sns:region:acct:t),
/// and plain broker topic names into a single canonical grammar while preserving exact topic names.
/// </summary>
public static class TopicCanonicalParser
{
    private const char SlashChar = '/';
    private const char ColonChar = ':';
    private const string CanonicalSchemePrefix = "topic://";
    private const string CanonicalPlainPrefix = "topic:";
    private const string GcpProvider = "gcp";
    private const string AwsProvider = "aws";
    private const string ProjectsPrefix = "projects/";
    private const string TopicsSegment = "topics";
    private const string SubscriptionsSegment = "subscriptions";
    private const string ArnAwsPrefix = "arn:aws:";

    public static CanonicalTopic Parse(string? rawTopic, TopicDialect dialect = TopicDialect.Auto)
    {
        if (string.IsNullOrWhiteSpace(rawTopic))
        {
            return new CanonicalTopic(rawTopic ?? string.Empty, CanonicalPlainPrefix, CanonicalPlainPrefix, string.Empty);
        }

        var span = rawTopic.AsSpan().Trim();

        // 1. Canonical format: topic://gcp/{proj}/{kind}/{name} or topic://aws/{svc}/{region}/{acct}/{name} or topic:{name}
        if (span.StartsWith(CanonicalSchemePrefix.AsSpan(), StringComparison.Ordinal))
        {
            var body = span[CanonicalSchemePrefix.Length..];
            var firstSlash = body.IndexOf(SlashChar);
            if (firstSlash > 0)
            {
                var provider = body[..firstSlash].ToString();
                var rest = body[(firstSlash + 1)..];
                if (provider == GcpProvider)
                {
                    var s1 = rest.IndexOf(SlashChar);
                    if (s1 > 0)
                    {
                        var proj = rest[..s1].ToString();
                        var afterProj = rest[(s1 + 1)..];
                        var s2 = afterProj.IndexOf(SlashChar);
                        if (s2 > 0)
                        {
                            var kind = afterProj[..s2].ToString();
                            var tName = afterProj[(s2 + 1)..].ToString();
                            return new CanonicalTopic(
                                rawTopic,
                                span.ToString(),
                                CanonicalPlainPrefix + tName,
                                tName,
                                GcpProvider,
                                proj,
                                null,
                                kind);
                        }
                    }
                }
                else if (provider == AwsProvider)
                {
                    var s1 = rest.IndexOf(SlashChar);
                    if (s1 > 0)
                    {
                        var svc = rest[..s1].ToString();
                        var afterSvc = rest[(s1 + 1)..];
                        var s2 = afterSvc.IndexOf(SlashChar);
                        if (s2 > 0)
                        {
                            var region = afterSvc[..s2].ToString();
                            var afterRegion = afterSvc[(s2 + 1)..];
                            var s3 = afterRegion.IndexOf(SlashChar);
                            if (s3 > 0)
                            {
                                var acct = afterRegion[..s3].ToString();
                                var tName = afterRegion[(s3 + 1)..].ToString();
                                return new CanonicalTopic(
                                    rawTopic,
                                    span.ToString(),
                                    CanonicalPlainPrefix + tName,
                                    tName,
                                    AwsProvider,
                                    region,
                                    acct,
                                    svc);
                            }
                        }
                    }
                }
            }
        }

        if (span.StartsWith(CanonicalPlainPrefix.AsSpan(), StringComparison.Ordinal) &&
            !span.StartsWith(CanonicalSchemePrefix.AsSpan(), StringComparison.Ordinal))
        {
            var plainName = span[CanonicalPlainPrefix.Length..].ToString();
            var tpl = CanonicalPlainPrefix + plainName;
            return new CanonicalTopic(rawTopic, tpl, tpl, plainName);
        }

        // 2. GCP Pub/Sub: projects/{project}/topics/{topic} or projects/{project}/subscriptions/{sub}
        if (span.StartsWith(ProjectsPrefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            var afterProjects = span[ProjectsPrefix.Length..];
            var slashIdx = afterProjects.IndexOf(SlashChar);
            if (slashIdx > 0)
            {
                var proj = afterProjects[..slashIdx].ToString();
                var rem = afterProjects[(slashIdx + 1)..];
                var slash2 = rem.IndexOf(SlashChar);
                if (slash2 > 0)
                {
                    var kindSpan = rem[..slash2];
                    var nameSpan = rem[(slash2 + 1)..];
                    if (!nameSpan.IsEmpty &&
                        (kindSpan.Equals(TopicsSegment.AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                         kindSpan.Equals(SubscriptionsSegment.AsSpan(), StringComparison.OrdinalIgnoreCase)))
                    {
                        var kind = kindSpan.ToString().ToLowerInvariant();
                        var tName = nameSpan.ToString();
                        var canonicalTpl = $"{CanonicalSchemePrefix}{GcpProvider}/{proj}/{kind}/{tName}";
                        return new CanonicalTopic(
                            rawTopic,
                            canonicalTpl,
                            CanonicalPlainPrefix + tName,
                            tName,
                            GcpProvider,
                            proj,
                            null,
                            kind);
                    }
                }
            }
        }

        // 3. AWS SNS / SQS ARN: arn:aws:{sns|sqs}:{region}:{account}:{name}
        if (span.StartsWith(ArnAwsPrefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            var afterArn = span[ArnAwsPrefix.Length..];
            var c1 = afterArn.IndexOf(ColonChar);
            if (c1 > 0)
            {
                var svc = afterArn[..c1].ToString().ToLowerInvariant();
                var rem1 = afterArn[(c1 + 1)..];
                var c2 = rem1.IndexOf(ColonChar);
                if (c2 > 0)
                {
                    var region = rem1[..c2].ToString();
                    var rem2 = rem1[(c2 + 1)..];
                    var c3 = rem2.IndexOf(ColonChar);
                    if (c3 > 0)
                    {
                        var acct = rem2[..c3].ToString();
                        var tName = rem2[(c3 + 1)..].ToString();
                        var canonicalTpl = $"{CanonicalSchemePrefix}{AwsProvider}/{svc}/{region}/{acct}/{tName}";
                        return new CanonicalTopic(
                            rawTopic,
                            canonicalTpl,
                            CanonicalPlainPrefix + tName,
                            tName,
                            AwsProvider,
                            region,
                            acct,
                            svc);
                    }
                }
            }
        }

        // 4. Plain topic/queue name — preserved verbatim without mutating segment names
        var verbatimName = span.ToString();
        var plainCanonical = CanonicalPlainPrefix + verbatimName;
        return new CanonicalTopic(rawTopic, plainCanonical, plainCanonical, verbatimName);
    }

    public static string ToCanonical(string rawTopic, TopicDialect dialect = TopicDialect.Auto) =>
        Parse(rawTopic, dialect).CanonicalTemplate;

    public static string ToCanonicalShape(string rawTopic, TopicDialect dialect = TopicDialect.Auto) =>
        Parse(rawTopic, dialect).CanonicalShape;

    public static string FromCanonical(string canonicalTopic, TopicDialect targetDialect) =>
        Format(Parse(canonicalTopic, TopicDialect.Canonical), targetDialect);

    public static string Format(CanonicalTopic topic, TopicDialect targetDialect) => targetDialect switch
    {
        TopicDialect.GcpPubSubTopic =>
            $"projects/{topic.ProjectOrRegion ?? "default"}/{TopicsSegment}/{topic.TopicName}",
        TopicDialect.GcpPubSubSubscription =>
            $"projects/{topic.ProjectOrRegion ?? "default"}/{SubscriptionsSegment}/{topic.TopicName}",
        TopicDialect.AwsArn =>
            $"arn:aws:{topic.ServiceKind ?? "sns"}:{topic.ProjectOrRegion ?? "us-east-1"}:{topic.AccountId ?? "000000000000"}:{topic.TopicName}",
        TopicDialect.Plain =>
            topic.TopicName,
        _ =>
            topic.CanonicalTemplate
    };
}
