using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostsListViewModelRelationTests
{
  [Fact]
  public async Task LoadFeedAsyncMapsSavedAndHiddenBookmarkFlags()
  {
    var service = new RecordingPostsService
    {
      FeedResponse = CreateResponse(
          new Post("post-1", "review", "Relation post", "Body", "user-1"),
          new BookmarkPredicates(Save: true, Hide: true)),
    };
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Items);
    Assert.True(row.IsSaved);
    Assert.True(row.IsHidden);
    Assert.Equal("Unsave", row.SaveActionLabel);
    Assert.Equal("Unhide", row.HideActionLabel);
  }

  [Fact]
  public async Task ToggleSaveAsyncOptimisticallyUpdatesAndRollsBackOnFailure()
  {
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingPostsService
    {
      FeedResponse = CreateResponse(new Post("post-1", "review", "Relation post", "Body", "user-1")),
      BookmarkCompletion = completion,
    };
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    var mutation = viewModel.ToggleSaveAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.True(viewModel.Items[0].IsSaved);
    Assert.False(viewModel.Items[0].IsHidden);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(("post-1", "save", true), Assert.Single(service.BookmarkCalls));

    completion.SetException(new InvalidOperationException("Save failed."));
    await mutation;

    Assert.False(viewModel.Items[0].IsSaved);
    Assert.False(viewModel.Items[0].IsHidden);
    Assert.Equal("Save failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ToggleHideAsyncOptimisticallyUpdatesAndRollsBackOnCancellation()
  {
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingPostsService
    {
      FeedResponse = CreateResponse(new Post("post-1", "review", "Relation post", "Body", "user-1")),
      BookmarkCompletion = completion,
    };
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    var mutation = viewModel.ToggleHideAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    Assert.False(viewModel.Items[0].IsSaved);
    Assert.True(viewModel.Items[0].IsHidden);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(("post-1", "hide", true), Assert.Single(service.BookmarkCalls));

    completion.SetCanceled(TestContext.Current.CancellationToken);
    await mutation;

    Assert.False(viewModel.Items[0].IsSaved);
    Assert.False(viewModel.Items[0].IsHidden);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ApiPostsServiceSetPostBookmarkAsyncUsesSaveAndHideEndpoints()
  {
    var handler = new RecordingHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiPostsService(client);

    await service.SetPostBookmarkAsync("post-1", "save", true, TestContext.Current.CancellationToken);
    await service.SetPostBookmarkAsync("post-1", "hide", false, TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Put, request.Method);
          Assert.Equal("/api/v1/bookmarks/post/post-1/save", request.PathAndQuery);
        },
        request =>
        {
          Assert.Equal(HttpMethod.Delete, request.Method);
          Assert.Equal("/api/v1/bookmarks/post/post-1/hide", request.PathAndQuery);
        });
  }

  private static PostsFeedResponse CreateResponse(
      Post post,
      BookmarkPredicates? bookmarks = null) =>
      new(
          [new EntityReference("post", post.Id, post.Id, null, null, null, null, null, null, null, null)],
          new PageInfo(null, false, null),
          new Dictionary<string, Post>(StringComparer.Ordinal) { [post.Id] = post },
          new Dictionary<string, User>(StringComparer.Ordinal),
          new Dictionary<string, Community>(StringComparer.Ordinal),
          Bookmarks: bookmarks is null
              ? null
              : new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal) { [post.Id] = bookmarks });

  private sealed class RecordingPostsService : IPostsService
  {
    public PostsFeedResponse? FeedResponse { get; init; }

    public TaskCompletionSource? BookmarkCompletion { get; init; }

    public List<(string PostId, string Predicate, bool Enabled)> BookmarkCalls { get; } = [];

    public Task<PostsFeedResponse> FetchFeedAsync(
        FetchPostsFeedRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(FeedResponse ?? throw new InvalidOperationException("Missing response."));

    public Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostResponse> FetchPostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreatePostAsync(CreatePostBody body, string idempotencyKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UpdatePostAsync(
        string postIdOrSlug,
        UpdatePostBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VotePostAsync(string postId, int score, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SetPostBookmarkAsync(
        string postId,
        string predicate,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
      BookmarkCalls.Add((postId, predicate, enabled));
      return BookmarkCompletion?.Task ?? Task.CompletedTask;
    }

    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class RecordingHandler : HttpMessageHandler
  {
    public List<RecordedRequest> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Requests.Add(new RecordedRequest(request.Method, request.RequestUri?.PathAndQuery));
      return Task.FromResult(
          new HttpResponseMessage(HttpStatusCode.NoContent)
          {
            RequestMessage = request,
          });
    }
  }

  private sealed record RecordedRequest(HttpMethod Method, string? PathAndQuery);
}
