using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class VoteIntegrityPenaltySnapshotPaginationTests
{
  private static readonly NavigationViewer Admin = new(true, ["administrator"]);

  [Fact]
  public async Task BaselineAggregatesPagesAndForwardsOpaqueCursor()
  {
    var flag = ModerationIntegrityTestService.VoteFlag("flag-1");
    var first = Penalty("old-1", flag.Id);
    var second = Penalty("old-2", flag.Id);
    var snapshot = 0;
    var cursors = new List<string?>();
    var service = Service(flag, (status, after, sourceFlagId, _) =>
    {
      Assert.Equal(IntegrityPenaltyStatus.All, status);
      Assert.Equal(flag.Id, sourceFlagId);
      cursors.Add(after);
      if (after is null) snapshot++;
      return Task.FromResult(snapshot switch
      {
        1 when after is null => Page(flag.Id, [first], "opaque:cursor/value", true),
        1 when after == "opaque:cursor/value" => Page(flag.Id, [second]),
        2 when after is null => Page(flag.Id, [first, second]),
        _ => throw new InvalidOperationException("Unexpected snapshot page."),
      });
    });
    var viewModel = await Loaded(service);

    await viewModel.ApplyVoteRingPenaltyAsync(flag.Id, TestContext.Current.CancellationToken);
    await viewModel.ReconcileVotePenaltyAsync(flag.Id);

    Assert.Equal([null, "opaque:cursor/value", null], cursors);
    Assert.Equal(1, service.VotePenalties);
    Assert.True(viewModel.CanApplyVoteRingPenalty(flag.Id));
  }

  [Fact]
  public async Task ContinuationScopeMismatchFailsClosedBeforePost()
  {
    var flag = ModerationIntegrityTestService.VoteFlag("flag-1");
    var cursors = new List<string?>();
    var service = Service(flag, (_, after, _, _) =>
    {
      cursors.Add(after);
      return Task.FromResult(after is null
          ? Page(flag.Id, [Penalty("old-1", flag.Id)], "next", true)
          : Page("different-flag", [Penalty("old-2", flag.Id)]));
    });
    var viewModel = await Loaded(service);

    await viewModel.ApplyVoteRingPenaltyAsync(flag.Id, TestContext.Current.CancellationToken);

    Assert.Equal([null, "next"], cursors);
    Assert.Equal(0, service.VotePenalties);
    Assert.NotNull(viewModel.ActionError(flag.Id));
  }

  [Theory]
  [InlineData(CursorFailure.Missing)]
  [InlineData(CursorFailure.Repeated)]
  public async Task InvalidContinuationCursorFailsClosedBeforePost(CursorFailure failure)
  {
    var flag = ModerationIntegrityTestService.VoteFlag("flag-1");
    var cursors = new List<string?>();
    var service = Service(flag, (_, after, _, _) =>
    {
      cursors.Add(after);
      return Task.FromResult(failure switch
      {
        CursorFailure.Missing => Page(flag.Id, [], null, true),
        CursorFailure.Repeated when after is null =>
            Page(flag.Id, [Penalty("old-1", flag.Id)], "repeat", true),
        CursorFailure.Repeated =>
            Page(flag.Id, [Penalty("old-2", flag.Id)], "repeat", true),
        _ => throw new InvalidOperationException("Unexpected cursor failure."),
      });
    });
    var viewModel = await Loaded(service);

    await viewModel.ApplyVoteRingPenaltyAsync(flag.Id, TestContext.Current.CancellationToken);

    Assert.Equal(
        failure == CursorFailure.Missing ? [null] : [null, "repeat"],
        cursors);
    Assert.Equal(0, service.VotePenalties);
    Assert.NotNull(viewModel.ActionError(flag.Id));
  }

  public enum CursorFailure { Missing, Repeated }

  private static ModerationIntegrityTestService Service(
      VoteIntegrityFlag flag,
      Func<IntegrityPenaltyStatus, string?, string?, CancellationToken,
          Task<VoteIntegrityPenaltiesResponse>> fetch) => new()
          {
            FetchVotes = (_, _, _) => Task.FromResult(
                ModerationIntegrityTestService.VotePage([flag])),
            PenalizeVotes = (_, _) => Task.FromException<VoteIntegrityPenaltyApplicationResponse>(
                new HttpRequestException("result uncertain")),
            FetchVotePenalties = fetch,
            FetchVote = (_, _) => Task.FromResult(new VoteIntegrityFlagResponse(flag)),
          };

  private static async Task<VoteIntegrityViewModel> Loaded(
      ModerationIntegrityTestService service)
  {
    var viewModel = new VoteIntegrityViewModel(service, Admin);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static VoteWeightPenalty Penalty(string id, string flagId) => new(
      id, "user-1", 0.2, "voting_ring", flagId, "admin-1",
      DateTimeOffset.UtcNow, "admin-2", DateTimeOffset.UtcNow);

  private static VoteIntegrityPenaltiesResponse Page(
      string flagId,
      IReadOnlyList<VoteWeightPenalty> penalties,
      string? endCursor = null,
      bool hasNextPage = false) => new(
      penalties, new PageInfo(endCursor, hasNextPage, penalties.FirstOrDefault()?.Id),
      new VoteIntegrityPenaltyFilterScope("flag", flagId));
}
