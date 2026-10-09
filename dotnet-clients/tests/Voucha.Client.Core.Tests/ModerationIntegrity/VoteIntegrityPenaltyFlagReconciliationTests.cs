using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class VoteIntegrityPenaltyFlagReconciliationTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Fact]
  public async Task NoNewPenaltyRefreshesPendingFlagBeforePermittingRetry()
  {
    var initial = ModerationIntegrityTestService.VoteFlag("flag-1");
    var authoritative = initial with { CreatedAt = initial.CreatedAt.AddMinutes(1) };
    var exactGets = 0;
    var service = Service([initial], (_, _) =>
    {
      exactGets++;
      return Task.FromResult(new VoteIntegrityFlagResponse(authoritative));
    });
    var viewModel = await Loaded(service);

    await AmbiguousPenaltyAndReconcile(viewModel, initial.Id);

    Assert.Equal(1, exactGets);
    Assert.Same(authoritative, viewModel.Items.Single().Flag);
    Assert.True(viewModel.CanApplyVoteRingPenalty(initial.Id));
  }

  [Fact]
  public async Task ConcurrentlyResolvedFlagIsRemovedFromPendingQueue()
  {
    var initial = ModerationIntegrityTestService.VoteFlag("flag-1");
    var resolved = initial with
    {
      Resolution = "dismissed",
      ResolvedAt = DateTimeOffset.UtcNow,
      ResolvedById = "admin-2",
    };
    var service = Service([initial], (_, _) => Task.FromResult(
        new VoteIntegrityFlagResponse(resolved)));
    var viewModel = await Loaded(service);

    await AmbiguousPenaltyAndReconcile(viewModel, initial.Id);

    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.NeedsReconciliation(initial.Id));
    Assert.False(viewModel.CanApplyVoteRingPenalty(initial.Id));
    Assert.Equal(1, service.VotePenalties);
  }

  [Fact]
  public async Task ExactGetReplacesResolvedFlagInPlaceWithoutReloadingCollection()
  {
    var before = ModerationIntegrityTestService.VoteFlag("before");
    var target = ModerationIntegrityTestService.VoteFlag("target");
    var after = ModerationIntegrityTestService.VoteFlag("after");
    var resolved = target with
    {
      Resolution = "suspended",
      ResolvedAt = DateTimeOffset.UtcNow,
    };
    var exactGets = 0;
    var service = Service([before, target, after], (flagId, _) =>
    {
      exactGets++;
      Assert.Equal(target.Id, flagId);
      return Task.FromResult(new VoteIntegrityFlagResponse(resolved));
    });
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.SelectStatusAsync(
        IntegrityFlagStatus.All, TestContext.Current.CancellationToken);

    await AmbiguousPenaltyAndReconcile(viewModel, target.Id);

    Assert.Equal(["before", "target", "after"],
        viewModel.Items.Select(row => row.Flag.Id));
    Assert.Same(resolved, viewModel.Items[1].Flag);
    Assert.Equal(1, exactGets);
    Assert.Single(service.VoteFetches);
  }

  [Fact]
  public async Task ExactGetFailureStaysSuppressedAndCanReconcileAgain()
  {
    var initial = ModerationIntegrityTestService.VoteFlag("flag-1");
    var exactGets = 0;
    var service = Service([initial], (_, _) => ++exactGets == 1
        ? Task.FromException<VoteIntegrityFlagResponse>(
            new HttpRequestException("flag unavailable"))
        : Task.FromResult(new VoteIntegrityFlagResponse(initial)));
    var viewModel = await Loaded(service);

    await viewModel.ApplyVoteRingPenaltyAsync(
        initial.Id, TestContext.Current.CancellationToken);
    await viewModel.ReconcileVotePenaltyAsync(initial.Id);

    Assert.Equal(1, service.VotePenalties);
    Assert.True(viewModel.NeedsReconciliation(initial.Id));
    Assert.False(viewModel.CanApplyVoteRingPenalty(initial.Id));
    await viewModel.ReconcileVotePenaltyAsync(initial.Id);
    Assert.Equal(2, exactGets);
    Assert.False(viewModel.NeedsReconciliation(initial.Id));
    Assert.True(viewModel.CanApplyVoteRingPenalty(initial.Id));
  }

  [Fact]
  public async Task NewPenaltyFetchesCommittedFlagWithoutPermittingRetry()
  {
    var initial = ModerationIntegrityTestService.VoteFlag("flag-1");
    var snapshots = 0;
    var exactGets = 0;
    var historical = Penalty("old", initial.Id);
    var committed = Penalty("new", initial.Id);
    var service = Service([initial], (_, _) =>
    {
      exactGets++;
      return Task.FromResult(new VoteIntegrityFlagResponse(initial with
      {
        Resolution = "penalized",
        ResolvedAt = DateTimeOffset.UtcNow,
      }));
    });
    service.FetchVotePenalties = (_, _, sourceFlagId, _) => Task.FromResult(
        ++snapshots == 1
            ? Page(sourceFlagId!, historical)
            : Page(sourceFlagId!, historical, committed));
    var viewModel = await Loaded(service);

    await AmbiguousPenaltyAndReconcile(viewModel, initial.Id);

    Assert.Equal(1, exactGets);
    Assert.False(viewModel.NeedsReconciliation(initial.Id));
    Assert.False(viewModel.CanApplyVoteRingPenalty(initial.Id));
    Assert.Empty(viewModel.Items);
  }

  private static ModerationIntegrityTestService Service(
      IReadOnlyList<VoteIntegrityFlag> flags,
      Func<string, CancellationToken, Task<VoteIntegrityFlagResponse>> fetchExact)
  {
    var historical = Penalty("old", flags[0].Id);
    return new()
    {
      FetchVotes = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.VotePage(flags)),
      PenalizeVotes = (_, _) => Task.FromException<VoteIntegrityPenaltyApplicationResponse>(
          new HttpRequestException("result uncertain")),
      FetchVotePenalties = (_, _, sourceFlagId, _) =>
          Task.FromResult(Page(sourceFlagId!, historical with { SourceFlagId = sourceFlagId! })),
      FetchVote = fetchExact,
    };
  }

  private static async Task<VoteIntegrityViewModel> Loaded(
      ModerationIntegrityTestService service)
  {
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static async Task AmbiguousPenaltyAndReconcile(
      VoteIntegrityViewModel viewModel,
      string flagId)
  {
    await viewModel.ApplyVoteRingPenaltyAsync(flagId, TestContext.Current.CancellationToken);
    await viewModel.ReconcileVotePenaltyAsync(flagId);
  }

  private static VoteWeightPenalty Penalty(string id, string flagId) => new(
      id, "user-1", 0.2, "voting_ring", flagId, "admin-1",
      DateTimeOffset.UtcNow, "admin-2", DateTimeOffset.UtcNow);

  private static VoteIntegrityPenaltiesResponse Page(
      string flagId,
      params VoteWeightPenalty[] penalties) => new(
      penalties, new PageInfo(null, false, penalties.LastOrDefault()?.Id),
      new VoteIntegrityPenaltyFilterScope("flag", flagId));
}
