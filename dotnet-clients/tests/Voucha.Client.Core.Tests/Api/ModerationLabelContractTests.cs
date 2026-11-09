using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.ModerationIntegrity;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationLabelContractTests
{
  [Fact]
  public void ReportsPreserveNullableIdentityFactsAndFactualTarget()
  {
    var report = Assert.Single(Decode<StaffFlatModerationReportsResponse>("native.moderation.reports.default").Reports);
    Assert.Equal("Native post", report.TargetLabel);
    Assert.Null(report.ReporterDisplayName);
    Assert.Equal("alice", ModerationLabels.Person(report.ReporterDisplayName, report.ReporterUsername, UiLocalization.English));
    Assert.Null(report.ResolverUsername);
    Assert.Null(report.ResolverDisplayName);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetModerationRebasedTargetUnavailable),
        ModerationLabels.Person(report.ResolverDisplayName, report.ResolverUsername, UiLocalization.English));
    var clusters = Decode<StaffClusteredModerationReportsResponse>("native.moderation.reports.clustered.default");
    Assert.All(clusters.Clusters, cluster => Assert.False(string.IsNullOrWhiteSpace(cluster.TargetLabel)));
    var member = Decode<MemberFlatModerationReportsResponse>("native.moderation.reports.member.default");
    Assert.All(member.Reports, value => Assert.False(string.IsNullOrWhiteSpace(value.TargetLabel)));
  }

  [Theory]
  [InlineData("native.moderation.topic-claims.verification")]
  [InlineData("native.moderation.topic-claims.rejection")]
  [InlineData("native.moderation.topic-claims.revocation")]
  public void ClaimMutationsRetainHydratedTopicAndNullableClaimant(string fixture)
  {
    var claim = Decode<TopicClaimMutationResponse>(fixture).Claim;
    Assert.Equal("Example topic", claim.TopicName);
    Assert.Equal("claimant", claim.ClaimantUsername);
    Assert.Null(claim.ClaimantDisplayName);
    Assert.Equal("Example topic", Assert.Single(Decode<PendingTopicClaimsResponse>(
        "native.moderation.topic-claims.pending").Claims).TopicName);
  }

  [Fact]
  public void NotesPreserveUnavailableAuthorAndCommunityWithoutIdFallback()
  {
    var notes = Decode<UserModerationNotesResponse>("native.moderation.mod-notes.default").Notes;
    var unavailable = notes.Single(note => note.AuthorUsername is null);
    Assert.Null(unavailable.AuthorDisplayName);
    Assert.Null(unavailable.CommunityName);
    var label = ModerationLabels.Person(unavailable.AuthorDisplayName, unavailable.AuthorUsername, UiLocalization.English);
    Assert.DoesNotContain(unavailable.AuthorUserId!, label, StringComparison.Ordinal);
    Assert.Equal("moderator", Decode<UserModerationNoteResponse>("native.moderation.mod-notes.create.default").Note.AuthorUsername);
  }

  [Fact]
  public void IntegrityRowsUseEntityLabelsAndPenaltyNames()
  {
    var flags = Decode<ReportIntegrityFlagsResponse>("native.moderation.report-integrity.default");
    Assert.All(flags.Results, flag => Assert.Equal(flag.EntityLabel,
        UiLocalization.English.Resolve(IntegrityFlagPresentation.ReportRow(flag, UiLocalization.English).Entity.Label)));
    var votes = Decode<VoteIntegrityFlagsResponse>("native.moderation.vote-integrity.default");
    Assert.All(votes.Results, flag => Assert.Equal(flag.EntityLabel,
        UiLocalization.English.Resolve(IntegrityFlagPresentation.VoteRow(flag, UiLocalization.English).Entity.Label)));
    var penalties = Decode<VoteIntegrityPenaltiesResponse>("native.moderation.vote-integrity.penalties.default");
    Assert.Null(Decode<VoteWeightPenaltyResponse>("native.moderation.vote-integrity.penalties.revoke").Penalty.RevokerDisplayName);
    Assert.Contains(Decode<ReportIntegrityPenaltiesResponse>("native.moderation.report-integrity.penalties.default").Results,
        penalty => penalty.CreatedById is null);
    foreach (var penalty in penalties.Results)
    {
      var row = IntegrityPenaltyPresentation.Vote(penalty);
      Assert.Equal(ModerationLabels.Person(penalty.UserDisplayName, penalty.UserUsername, UiLocalization.English),
          UiLocalization.English.Resolve(row.UserLabel));
      Assert.Equal(ModerationLabels.Person(penalty.CreatorDisplayName, penalty.CreatorUsername, UiLocalization.English),
          UiLocalization.English.Resolve(row.CreatorLabel));
    }
  }

  [Fact]
  public void ReviewQueueAndDisputesUseCanonicalPostLabels()
  {
    var posts = Decode<AdminReviewQueueResponse>("native.moderation.review-queue.default").Results;
    foreach (var post in posts)
    {
      var row = new ReviewQueueRow(post, post.ClearanceStatus, UiLocalization.English, [], false,
          false, false, false, false);
      Assert.Equal(post.PostLabel, row.DisplayTitle);
      Assert.DoesNotContain(post.CreatedById ?? "no creator", row.Author, StringComparison.Ordinal);
    }
    var disputes = Decode<StaffModerationDisputeListResponse>("native.moderation.disputes.default");
    Assert.All(disputes.Disputes, dispute => Assert.False(string.IsNullOrWhiteSpace(dispute.PostLabel)));
  }

  [Fact]
  public void RequiredLabelsRejectMissingOrNullCanonicalFixtureFields()
  {
    var json = System.Text.Json.Nodes.JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.reports.default"))!;
    json["results"]![0]!["target_label"] = null;
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StaffFlatModerationReportsResponse>(json.ToJsonString(), VouchaApiJson.Options));
    json["results"]![0]!.AsObject().Remove("target_label");
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StaffFlatModerationReportsResponse>(json.ToJsonString(), VouchaApiJson.Options));
  }

  [Fact]
  public void DisplayNamesWinOverUsernamesAndBlankNamesUseLocalizedFallback()
  {
    Assert.Equal("Display", ModerationLabels.Person(" Display ", "handle", UiLocalization.English));
    Assert.Equal("handle", ModerationLabels.Person(" ", "handle", UiLocalization.English));
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetModerationRebasedTargetUnavailable),
        ModerationLabels.Person(null, null, UiLocalization.English));
  }

  private static T Decode<T>(string fixture) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixture), VouchaApiJson.Options)!;
}
