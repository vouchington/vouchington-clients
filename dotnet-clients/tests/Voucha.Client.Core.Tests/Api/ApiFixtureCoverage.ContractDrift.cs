using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, Type> WithContractDriftFixtures(this Dictionary<string, Type> registry)
  {
    registry["native.copyright.repeat-infringer-accounts"] = typeof(CopyrightRepeatInfringerAccountsResponse);
    registry["web.copyright.email-notice-association-search"] = typeof(CopyrightAssociationTargetsResponse);
    registry["web.copyright.staff-queue.default"] = typeof(CopyrightStaffQueueResponse);
    registry["web.admin.psql.jobs.classifier-range"] = typeof(EngineeringSuccessResponse);
    registry["native.moderation.report-integrity.flag"] = typeof(ReportIntegrityFlagResponse);
    foreach (var kind in new[] { "vote", "report" })
    {
      var prefix = $"native.moderation.{kind}-integrity.penalties.";
      registry[prefix + "review"] = kind == "vote" ? typeof(VoteWeightPenaltyResponse) : typeof(ReportAbusePenaltyResponse);
      foreach (var suffix in new[] { "review-due", "review-due.page-1", "review-due.page-2" })
        registry[prefix + suffix] = kind == "vote" ? typeof(VoteIntegrityPenaltiesResponse) : typeof(ReportIntegrityPenaltiesResponse);
    }
    return registry;
  }

  private static Dictionary<string, ApiRequest> WithContractDriftEndpoints(this Dictionary<string, ApiRequest> registry)
  {
    registry["native.copyright.repeat-infringer-accounts"] = VouchaApiEndpoints.CopyrightRepeatInfringerAccounts(
        ApiFixtureLoader.RouteParameterValue("native.copyright.repeat-infringer-accounts", "id"));
    registry["web.copyright.email-notice-association-search"] = VouchaApiEndpoints.CopyrightAssociationTargets("Claimant");
    registry["web.copyright.staff-queue.default"] = VouchaApiEndpoints.CopyrightStaffQueue();
    registry["web.admin.psql.jobs.classifier-range"] = VouchaApiEndpoints.EnqueuePsqlJob(
        new PsqlJobBody("createPartitions", new PsqlClassifierRange("2020-02-01", "2020-02-02", 100)));
    registry["native.moderation.report-integrity.flag"] = VouchaApiEndpoints.ReportIntegrityFlag("report-flag-1");
    foreach (var kind in new[] { "vote", "report" })
    {
      var prefix = $"native.moderation.{kind}-integrity.penalties.";
      var id = ApiFixtureLoader.RouteParameterValue(prefix + "review", "id");
      registry[prefix + "review"] = kind == "vote"
          ? VouchaApiEndpoints.ReviewVoteIntegrityPenalty(id) : VouchaApiEndpoints.ReviewReportIntegrityPenalty(id);
      foreach (var suffix in new[] { "review-due", "review-due.page-1", "review-due.page-2" })
      {
        var fixture = ApiFixtureLoader.DotnetCoreRouteFixtures.Single(row => row.Id == prefix + suffix);
        fixture.Query.TryGetValue("after", out var after);
        int? limit = fixture.Query.TryGetValue("limit", out var value) ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : null;
        registry[fixture.Id] = kind == "vote"
            ? VouchaApiEndpoints.VoteIntegrityPenaltyReviewsDue(after, limit)
            : VouchaApiEndpoints.ReportIntegrityPenaltyReviewsDue(after, limit);
      }
    }
    return registry;
  }
}
