using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class AiCloudSdkKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerAwsKotlin = "aws.sdk.kotlin";
    private const string MarkerOpenAiKotlin = "com.aallam.openai";
    private const string MarkerKoog = "ai.koog";
    private const string MarkerXef = "com.xebia.functional.xef";
    private const string AwsPrefix = "aws:";
    private const string AiPrefix = "llm:kotlin-ai";

    private static readonly Regex AwsClientRegex = new(
        @"\b(?<svc>S3Client|DynamoDbClient|SqsClient|SnsClient|EventBridgeClient|LambdaClient|SecretsManagerClient|KmsClient)\b",
        RegexOptions.Compiled);

    public AiCloudSdkKotlinLibraryParser()
    {
        Id = "ai-cloud-sdk-kotlin";
        Name = "AWS SDK Kotlin, OpenAI-Kotlin, Koog & Xef AI";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "aws.sdk.kotlin*",
            "com.aallam.openai*",
            "ai.koog*",
            "com.xebia.functional.xef*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (content.Contains(MarkerAwsKotlin, StringComparison.Ordinal))
        {
            foreach (Match match in AwsClientRegex.Matches(content))
            {
                var svc = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(svc))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, AwsPrefix + svc, OntologyConstants.Relationships.Calls));
                }
            }
        }

        if (content.Contains(MarkerOpenAiKotlin, StringComparison.Ordinal) ||
            content.Contains(MarkerKoog, StringComparison.Ordinal) ||
            content.Contains(MarkerXef, StringComparison.Ordinal))
        {
            fileNode.References.Add(new Reference(fileNode.Id, AiPrefix, OntologyConstants.Relationships.Calls));
        }
    }
}
