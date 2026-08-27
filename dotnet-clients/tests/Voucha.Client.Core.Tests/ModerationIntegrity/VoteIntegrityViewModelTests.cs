using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using System.Net;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class VoteIntegrityViewModelTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Fact]
  public async Task FiltersRenderTopicDomainAndIdOnlyTargetsWithResolutionContext()
  {
    var pending = ModerationIntegrityTestService.VoteFlag(
        "pending", postId: null, hostnameId: "domain-1");
    var resolved = ModerationIntegrityTestService.VoteFlag(
        "resolved", "suspended", postId: null, topicId: "topic-2",
        resolvedAt: DateTimeOffset.Parse("2026-06-02T12:00:00Z"),
        resolvedById: "admin-2");
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (status, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage(
              status == IntegrityFlagStatus.Pending ? [pending] : [resolved])),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("/domain/domain-1", viewModel.Items.Single().Entity.Route);
    Assert.Contains("vote_count: 20", viewModel.Items.Single().Evidence);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.Resolved, TestContext.Current.CancellationToken);
    Assert.Equal("/topic/topic-2", viewModel.Items.Single().Entity.Route);
    Assert.Equal("suspended", viewModel.Items.Single().Flag.Resolution);
    Assert.Equal("admin-2", viewModel.Items.Single().Flag.ResolvedById);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);

    Assert.Equal(
        [IntegrityFlagStatus.Pending, IntegrityFlagStatus.Resolved, IntegrityFlagStatus.All],
        service.VoteFetches.Select(request => request.Status));
  }

  [Fact]
  public async Task RemainingVoteEntityKindsStayIdOnly()
  {
    var rss = ModerationIntegrityTestService.VoteFlag("rss", postId: null) with
    {
      RssFeedItemId = "rss-1",
    };
    var relation = ModerationIntegrityTestService.VoteFlag("relation", postId: null) with
    {
      EntityRelationId = "relation-1",
    };
    var agent = ModerationIntegrityTestService.VoteFlag("agent", postId: null) with
    {
      AgentModerationId = "agent-1",
    };
    var fallback = ModerationIntegrityTestService.VoteFlag("fallback", postId: null);
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage([rss, relation, agent, fallback])),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("RSS item ID rss-1", UiLocalization.English.Resolve(viewModel.Items[0].Entity.Label));
    Assert.Equal("Entity relation ID relation-1", UiLocalization.English.Resolve(viewModel.Items[1].Entity.Label));
    Assert.Equal("Agent moderation ID agent-1", UiLocalization.English.Resolve(viewModel.Items[2].Entity.Label));
    Assert.Equal("Flag ID", UiLocalization.English.Resolve(viewModel.Items[3].Entity.Label));
    Assert.All(viewModel.Items, row => Assert.Null(row.Entity.Route));
  }

  [Fact]
  public async Task VoteCursorPaginationForwardsOpaqueCursorAndPreservesRowsOnFailure()
  {
    var continuationFails = true;
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, after, _) => after is null
          ? Task.FromResult(ModerationIntegrityTestService.VotePage(
              [ModerationIntegrityTestService.VoteFlag("flag-1")],
              "opaque:cursor/value", true))
          : continuationFails
              ? Task.FromException<VoteIntegrityFlagsResponse>(
                  new HttpRequestException("vote continuation failed"))
              : Task.FromResult(ModerationIntegrityTestService.VotePage(
                  [
                    ModerationIntegrityTestService.VoteFlag("flag-1"),
                    ModerationIntegrityTestService.VoteFlag("flag-2"),
                  ])),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("opaque:cursor/value", service.VoteFetches.Last().After);
    Assert.Equal("flag-1", viewModel.Items.Single().Flag.Id);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    continuationFails = false;
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["flag-1", "flag-2"], viewModel.Items.Select(row => row.Flag.Id));
  }

  [Theory]
  [InlineData(VoteIntegrityResolution.Dismissed)]
  [InlineData(VoteIntegrityResolution.Penalized)]
  [InlineData(VoteIntegrityResolution.Suspended)]
  public async Task EveryResolutionUsesConfirmedResponse(VoteIntegrityResolution resolution)
  {
    var authoritative = ModerationIntegrityTestService.VoteFlag(
        "flag-1", resolution.ToString().ToLowerInvariant(),
        resolvedAt: DateTimeOffset.UtcNow, resolvedById: "admin-9");
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage(
              [ModerationIntegrityTestService.VoteFlag("flag-1")])),
      ResolveVote = (_, actual, _) =>
      {
        Assert.Equal(resolution, actual);
        return Task.FromResult(new VoteIntegrityFlagResponse(authoritative));
      },
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);

    await viewModel.ResolveAsync("flag-1", resolution, TestContext.Current.CancellationToken);

    Assert.Equal(resolution, service.VoteResolutions.Single());
    Assert.Equal("admin-9", viewModel.Items.Single().Flag.ResolvedById);
    Assert.False(viewModel.CanResolve("flag-1"));
  }

  [Theory]
  [InlineData(IntegrityFlagStatus.Pending, false)]
  [InlineData(IntegrityFlagStatus.Resolved, true)]
  [InlineData(IntegrityFlagStatus.All, true)]
  public async Task AuthoritativeResolutionEvictsOnlyThePendingRowAndPreservesOrder(
      IntegrityFlagStatus status,
      bool retainsResolved)
  {
    var flags = new[]
    {
      ModerationIntegrityTestService.VoteFlag("before"),
      ModerationIntegrityTestService.VoteFlag("target"),
      ModerationIntegrityTestService.VoteFlag("after"),
    };
    var authoritative = flags[1] with
    {
      Resolution = "dismissed",
      ResolvedAt = DateTimeOffset.UtcNow,
    };
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage(flags, "cursor-1", true)),
      ResolveVote = (_, _, _) => Task.FromResult(new VoteIntegrityFlagResponse(authoritative)),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);

    if (status == IntegrityFlagStatus.Pending)
      await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    else
      await viewModel.SelectStatusAsync(status, TestContext.Current.CancellationToken);
    await viewModel.ResolveAsync(
        "target", VoteIntegrityResolution.Dismissed, TestContext.Current.CancellationToken);

    Assert.Equal(
        retainsResolved ? ["before", "target", "after"] : ["before", "after"],
        viewModel.Items.Select(row => row.Flag.Id));
    Assert.True(viewModel.HasMore);
    if (retainsResolved) Assert.Equal("dismissed", viewModel.Items[1].Flag.Resolution);
  }

  [Fact]
  public async Task PenaltyDoesNotResolveAndSuccessfulRepeatIsPrevented()
  {
    var pending = ModerationIntegrityTestService.VoteFlag("flag-1");
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage([pending])),
      PenalizeVotes = (_, _) => Task.FromResult(new VoteIntegrityPenaltyApplicationResponse(3)),
      FetchVotePenalties = (_, _, sourceFlagId, _) =>
          Task.FromResult(VotePenaltyPage(sourceFlagId!)),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);
    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);

    Assert.Equal(1, service.VotePenalties);
    Assert.Equal(3, viewModel.PenalizedUserCount("flag-1"));
    Assert.Null(viewModel.Items.Single().Flag.Resolution);
    Assert.True(viewModel.CanResolve("flag-1"));
    Assert.False(viewModel.CanApplyVoteRingPenalty("flag-1"));
  }

  [Fact]
  public async Task FailureUnlocksSharedPerFlagActionForRetry()
  {
    var attempts = 0;
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage(
              [ModerationIntegrityTestService.VoteFlag("flag-1")])),
      ResolveVote = (_, _, _) =>
      {
        attempts++;
        return attempts == 1
            ? Task.FromException<VoteIntegrityFlagResponse>(
                new VouchaApiException("resolution failed", null, HttpStatusCode.BadRequest))
            : Task.FromResult(new VoteIntegrityFlagResponse(
                ModerationIntegrityTestService.VoteFlag(
                    "flag-1", "dismissed", resolvedAt: DateTimeOffset.UtcNow)));
      },
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);

    await viewModel.ResolveAsync(
        "flag-1", VoteIntegrityResolution.Dismissed, TestContext.Current.CancellationToken);
    Assert.Equal("resolution failed", viewModel.ActionError("flag-1"));
    Assert.True(viewModel.CanApplyVoteRingPenalty("flag-1"));
    await viewModel.ResolveAsync(
        "flag-1", VoteIntegrityResolution.Dismissed, TestContext.Current.CancellationToken);

    Assert.Equal("dismissed", viewModel.Items.Single().Flag.Resolution);
    Assert.Null(viewModel.ActionError("flag-1"));
  }

  [Fact]
  public async Task CanceledPenaltyRequiresReconciliationAndPreventsRepeat()
  {
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage(
              [ModerationIntegrityTestService.VoteFlag("flag-1")])),
      PenalizeVotes = (_, token) => Task.FromCanceled<VoteIntegrityPenaltyApplicationResponse>(token),
      FetchVotePenalties = (_, _, sourceFlagId, _) =>
          Task.FromResult(VotePenaltyPage(sourceFlagId!)),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", cancellation.Token);

    Assert.False(viewModel.IsActionInFlight("flag-1"));
    Assert.True(viewModel.NeedsReconciliation("flag-1"));
    Assert.False(viewModel.CanApplyVoteRingPenalty("flag-1"));
  }

  [Fact]
  public async Task HistoricalRevokedPenaltyWithoutNewRowSafelyPermitsRetry()
  {
    var flag = ModerationIntegrityTestService.VoteFlag("flag-1");
    var historical = VotePenalty("old-revoked", flag.Id, revoked: true);
    var snapshots = 0;
    var attempts = 0;
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.VotePage([flag])),
      PenalizeVotes = (_, _) => ++attempts == 1
          ? Task.FromException<VoteIntegrityPenaltyApplicationResponse>(
              new HttpRequestException("uncertain"))
          : Task.FromResult(new VoteIntegrityPenaltyApplicationResponse(2)),
      FetchVotePenalties = (_, _, sourceFlagId, _) =>
      {
        snapshots++;
        return Task.FromResult(VotePenaltyPage(sourceFlagId!, historical));
      },
      FetchVote = (_, _) => Task.FromResult(new VoteIntegrityFlagResponse(flag)),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);
    await viewModel.ReconcileVotePenaltyAsync("flag-1");

    Assert.Equal(2, snapshots);
    Assert.False(viewModel.NeedsReconciliation("flag-1"));
    Assert.True(viewModel.CanApplyVoteRingPenalty("flag-1"));
    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);
    Assert.Equal(2, service.VotePenalties);
    Assert.Equal(2, viewModel.PenalizedUserCount("flag-1"));
  }

  [Fact]
  public async Task NewPenaltyAfterHistoricalRevokedBaselineKeepsPostSuppressed()
  {
    var flag = ModerationIntegrityTestService.VoteFlag("flag-1");
    var historical = VotePenalty("old-revoked", flag.Id, revoked: true);
    var committed = VotePenalty("new-active", flag.Id, revoked: false);
    var snapshots = 0;
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.VotePage([flag])),
      PenalizeVotes = (_, _) => Task.FromException<VoteIntegrityPenaltyApplicationResponse>(
          new HttpRequestException("uncertain")),
      FetchVotePenalties = (_, _, sourceFlagId, _) => Task.FromResult(
          ++snapshots == 1
              ? VotePenaltyPage(sourceFlagId!, historical)
              : VotePenaltyPage(sourceFlagId!, historical, committed)),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);
    await viewModel.ReconcileVotePenaltyAsync("flag-1");
    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);

    Assert.Equal(2, snapshots);
    Assert.Equal(1, service.VotePenalties);
    Assert.False(viewModel.NeedsReconciliation("flag-1"));
    Assert.False(viewModel.CanApplyVoteRingPenalty("flag-1"));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task BaselineFailureOrScopeMismatchPreventsPost(bool scopeMismatch)
  {
    var flag = ModerationIntegrityTestService.VoteFlag("flag-1");
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.VotePage([flag])),
      FetchVotePenalties = (_, _, _, _) => scopeMismatch
          ? Task.FromResult(VotePenaltyPage("different-flag"))
          : Task.FromException<VoteIntegrityPenaltiesResponse>(
              new HttpRequestException("baseline unavailable")),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);

    Assert.Equal(0, service.VotePenalties);
    Assert.False(viewModel.IsActionInFlight("flag-1"));
    Assert.NotNull(viewModel.ActionError("flag-1"));
  }

  [Fact]
  public async Task ReconciliationFailureRetainsSuppressionAndBaseline()
  {
    var flag = ModerationIntegrityTestService.VoteFlag("flag-1");
    var snapshots = 0;
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.VotePage([flag])),
      PenalizeVotes = (_, _) => Task.FromException<VoteIntegrityPenaltyApplicationResponse>(
          new HttpRequestException("uncertain")),
      FetchVotePenalties = (_, _, sourceFlagId, _) => ++snapshots == 1
          ? Task.FromResult(VotePenaltyPage(sourceFlagId!))
          : Task.FromException<VoteIntegrityPenaltiesResponse>(
              new HttpRequestException("reconciliation unavailable")),
    };
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ApplyVoteRingPenaltyAsync("flag-1", TestContext.Current.CancellationToken);
    await viewModel.ReconcileVotePenaltyAsync("flag-1");

    Assert.Equal(1, service.VotePenalties);
    Assert.True(viewModel.NeedsReconciliation("flag-1"));
    Assert.False(viewModel.CanApplyVoteRingPenalty("flag-1"));
  }

  [Fact]
  public async Task ResolveAndApplyCapabilitiesAreIndependent()
  {
    var service = new ModerationIntegrityTestService
    {
      FetchVotes = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.VotePage(
          [ModerationIntegrityTestService.VoteFlag("flag-1")])),
    };
    var capabilities = new IntegrityCapabilities(true, false, true, true, true);
    var viewModel = new VoteIntegrityViewModel(service, Admin, capabilities: capabilities);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanResolve("flag-1"));
    Assert.True(viewModel.CanApplyVoteRingPenalty("flag-1"));
  }

  private static VoteWeightPenalty VotePenalty(string id, string flagId, bool revoked) => new(
      id, "user-1", 0.2, "voting_ring", flagId, "admin-1",
      revoked ? DateTimeOffset.UtcNow : null, revoked ? "admin-2" : null,
      DateTimeOffset.UtcNow);

  private static VoteIntegrityPenaltiesResponse VotePenaltyPage(
      string flagId,
      params VoteWeightPenalty[] penalties) => new(
      penalties, new PageInfo(null, false, penalties.LastOrDefault()?.Id),
      new VoteIntegrityPenaltyFilterScope("flag", flagId));
}
