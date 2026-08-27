using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Friends;
using Xunit;

namespace Voucha.Client.Core.Tests.FollowerDistributions;

public sealed class FollowerDistributionViewModelTests
{
  [Fact]
  public void SelectionLimitsToOneHundredAndResetClearsContext()
  {
    using var viewModel = new FollowerDistributionViewModel(new Friends(), Client(HttpStatusCode.OK), FollowerDistributionTargetKind.Post, "post-1");
    viewModel.SetAudience(true);
    foreach (var index in Enumerable.Range(0, 100)) Assert.True(viewModel.ToggleSelection($"user-{index}"));
    Assert.False(viewModel.ToggleSelection("one-too-many"));
    viewModel.Reset("post-2");
    Assert.Empty(viewModel.SelectedIds);
    Assert.False(viewModel.IsSelectedAudience);
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
  private static VouchaApiClient Client(RecordingHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

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
