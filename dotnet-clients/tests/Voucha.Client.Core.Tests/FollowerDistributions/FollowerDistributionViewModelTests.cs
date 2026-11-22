using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Friends;
using Xunit;

namespace Voucha.Client.Core.Tests.FollowerDistributions;

public sealed class FollowerDistributionViewModelTests
{
  [Fact]
  public async Task SelectionCapsAtTheFetchedRecipientLimitAndResetClearsContext()
  {
    var handler = new RoutingHandler(new()
    {
      ["/api/v1/content-limits"] = (HttpStatusCode.OK, ContentLimits(recipientsMax: 3))
    });
    using var viewModel = new FollowerDistributionViewModel(new Friends(), Client(handler), FollowerDistributionTargetKind.Post, "post-1");
    viewModel.SetAudience(true);
    await viewModel.LoadRecipientLimitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3, viewModel.RecipientLimit);

    foreach (var index in Enumerable.Range(0, 3)) Assert.True(viewModel.ToggleSelection($"user-{index}"));
    Assert.False(viewModel.CanSelectMore);
    Assert.False(viewModel.ToggleSelection("one-too-many"));
    Assert.True(viewModel.ToggleSelection("user-0"));
    Assert.True(viewModel.ToggleSelection("one-more"));

    viewModel.Reset("post-2");
    Assert.Empty(viewModel.SelectedIds);
    Assert.False(viewModel.IsSelectedAudience);
    Assert.Null(viewModel.RecipientLimit);
  }

  [Fact]
  public async Task SelectionIsUncappedWhenTheLimitFetchFailsAndTheServerRejectionSurfaces()
  {
    var handler = new RoutingHandler(new()
    {
      ["/api/v1/content-limits"] = (HttpStatusCode.InternalServerError, "{}"),
      ["/api/v1/posts/post-1/sends"] = (HttpStatusCode.BadRequest, """{"message":"recipient_user_ids can include at most 100 followers"}""")
    });
    using var viewModel = new FollowerDistributionViewModel(new Friends(), Client(handler), FollowerDistributionTargetKind.Post, "post-1");
    viewModel.SetAudience(true);
    await viewModel.LoadRecipientLimitAsync(TestContext.Current.CancellationToken);
    Assert.Null(viewModel.RecipientLimit);

    foreach (var index in Enumerable.Range(0, 101)) Assert.True(viewModel.ToggleSelection($"01900000-0000-7000-8000-{index:D12}"));
    Assert.True(viewModel.CanSelectMore);

    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));
    var rejection = Assert.IsType<VouchaApiException>(viewModel.Error);
    Assert.Equal(HttpStatusCode.BadRequest, rejection.StatusCode);
    Assert.Equal(101, viewModel.SelectedIds.Count);
  }

  [Fact]
  public async Task SubmitStaysDisabledWhileAnEarlierSelectionExceedsTheFetchedLimit()
  {
    var handler = new RoutingHandler(new()
    {
      ["/api/v1/content-limits"] = (HttpStatusCode.OK, ContentLimits(recipientsMax: 3))
    });
    using var viewModel = new FollowerDistributionViewModel(new Friends(), Client(handler), FollowerDistributionTargetKind.Post, "post-1");
    viewModel.SetAudience(true);
    foreach (var index in Enumerable.Range(0, 5)) Assert.True(viewModel.ToggleSelection($"user-{index}"));
    Assert.True(viewModel.CanSubmit);

    await viewModel.LoadRecipientLimitAsync(TestContext.Current.CancellationToken);

    Assert.Equal(5, viewModel.SelectedIds.Count);
    Assert.False(viewModel.CanSubmit);
    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));
    Assert.True(viewModel.ToggleSelection("user-0"));
    Assert.True(viewModel.ToggleSelection("user-1"));
    Assert.True(viewModel.CanSubmit);
  }

  [Fact]
  public async Task LimitResponseFromBeforeAResetIsIgnored()
  {
    var handler = new GatedHandler(ContentLimits(recipientsMax: 1));
    using var viewModel = new FollowerDistributionViewModel(new Friends(), Client(handler), FollowerDistributionTargetKind.Post, "post-1");
    var staleLoad = viewModel.LoadRecipientLimitAsync(TestContext.Current.CancellationToken);
    await handler.Requested.Task.WaitAsync(TestContext.Current.CancellationToken);

    viewModel.Reset("post-2");
    handler.Release.SetResult();
    await staleLoad;

    Assert.Null(viewModel.RecipientLimit);
  }

  [Fact]
  public async Task SubmitSuppressesDuplicatesAndPreservesSelectionOnFailure()
  {
    var handler = new RecordingHandler(HttpStatusCode.InternalServerError);
    using var viewModel = new FollowerDistributionViewModel(new Friends(), Client(handler), FollowerDistributionTargetKind.Post, "post-1");
    viewModel.SetAudience(true);
    const string recipientId = "01900000-0000-7000-8000-000000000001";
    viewModel.ToggleSelection(recipientId);
    Assert.False(await viewModel.SubmitAsync(TestContext.Current.CancellationToken));
    Assert.Contains(recipientId, viewModel.SelectedIds);
    Assert.Equal(1, handler.RequestCount);
  }

  [Fact]
  public async Task SearchForwardsQueryAndCursorWithoutLocalFiltering()
  {
    var friends = new Friends(new UserFollowersResponse([new User("user-1", "server result")], new PageInfo("cursor-2", true, null)));
    using var viewModel = new FollowerDistributionViewModel(friends, Client(HttpStatusCode.OK), FollowerDistributionTargetKind.Post, "post-1");
    await viewModel.SearchFollowersAsync("owner", "needle", TestContext.Current.CancellationToken);
    await viewModel.LoadMoreFollowersAsync(TestContext.Current.CancellationToken);
    Assert.Equal([("owner", "needle", (string?)null), ("owner", "needle", "cursor-2")], friends.Requests);
  }

  [Fact]
  public async Task EmptySearchLoadsTheCurrentFollowersPage()
  {
    var friends = new Friends();
    using var viewModel = new FollowerDistributionViewModel(friends, Client(HttpStatusCode.OK), FollowerDistributionTargetKind.Post, "post-1");
    await viewModel.SearchFollowersAsync("owner", "  ", TestContext.Current.CancellationToken);
    Assert.Equal([("owner", (string?)null, (string?)null)], friends.Requests);
  }

  [Fact]
  public async Task NewSearchCancelsTheDebounceBeforeItCanFetch()
  {
    var friends = new Friends();
    using var viewModel = new FollowerDistributionViewModel(friends, Client(HttpStatusCode.OK), FollowerDistributionTargetKind.Post, "post-1");
    var first = viewModel.SearchFollowersAsync("owner", "first", TestContext.Current.CancellationToken);
    await viewModel.SearchFollowersAsync("owner", "second", TestContext.Current.CancellationToken);
    await first;
    Assert.Equal([("owner", "second", (string?)null)], friends.Requests);
  }

  [Fact]
  public async Task FailedSearchThenSuccessfulRetryClearsError()
  {
    var friends = new RetryingFriends(
        Task.FromException<UserFollowersResponse>(new HttpRequestException("offline")),
        Task.FromResult(new UserFollowersResponse([], new PageInfo(null, false, null))));
    using var viewModel = new FollowerDistributionViewModel(friends, Client(HttpStatusCode.OK), FollowerDistributionTargetKind.Post, "post-1");

    await viewModel.SearchFollowersAsync("owner", "needle", TestContext.Current.CancellationToken);
    Assert.IsType<HttpRequestException>(viewModel.Error);

    var retry = viewModel.SearchFollowersAsync("owner", "needle", TestContext.Current.CancellationToken);
    Assert.Null(viewModel.Error);
    await retry;

    Assert.Null(viewModel.Error);
  }

  [Fact]
  public async Task SearchReplacementPreservesSelectedRecipientsForReviewAndRemoval()
  {
    var retained = new User("user-1", "Retained recipient");
    var friends = new RetryingFriends(
        Task.FromResult(new UserFollowersResponse([retained], new PageInfo(null, false, null))),
        Task.FromResult(new UserFollowersResponse([new User("user-2", "Replacement result")], new PageInfo(null, false, null))));
    using var viewModel = new FollowerDistributionViewModel(friends, Client(HttpStatusCode.OK), FollowerDistributionTargetKind.Post, "post-1");
    viewModel.SetAudience(true);

    await viewModel.SearchFollowersAsync("owner", "retained", TestContext.Current.CancellationToken);
    Assert.True(viewModel.ToggleSelection(retained));
    await viewModel.SearchFollowersAsync("owner", "replacement", TestContext.Current.CancellationToken);

    Assert.Equal([retained], viewModel.SelectedRecipients);
    Assert.True(viewModel.ToggleSelection(retained.Id));
    Assert.Empty(viewModel.SelectedRecipients);
  }

  [Theory]
  [InlineData("comment", null, "public", "everyone", false)]
  [InlineData("discussion", null, "public", "everyone", true)]
  [InlineData("discussion", null, "public", "users", true)]
  [InlineData("discussion", "parent", "public", "everyone", false)]
  public void EligibilityExcludesComments(string postType, string? parentId, string privacy, string broadcast, bool expected) =>
      Assert.Equal(expected, FollowerDistributionEligibility.IsPublicTopLevelPost(postType, parentId, privacy, broadcast));

  private static VouchaApiClient Client(HttpStatusCode status) => Client(new RecordingHandler(status));
  private static VouchaApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private sealed class Friends(UserFollowersResponse? response = null) : IFriendsService
  {
    public List<(string UserId, string? Query, string? After)> Requests { get; } = [];
    public Task<UserFollowersResponse> FetchFollowersAsync(string userId, string? query = null, string? after = null, CancellationToken cancellationToken = default)
    {
      Requests.Add((userId, query, after));
      return Task.FromResult(response ?? new UserFollowersResponse([], new PageInfo(null, false, null)));
    }
    public Task<UserFollowingResponse> FetchFollowingAsync(string userId, string? after = null, CancellationToken cancellationToken = default) => Task.FromResult(new UserFollowingResponse([], new PageInfo(null, false, null)));
    public Task SetFollowAsync(string userId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class RetryingFriends(params Task<UserFollowersResponse>[] responses) : IFriendsService
  {
    private readonly Queue<Task<UserFollowersResponse>> responses = new(responses);
    public Task<UserFollowersResponse> FetchFollowersAsync(string userId, string? query = null, string? after = null, CancellationToken cancellationToken = default) =>
        responses.Dequeue();
    public Task<UserFollowingResponse> FetchFollowingAsync(string userId, string? after = null, CancellationToken cancellationToken = default) => Task.FromResult(new UserFollowingResponse([], new PageInfo(null, false, null)));
    public Task SetFollowAsync(string userId, bool following, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private static string ContentLimits(int recipientsMax) =>
      $$"""{"post_images_max":20,"copyright_claimant_targets_max":20,"follower_distribution_recipients_max":{{recipientsMax}}}""";

  private sealed class RoutingHandler(Dictionary<string, (HttpStatusCode Status, string Body)> routes) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var (status, body) = routes[request.RequestUri!.AbsolutePath];
      return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
  }

  private sealed class GatedHandler(string body) : HttpMessageHandler
  {
    public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requested.TrySetResult();
      await Release.Task.WaitAsync(cancellationToken);
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
  }

  private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
  {
    public int RequestCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      RequestCount++;
      return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
    }
  }
}
