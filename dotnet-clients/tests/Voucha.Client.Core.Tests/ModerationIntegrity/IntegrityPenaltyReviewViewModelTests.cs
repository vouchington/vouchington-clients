using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Xunit;
using static Voucha.Client.Core.Tests.ModerationIntegrity.PenaltyReviewFixtures;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class IntegrityPenaltyReviewViewModelTests
{
  private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

  private static IIntegrityPenaltyReviewModel Make(
      bool vote, PenaltyReviewTestService reviews, ModerationIntegrityTestService? service = null,
      NavigationViewer? viewer = null) =>
      vote
          ? new VoteIntegrityPenaltyReviewViewModel(
              service ?? new ModerationIntegrityTestService(), reviews, viewer ?? Admin)
          : new ReportIntegrityPenaltyReviewViewModel(
              service ?? new ModerationIntegrityTestService(), reviews, viewer ?? Admin);

  private static void QueueDue(PenaltyReviewTestService reviews, bool vote, string suffix = "")
  {
    if (vote) reviews.VoteDue.Enqueue(() => Task.FromResult(VoteDue(suffix)));
    else reviews.ReportDue.Enqueue(() => Task.FromResult(ReportDue(suffix)));
  }

  private static void QueueReview(
      PenaltyReviewTestService reviews, bool vote, Exception? failure = null)
  {
    if (vote)
      reviews.VoteReviews.Enqueue(() => failure is null
          ? Task.FromResult(VoteReviewed()) : Task.FromException<VoteWeightPenaltyResponse>(failure));
    else
      reviews.ReportReviews.Enqueue(() => failure is null
          ? Task.FromResult(ReportReviewed()) : Task.FromException<ReportAbusePenaltyResponse>(failure));
  }

  private static ModerationIntegrityTestService Exact(bool vote, Func<Task<DateTimeOffset?>> reviewDue)
  {
    var service = new ModerationIntegrityTestService();
    service.FetchReportPenalty = async (_, _) => new(
        ReportReviewed().Penalty with { ReviewDueAt = await reviewDue(), ReviewedAt = null });
    service.FetchVotePenalty = async (_, _) => new(
        VoteReviewed().Penalty with { ReviewDueAt = await reviewDue(), ReviewedAt = null });
    return service;
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task QueueLoadsBoundedPageContinuesWithCursorAndRefreshesFromStart(bool vote)
  {
    var reviews = new PenaltyReviewTestService();
    QueueDue(reviews, vote, ".page-1");
    QueueDue(reviews, vote, ".page-2");
    QueueDue(reviews, vote, ".page-1");
    var queue = Make(vote, reviews);

    await queue.LoadAsync(Token);
    Assert.Single(queue.Rows);
    Assert.True(queue.HasMore);
    Assert.NotNull(queue.Rows[0].ReviewDueAt);
    await queue.LoadMoreAsync(Token);

    Assert.Equal(2, queue.Rows.Count);
    Assert.False(queue.HasMore);
    Assert.NotNull(reviews.DueCursors[1]);
    await queue.LoadMoreAsync(Token);
    Assert.Equal(2, reviews.DueCursors.Count);
    await queue.RefreshAsync(Token);
    Assert.Single(queue.Rows);
    Assert.Null(reviews.DueCursors[2]);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EmptyAndLoadFailureStatesRetryByRefresh(bool vote)
  {
    var reviews = new PenaltyReviewTestService();
    var failure = Http(HttpStatusCode.BadGateway);
    if (vote)
    {
      reviews.VoteDue.Enqueue(() => Task.FromException<VoteIntegrityPenaltiesResponse>(failure));
      reviews.VoteDue.Enqueue(() => Task.FromResult(new VoteIntegrityPenaltiesResponse(
          [], new PageInfo(null, false, null), null)));
    }
    else
    {
      reviews.ReportDue.Enqueue(() => Task.FromException<ReportIntegrityPenaltiesResponse>(failure));
      reviews.ReportDue.Enqueue(() => Task.FromResult(new ReportIntegrityPenaltiesResponse(
          [], new PageInfo(null, false, null))));
    }
    var queue = Make(vote, reviews);

    await queue.LoadAsync(Token);
    Assert.Equal(LoadState.Error, queue.State);
    Assert.Equal("Could not load reviews due. Refresh to try again.", queue.ErrorMessage);
    await queue.RefreshAsync(Token);
    Assert.True(queue.IsEmpty);
    Assert.Null(queue.ErrorMessage);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ConfirmAdoptsServerReviewDateAndCannotAdvanceTwice(bool vote)
  {
    var reviews = new PenaltyReviewTestService();
    QueueDue(reviews, vote);
    QueueReview(reviews, vote);
    var queue = Make(vote, reviews);
    await queue.LoadAsync(Token);
    var id = queue.Rows[0].Id;
    var due = queue.Rows[0].ReviewDueAt;

    await queue.ConfirmAsync(id, Token);
    await queue.ConfirmAsync(id, Token);

    Assert.Single(reviews.ReviewCalls);
    Assert.NotEqual(due, queue.Rows[0].ReviewDueAt);
    Assert.NotNull(queue.Rows[0].ReviewedAt);
    Assert.False(queue.CanConfirm(id));
    Assert.True(queue.CanRevoke(id));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AmbiguousConfirmReconcilesCommittedReviewWithoutResubmitting(bool vote)
  {
    var reviews = new PenaltyReviewTestService();
    QueueDue(reviews, vote);
    QueueReview(reviews, vote, Http(HttpStatusCode.ServiceUnavailable));
    var service = new ModerationIntegrityTestService
    {
      FetchReportPenalty = (_, _) => Task.FromResult(ReportReviewed()),
      FetchVotePenalty = (_, _) => Task.FromResult(VoteReviewed()),
    };
    var queue = Make(vote, reviews, service);
    await queue.LoadAsync(Token);
    var id = queue.Rows[0].Id;

    await queue.ConfirmAsync(id, Token);

    Assert.Single(reviews.ReviewCalls);
    Assert.False(queue.NeedsReconciliation(id));
    Assert.Null(queue.ActionError(id));
    Assert.False(queue.CanConfirm(id));
    Assert.NotNull(queue.Rows[0].ReviewedAt);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task UnreconciledAmbiguousConfirmBlocksResubmissionUntilServerShowsUnchangedPenalty(bool vote)
  {
    var reviews = new PenaltyReviewTestService();
    QueueDue(reviews, vote);
    QueueReview(reviews, vote, new HttpRequestException("timeout"));
    QueueReview(reviews, vote);
    var calls = 0;
    var unchanged = vote ? VoteDue().Results[0].ReviewDueAt : ReportDue().Results[0].ReviewDueAt;
    var service = Exact(vote, () => ++calls == 1
        ? Task.FromException<DateTimeOffset?>(new HttpRequestException("still down"))
        : Task.FromResult(unchanged));
    var queue = Make(vote, reviews, service);
    await queue.LoadAsync(Token);
    var id = queue.Rows[0].Id;

    await queue.ConfirmAsync(id, Token);
    Assert.True(queue.NeedsReconciliation(id));
    Assert.False(queue.CanConfirm(id));
    Assert.NotNull(queue.ActionError(id));
    await queue.ConfirmAsync(id, Token);
    Assert.Single(reviews.ReviewCalls);

    await queue.ReconcileAsync(id);
    Assert.False(queue.NeedsReconciliation(id));
    Assert.True(queue.CanConfirm(id));
    await queue.ConfirmAsync(id, Token);
    Assert.Equal(2, reviews.ReviewCalls.Count);
    Assert.False(queue.CanConfirm(id));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RevokeRemovesPenaltyFromDueQueueAndReconcilesAmbiguousRevocation(bool vote)
  {
    var reviews = new PenaltyReviewTestService();
    QueueDue(reviews, vote);
    QueueDue(reviews, vote);
    var revokedAt = DateTimeOffset.Parse("2026-10-24T12:00:00Z");
    var calls = 0;
    var service = new ModerationIntegrityTestService
    {
      RevokeReportPenalty = (_, _) => ++calls == 1
          ? Task.FromResult(new ReportAbusePenaltyResponse(ReportReviewed().Penalty with { RevokedAt = revokedAt }))
          : Task.FromException<ReportAbusePenaltyResponse>(Http(HttpStatusCode.InternalServerError)),
      RevokeVotePenalty = (_, _) => ++calls == 1
          ? Task.FromResult(new VoteWeightPenaltyResponse(VoteReviewed().Penalty with { RevokedAt = revokedAt }))
          : Task.FromException<VoteWeightPenaltyResponse>(Http(HttpStatusCode.InternalServerError)),
      FetchReportPenalty = (_, _) => Task.FromResult(
          new ReportAbusePenaltyResponse(ReportReviewed().Penalty with { RevokedAt = revokedAt })),
      FetchVotePenalty = (_, _) => Task.FromResult(
          new VoteWeightPenaltyResponse(VoteReviewed().Penalty with { RevokedAt = revokedAt })),
    };
    var queue = Make(vote, reviews, service);
    await queue.LoadAsync(Token);
    var id = queue.Rows[0].Id;

    await queue.RevokeAsync(id, Token);
    Assert.Empty(queue.Rows);

    await queue.RefreshAsync(Token);
    await queue.RevokeAsync(id, Token);
    Assert.Empty(queue.Rows);
    Assert.False(queue.NeedsReconciliation(id));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AuthorizationLossRemovesActionsAndPlainFailuresKeepThem(bool vote)
  {
    var reviews = new PenaltyReviewTestService();
    QueueDue(reviews, vote);
    QueueReview(reviews, vote, Http(HttpStatusCode.Conflict));
    QueueReview(reviews, vote, Http(HttpStatusCode.Forbidden));
    var queue = Make(vote, reviews);
    await queue.LoadAsync(Token);
    var id = queue.Rows[0].Id;

    await queue.ConfirmAsync(id, Token);
    Assert.NotNull(queue.ActionError(id));
    Assert.True(queue.CanConfirm(id));
    Assert.False(queue.NeedsReconciliation(id));

    await queue.ConfirmAsync(id, Token);
    Assert.False(queue.IsAuthorized);
    Assert.False(queue.CanConfirm(id));
    Assert.False(queue.CanRevoke(id));
    Assert.Contains("administrators", queue.ActionError(id), StringComparison.Ordinal);
  }

  [Fact]
  public async Task ReportAndVoteGatesAreIndependentAndNonAdministratorsNeverFetch()
  {
    var reviews = new PenaltyReviewTestService();
    var reportOnly = new IntegrityCapabilities(true, true, true, true, true, true, false);
    var service = new ModerationIntegrityTestService();
    var report = new ReportIntegrityPenaltyReviewViewModel(
        service, reviews, Admin, capabilities: reportOnly);
    var vote = new VoteIntegrityPenaltyReviewViewModel(
        service, reviews, Admin, capabilities: reportOnly);
    QueueDue(reviews, false);

    await report.LoadAsync(Token);
    await vote.LoadAsync(Token);

    Assert.True(report.IsAuthorized);
    Assert.False(vote.IsAuthorized);
    Assert.Equal(LoadState.Error, vote.State);
    foreach (var viewer in new NavigationViewer[] { new(false, []), new(true, ["moderator"]) })
    {
      var denied = Make(false, reviews, service, viewer);
      await denied.LoadAsync(Token);
      await denied.RefreshAsync(Token);
      Assert.False(denied.IsAuthorized);
      Assert.Contains("administrators", denied.ErrorMessage, StringComparison.Ordinal);
    }
    Assert.Single(reviews.DueCursors);
  }

  [Fact]
  public async Task ContinuationFailureKeepsRowsAndLocaleChangeNotifiesWithoutFetching()
  {
    var reviews = new PenaltyReviewTestService();
    QueueDue(reviews, false, ".page-1");
    reviews.ReportDue.Enqueue(() => Task.FromException<ReportIntegrityPenaltiesResponse>(
        Http(HttpStatusCode.BadGateway)));
    using var viewModel = new ReportIntegrityPenaltyReviewViewModel(
        new ModerationIntegrityTestService(), reviews, Admin);
    var changed = new List<string?>();
    viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
    await viewModel.LoadAsync(Token);

    await viewModel.LoadMoreAsync(Token);

    Assert.Single(viewModel.Rows);
    Assert.NotNull(viewModel.ErrorMessage);
    Assert.True(viewModel.CanLoadMore);
    changed.Clear();
    viewModel.OnUiLocaleChanged();
    Assert.Contains(nameof(viewModel.ErrorMessage), changed);
  }
}
