using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Friends;

public sealed class FriendRecommendationsViewModelTests
{
  [Fact]
  public async Task PaginationDeduplicatesAndForwardsOpaqueCursor()
  {
    var service = new RecordingRecommendationService(
        Page(["user-1"], "opaque-cursor", hasMore: true),
        Page(["user-1", "user-2"], null, hasMore: false));
    var viewModel = new FriendRecommendationsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["user-1", "user-2"], viewModel.Items.Select(item => item.Id));
    Assert.Equal([null, "opaque-cursor"], service.Cursors);
    Assert.All(service.Limits, limit => Assert.Equal(25, limit));
    Assert.False(viewModel.HasMore);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task RemovingFinalVisibleRecommendationKeepsNextPageReachable(bool follow)
  {
    var service = new RecordingRecommendationService(
        Page(["user-1"], "opaque-cursor", hasMore: true),
        Page(["user-2"], null, hasMore: false));
    var viewModel = new FriendRecommendationsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Items);
    if (follow)
    {
      await viewModel.FollowAsync(row, TestContext.Current.CancellationToken);
    }
    else
    {
      await viewModel.DismissAsync(row, TestContext.Current.CancellationToken);
    }

    Assert.Empty(viewModel.Items);
    Assert.True(viewModel.HasMore);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["user-2"], viewModel.Items.Select(item => item.Id));
    Assert.Equal([null, "opaque-cursor"], service.Cursors);
  }

  [Fact]
  public async Task FollowAndDismissOptimisticallyRemoveAndRollbackAtClampedPosition()
  {
    var followCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var followService = new RecordingRecommendationService(
        Page(["user-1", "user-2"], null, hasMore: false),
        Page(["user-1", "user-2"], null, hasMore: false))
    {
      FollowCompletions = { ["user-1"] = followCompletion },
    };
    var followViewModel = new FriendRecommendationsViewModel(followService);
    await followViewModel.LoadAsync(TestContext.Current.CancellationToken);

    var followTask = followViewModel.FollowAsync(
        followViewModel.Items[0],
        TestContext.Current.CancellationToken);
    Assert.Equal(["user-2"], followViewModel.Items.Select(item => item.Id));
    followCompletion.SetResult();
    await followTask;
    await followViewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["user-2"], followViewModel.Items.Select(item => item.Id));
    Assert.Equal(["user-1"], followService.FollowCalls);

    var dismissCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var dismissService = new RecordingRecommendationService(
        Page(["user-1", "user-2", "user-3"], null, hasMore: false),
        Page(["user-1"], null, hasMore: false))
    {
      DismissCompletions = { ["user-3"] = dismissCompletion },
    };
    var dismissViewModel = new FriendRecommendationsViewModel(dismissService);
    await dismissViewModel.LoadAsync(TestContext.Current.CancellationToken);

    var dismissTask = dismissViewModel.DismissAsync(
        dismissViewModel.Items[2],
        TestContext.Current.CancellationToken);
    await dismissViewModel.ReloadAsync(TestContext.Current.CancellationToken);
    dismissCompletion.SetException(new InvalidOperationException("Dismiss failed."));
    await dismissTask;

    Assert.Equal(["user-1", "user-3"], dismissViewModel.Items.Select(item => item.Id));
    Assert.Equal("Dismiss failed.", dismissViewModel.ErrorMessage);
    Assert.Equal(["user-3"], dismissService.DismissCalls);
  }

  [Fact]
  public async Task MutationCompletionDoesNotEndAnActiveReloadOrEnableStalePagination()
  {
    var service = new RecordingRecommendationService(
        Page(["old-user"], "old-cursor", hasMore: true),
        Page(["stale-page-user"], null, hasMore: false));
    var viewModel = new FriendRecommendationsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var reloadCompletion = new TaskCompletionSource<FriendRecommendationsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var mutationCompletion = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchCompletion = reloadCompletion;
    service.FollowCompletions["old-user"] = mutationCompletion;

    var reload = viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    var follow = viewModel.FollowAsync(
        Assert.Single(viewModel.Items),
        TestContext.Current.CancellationToken);
    mutationCompletion.SetResult();
    await follow;

    Assert.Equal(LoadState.Loading, viewModel.State);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, null], service.Cursors);

    reloadCompletion.SetResult(Page(["fresh-user"], null, hasMore: false));
    await reload;

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(["fresh-user"], viewModel.Items.Select(item => item.Id));
    Assert.Equal([null, null], service.Cursors);
  }

  [Fact]
  public void DisplayNeverFallsBackToOpaqueUserId()
  {
    var hydrated = new FriendRecommendationRow(
        Recommendation("opaque-user-id", "Provider Friend"),
        new User("opaque-user-id", "hydrated", DisplayAccount: new PublicDisplayAccount("Hydrated Friend")));
    var providerOnly = new FriendRecommendationRow(
        Recommendation("second-opaque-id", "Provider Friend"),
        null);
    var fallback = new FriendRecommendationRow(
        Recommendation("third-opaque-id", ""),
        null);

    Assert.Equal("Hydrated Friend", hydrated.PrimaryLabel);
    Assert.Equal("via GitHub", hydrated.ProviderPresentation);
    Assert.Equal("Provider Friend", providerOnly.PrimaryLabel);
    Assert.Equal("Voucha member", fallback.PrimaryLabel);
    Assert.DoesNotContain("opaque", fallback.PrimaryLabel, StringComparison.Ordinal);
  }

  private static FriendRecommendationsResponse Page(
      IReadOnlyList<string> ids,
      string? cursor,
      bool hasMore) =>
      new(
          ids.Select(id => Recommendation(id, $"Provider {id}")).ToArray(),
          new PageInfo(cursor, hasMore, null),
          ids.ToDictionary(
              id => id,
              id => new User(id, $"username-{id}"),
              StringComparer.Ordinal));

  private static FriendRecommendation Recommendation(string id, string providerName) =>
      new("user", id, OAuthBrokerProvider.Github, providerName);

  private sealed class RecordingRecommendationService(
      params FriendRecommendationsResponse[] responses) : IFriendRecommendationsService
  {
    private readonly Queue<FriendRecommendationsResponse> responses = new(responses);

    public List<string?> Cursors { get; } = [];
    public List<int> Limits { get; } = [];
    public List<string> FollowCalls { get; } = [];
    public List<string> DismissCalls { get; } = [];
    public Dictionary<string, TaskCompletionSource> FollowCompletions { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, TaskCompletionSource> DismissCompletions { get; } =
        new(StringComparer.Ordinal);
    public TaskCompletionSource<FriendRecommendationsResponse>? FetchCompletion { get; set; }

    public Task<FriendRecommendationsResponse> FetchAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      Cursors.Add(after);
      Limits.Add(limit);
      if (FetchCompletion is { } completion)
      {
        FetchCompletion = null;
        return completion.Task;
      }
      return Task.FromResult(responses.Dequeue());
    }

    public Task FollowAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
      FollowCalls.Add(userId);
      return FollowCompletions.GetValueOrDefault(userId)?.Task ?? Task.CompletedTask;
    }

    public Task DismissAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
      DismissCalls.Add(userId);
      return DismissCompletions.GetValueOrDefault(userId)?.Task ?? Task.CompletedTask;
    }
  }
}
