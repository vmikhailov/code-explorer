using CodeExplorer.Core.Analysis;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class EndpointScoringEngineTests
{
    [Test]
    public void ScoreProjectMatch_ExactAndNormalizedMatches_ScoreAboveThreshold()
    {
        var candidates = new List<ProjectMatchCandidate>
        {
            new("p1", "internal-service-traffic-types", "c:/work/ats/src/services/traffic-types"),
            new("p2", "integration-service-postback-partner", "c:/work/ats/src/services/postback-partner"),
            new("p3", "internal-service-rule-configurator", "c:/work/ats/src/services/rule-configurator")
        };

        // 1. traffictypes call
        var matchTraffic = EndpointScoringEngine.MatchCall("http://traffictypes", "/api/v1/types", "GET", candidates);
        matchTraffic.IsInternal.Should().BeTrue();
        matchTraffic.Project.Should().NotBeNull();
        matchTraffic.Project!.ProjectId.Should().Be("p1");
        matchTraffic.ProjectScore.Should().BeGreaterThanOrEqualTo(80.0);

        // 2. postbackpartner call
        var matchPostback = EndpointScoringEngine.MatchCall("http://postbackpartner", "/postback", "POST", candidates);
        matchPostback.IsInternal.Should().BeTrue();
        matchPostback.Project.Should().NotBeNull();
        matchPostback.Project!.ProjectId.Should().Be("p2");
        matchPostback.ProjectScore.Should().BeGreaterThanOrEqualTo(80.0);

        // 3. FQDN subdomain call
        var matchRule = EndpointScoringEngine.MatchCall("http://rule-configurator.at-systems.biz", "/rules", "GET", candidates);
        matchRule.IsInternal.Should().BeTrue();
        matchRule.Project.Should().NotBeNull();
        matchRule.Project!.ProjectId.Should().Be("p3");
        matchRule.ProjectScore.Should().BeGreaterThanOrEqualTo(80.0);
    }

    [Test]
    public void ScoreProjectMatch_ExternalPublicTld_RejectedOrLowScore()
    {
        var candidates = new List<ProjectMatchCandidate>
        {
            new("p1", "internal-service-traffic-types", "c:/work/ats/src/services/traffic-types")
        };

        // Public external domain should not match internal project
        var matchStripe = EndpointScoringEngine.MatchCall("https://api.stripe.com", "/v1/charges", "POST", candidates);
        matchStripe.IsInternal.Should().BeFalse();
        matchStripe.ProjectScore.Should().BeLessThan(40.0);

        var matchTelegram = EndpointScoringEngine.MatchCall("https://api.telegram.org", "/bot123/sendMessage", "POST", candidates);
        matchTelegram.IsInternal.Should().BeFalse();
        matchTelegram.ProjectScore.Should().BeLessThan(40.0);
    }

    [Test]
    public void ScoreEndpointMatch_RouteTemplateAndMethod_CalculatesConfidence()
    {
        var epCandidate = new EndpointMatchCandidate("ep1", "/api/v1/traffic-types/:id", "GET", "p1");

        var scoreMatch = EndpointScoringEngine.ScoreEndpointMatch("/api/v1/traffic-types/123", "GET", epCandidate);
        var scoreMismatchMethod = EndpointScoringEngine.ScoreEndpointMatch("/api/v1/traffic-types/123", "POST", epCandidate);

        scoreMatch.Should().BeGreaterThan(50.0);
        scoreMatch.Should().BeGreaterThan(scoreMismatchMethod);
    }

    [Test]
    public void ScoreProjectMatch_ShortTokensAndGenericPlaceholders_Rejected()
    {
        var candidates = new List<ProjectMatchCandidate>
        {
            new("p1", "internal-service-action-scheduler", "c:/work/ats/src/services/action-scheduler"),
            new("p2", "workers-site", "c:/work/ats/src/services/cf-worker/workers/tracker-worker/workers-site"),
            new("p3", "cf-worker-service-bindings", "c:/work/ats/src/services/cf-worker-service-bindings")
        };

        // Short token "rtb" should NOT match arbitrary projects by substring
        var matchRtb = EndpointScoringEngine.MatchCall("http://rtb", "/", "POST", candidates);
        matchRtb.IsInternal.Should().BeFalse();
        matchRtb.ProjectScore.Should().BeLessThan(40.0);

        // Generic placeholders should score 0
        var matchStar = EndpointScoringEngine.MatchCall("*", "/", "GET", candidates);
        matchStar.IsInternal.Should().BeFalse();
        matchStar.ProjectScore.Should().Be(0.0);

        var matchUnknown = EndpointScoringEngine.MatchCall("unknown-service", "/", "GET", candidates);
        matchUnknown.IsInternal.Should().BeFalse();
        matchUnknown.ProjectScore.Should().Be(0.0);
    }

    [Test]
    public void ScoreProjectMatch_WorkersDevSubdomain_DoesNotMatchUnrelatedWorkerProjects()
    {
        var candidates = new List<ProjectMatchCandidate>
        {
            new("p1", "atscfworkers", "c:/work/ats/src/services/cf-worker"),
            new("p2", "workers-site", "c:/work/ats/src/services/cf-worker/workers/tracker-worker/workers-site"),
            new("p3", "adhub-cf-worker", "c:/work/ats/src/services/adhub-cf-worker")
        };

        // adhub-cf-worker's workers.dev URL should match adhub-cf-worker, but NOT atscfworkers or workers-site
        var matchSelf = EndpointScoringEngine.MatchCall("https://adhub-cf-worker.mikhailov-v-atsystems.workers.dev", "/endpoint", "POST", candidates);
        matchSelf.Project.Should().NotBeNull();
        matchSelf.Project!.ProjectId.Should().Be("p3");

        var scoreWorkersSite = EndpointScoringEngine.ScoreProjectMatch("adhub-cf-worker.mikhailov-v-atsystems.workers.dev", candidates[1]);
        scoreWorkersSite.Should().BeLessThan(40.0);

        var scoreCfWorkers = EndpointScoringEngine.ScoreProjectMatch("adhub-cf-worker.mikhailov-v-atsystems.workers.dev", candidates[0]);
        scoreCfWorkers.Should().BeLessThan(40.0);
    }

    [Test]
    public void MatchCall_RelativePathMatchingInternalEndpoint_InfersOwningProjectAndScoresAsInternal()
    {
        var projects = new List<ProjectMatchCandidate>
        {
            new("ws:p::", "hearai", "c:/work/hearai", null, false),
            new("ws:p:flutter/android/app:", "app", "c:/work/hearai/flutter/android/app", null, false)
        };

        var endpoints = new List<EndpointMatchCandidate>
        {
            new("ep_supervisor_onboarding", "/supervisor/onboarding", "POST", "ws:p::"),
            new("ep_supervisor_children", "/supervisor/children", "GET", "ws:p::"),
            new("ep_auth_me", "/auth/me", "GET", "ws:p::")
        };

        // Relative path without host: should match the internal endpoint and infer owning project hearai
        var match = EndpointScoringEngine.MatchCall("/supervisor/onboarding", null, "POST", projects, endpoints);
        match.IsInternal.Should().BeTrue();
        match.Endpoint.Should().NotBeNull();
        match.Endpoint!.EndpointId.Should().Be("ep_supervisor_onboarding");
        match.EndpointScore.Should().BeGreaterThanOrEqualTo(50.0);
        match.Project.Should().NotBeNull();
        match.Project!.ProjectId.Should().Be("ws:p::");
    }

    [Test]
    public void IsRelativeOrInternalPath_ValidatesPathsCorrectly()
    {
        EndpointScoringEngine.IsRelativeOrInternalPath("/supervisor/onboarding").Should().BeTrue();
        EndpointScoringEngine.IsRelativeOrInternalPath("/auth/me").Should().BeTrue();
        EndpointScoringEngine.IsRelativeOrInternalPath("https://api.telegram.org").Should().BeFalse();
        EndpointScoringEngine.IsRelativeOrInternalPath("http://localhost:8000").Should().BeFalse();
        EndpointScoringEngine.IsRelativeOrInternalPath("//protocol-relative").Should().BeFalse();
        EndpointScoringEngine.IsRelativeOrInternalPath("").Should().BeFalse();
        EndpointScoringEngine.IsRelativeOrInternalPath(null).Should().BeFalse();
    }

    [Test]
    public void IsInternalEndpointMatch_DetectsMatchingEndpoint()
    {
        var endpoints = new List<EndpointMatchCandidate>
        {
            new("ep_flags", "/flags/{flag_id}/resolve", "POST", "ws:p::")
        };

        var isMatch = EndpointScoringEngine.IsInternalEndpointMatch(
            "/flags/42/resolve",
            "POST",
            endpoints,
            out var matchedEp,
            out var score);

        isMatch.Should().BeTrue();
        matchedEp.Should().NotBeNull();
        matchedEp!.EndpointId.Should().Be("ep_flags");
        score.Should().BeGreaterThanOrEqualTo(50.0);
    }
}
