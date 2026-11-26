using CodeExplorer.Parsers.Rust.Libraries;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SemanticTopologySynthesizerTests
{
    [Test]
    public void DetectAwsResourceRoles_RequiresBothSdkAndResourceConfigurationEvidence()
    {
        var source = "shares_bucket_name db_chunks_bucket_name kms_key_arns";
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aws-sdk-s3",
            "aws-sdk-kms"
        };

        AwsSdkRustLibraryParser.DetectAwsResourceRoles(source, packages)
            .Should().BeEquivalentTo("encrypted_shares", "database_chunks", "share_encryption_keys");

        AwsSdkRustLibraryParser.DetectAwsResourceRoles(source, new HashSet<string>())
            .Should().BeEmpty();
    }

    [Test]
    public void DetectAwsResourceRoles_DetectsGraphCheckpointBucket()
    {
        var source = "download_graph_checkpoint(&aws_clients.checkpoint_s3_client, &config.graph_checkpoint_bucket_name)";
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "aws-sdk-s3" };

        AwsSdkRustLibraryParser.DetectAwsResourceRoles(source, packages)
            .Should().BeEquivalentTo("graph_checkpoints");
    }
}
