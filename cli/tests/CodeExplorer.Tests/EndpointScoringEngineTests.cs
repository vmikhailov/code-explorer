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
        Assert.That(matchTraffic.IsInternal, Is.True);
        Assert.That(matchTraffic.Project, Is.Not.Null);
        Assert.That(matchTraffic.Project!.ProjectId, Is.EqualTo("p1"));
        Assert.That(matchTraffic.ProjectScore, Is.GreaterThanOrEqualTo(80.0));

        // 2. postbackpartner call
        var matchPostback = EndpointScoringEngine.MatchCall("http://postbackpartner", "/postback", "POST", candidates);
        Assert.That(matchPostback.IsInternal, Is.True);
        Assert.That(matchPostback.Project, Is.Not.Null);
        Assert.That(matchPostback.Project!.ProjectId, Is.EqualTo("p2"));
        Assert.That(matchPostback.ProjectScore, Is.GreaterThanOrEqualTo(80.0));

        // 3. FQDN subdomain call
        var matchRule = EndpointScoringEngine.MatchCall("http://rule-configurator.at-systems.biz", "/rules", "GET", candidates);
        Assert.That(matchRule.IsInternal, Is.True);
        Assert.That(matchRule.Project, Is.Not.Null);
        Assert.That(matchRule.Project!.ProjectId, Is.EqualTo("p3"));
        Assert.That(matchRule.ProjectScore, Is.GreaterThanOrEqualTo(80.0));
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
        Assert.That(matchStripe.IsInternal, Is.False);
        Assert.That(matchStripe.ProjectScore, Is.LessThan(40.0));

        var matchTelegram = EndpointScoringEngine.MatchCall("https://api.telegram.org", "/bot123/sendMessage", "POST", candidates);
        Assert.That(matchTelegram.IsInternal, Is.False);
        Assert.That(matchTelegram.ProjectScore, Is.LessThan(40.0));
    }

    [Test]
    public void ScoreEndpointMatch_RouteTemplateAndMethod_CalculatesConfidence()
    {
        var epCandidate = new EndpointMatchCandidate("ep1", "/api/v1/traffic-types/:id", "GET", "p1");

        var scoreMatch = EndpointScoringEngine.ScoreEndpointMatch("/api/v1/traffic-types/123", "GET", epCandidate);
        var scoreMismatchMethod = EndpointScoringEngine.ScoreEndpointMatch("/api/v1/traffic-types/123", "POST", epCandidate);

        Assert.That(scoreMatch, Is.GreaterThan(50.0));
        Assert.That(scoreMatch, Is.GreaterThan(scoreMismatchMethod));
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
        Assert.That(matchRtb.IsInternal, Is.False);
        Assert.That(matchRtb.ProjectScore, Is.LessThan(40.0));

        // Generic placeholders should score 0
        var matchStar = EndpointScoringEngine.MatchCall("*", "/", "GET", candidates);
        Assert.That(matchStar.IsInternal, Is.False);
        Assert.That(matchStar.ProjectScore, Is.EqualTo(0.0));

        var matchUnknown = EndpointScoringEngine.MatchCall("unknown-service", "/", "GET", candidates);
        Assert.That(matchUnknown.IsInternal, Is.False);
        Assert.That(matchUnknown.ProjectScore, Is.EqualTo(0.0));
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
        Assert.That(matchSelf.Project, Is.Not.Null);
        Assert.That(matchSelf.Project!.ProjectId, Is.EqualTo("p3"));

        var scoreWorkersSite = EndpointScoringEngine.ScoreProjectMatch("adhub-cf-worker.mikhailov-v-atsystems.workers.dev", candidates[1]);
        Assert.That(scoreWorkersSite, Is.LessThan(40.0));

        var scoreCfWorkers = EndpointScoringEngine.ScoreProjectMatch("adhub-cf-worker.mikhailov-v-atsystems.workers.dev", candidates[0]);
        Assert.That(scoreCfWorkers, Is.LessThan(40.0));
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
        Assert.That(match.IsInternal, Is.True);
        Assert.That(match.Endpoint, Is.Not.Null);
        Assert.That(match.Endpoint!.EndpointId, Is.EqualTo("ep_supervisor_onboarding"));
        Assert.That(match.EndpointScore, Is.GreaterThanOrEqualTo(50.0));
        Assert.That(match.Project, Is.Not.Null);
        Assert.That(match.Project!.ProjectId, Is.EqualTo("ws:p::"));
    }

    [Test]
    public void IsRelativeOrInternalPath_ValidatesPathsCorrectly()
    {
        Assert.That(EndpointScoringEngine.IsRelativeOrInternalPath("/supervisor/onboarding"), Is.True);
        Assert.That(EndpointScoringEngine.IsRelativeOrInternalPath("/auth/me"), Is.True);
        Assert.That(EndpointScoringEngine.IsRelativeOrInternalPath("https://api.telegram.org"), Is.False);
        Assert.That(EndpointScoringEngine.IsRelativeOrInternalPath("http://localhost:8000"), Is.False);
        Assert.That(EndpointScoringEngine.IsRelativeOrInternalPath("//protocol-relative"), Is.False);
        Assert.That(EndpointScoringEngine.IsRelativeOrInternalPath(""), Is.False);
        Assert.That(EndpointScoringEngine.IsRelativeOrInternalPath(null), Is.False);
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

        Assert.That(isMatch, Is.True);
        Assert.That(matchedEp, Is.Not.Null);
        Assert.That(matchedEp!.EndpointId, Is.EqualTo("ep_flags"));
        Assert.That(score, Is.GreaterThanOrEqualTo(50.0));
    }
}
