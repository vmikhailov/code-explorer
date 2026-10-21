using CodeExplorer.Core.Parser.Layers;
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

        SemanticTopologySynthesizer.DetectAwsResourceRoles(source, packages)
            .Should().BeEquivalentTo("encrypted_shares", "database_chunks", "share_encryption_keys");

        SemanticTopologySynthesizer.DetectAwsResourceRoles(source, new HashSet<string>())
            .Should().BeEmpty();
    }

    [Test]
    public void EvaluatePeerMeshEvidence_RequiresTwoIndependentSignals()
    {
        var singleSignal = SemanticTopologySynthesizer.EvaluatePeerMeshEvidence(
            "let party_id = config.party_id;",
            "",
            collectiveCompute: false);

        singleSignal.PeerConfiguration.Should().BeTrue();
        singleSignal.IsMesh.Should().BeFalse();

        var peerConfigurationAndTransport = SemanticTopologySynthesizer.EvaluatePeerMeshEvidence(
            "let party_id = config.party_id; TcpListener::bind(addr); TcpStream::connect(addr);",
            "",
            collectiveCompute: false);

        peerConfigurationAndTransport.IsMesh.Should().BeTrue();
        peerConfigurationAndTransport.Protocols.Should().Contain("TCP/mTLS");
    }

    [Test]
    public void EvaluatePeerMeshEvidence_DerivesMemberCountFromComposeParticipants()
    {
        var evidence = SemanticTopologySynthesizer.EvaluatePeerMeshEvidence(
            "let party_id = config.party_id;",
            """
            services:
              hawk_participant_0:
              hawk_participant_1:
              unrelated:
            """,
            collectiveCompute: false);

        evidence.IsMesh.Should().BeTrue();
        evidence.MemberCount.Should().Be(2);
        evidence.ComposeReplicas.Should().BeTrue();
    }
}
