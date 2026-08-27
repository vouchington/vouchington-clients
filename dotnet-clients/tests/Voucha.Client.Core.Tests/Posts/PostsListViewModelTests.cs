using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Voting;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostsListViewModelTests
{
  [Fact]
  public async Task LoadFeedAsyncBuildsRowsFromSidecarPosts()
  {
    var service = new RecordingPostsService
    {
      FeedResponse = MakeResponse(new Post("post-1", "review", "Card review", "Body", "user-1")),
    };
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync("follow_users", "review", TestContext.Current.CancellationToken);

    Assert.Equal("follow_users", service.LastFeedRequest?.Feed);
    Assert.Equal("review", service.LastFeedRequest?.PostTypes);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Collection(viewModel.Items, item =>
    {
      Assert.Equal("post-1", item.Id);
      Assert.Equal("Card review", item.Title);
      Assert.Equal("Review by user-1", item.Subtitle);
    });
  }

  [Fact]
  public async Task LoadBrowseAsyncExposesServiceErrors()
  {
    var viewModel = new PostsListViewModel(new ThrowingPostsService());

    await viewModel.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("Posts unavailable.", viewModel.ErrorMessage);
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task LoadBrowseAsyncTreatsCancellationAsIdle()
  {
    var viewModel = new PostsListViewModel(new CancelingPostsService());

    await viewModel.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Idle, viewModel.State);
  }

  [Fact]
  public async Task LoadMoreAsyncForwardsCursorAndDeduplicatesRows()
  {
    var service = new SequencePostsService(
        MakeResponse(
            new Post("post-1", "review", "First", "Body", "user-1"),
            pageInfo: new PageInfo("next", true, null)),
        MakeResponse(new Post("post-2", "review", "Second", "Body", "user-1")));
    var viewModel = new PostsListViewModel(service);

    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new string?[] { null, "next" }, service.Requests.Select(request => request.After));
    Assert.Equal(new[] { "post-1", "post-2" }, viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task VotePostAsyncKeepsTheSameChoiceUntilAnExplicitClear()
  {
    var service = new RecordingPostsService
    {
      FeedResponse = MakeResponse(
          new Post("post-1", "review", "Card review", "Body", "user-1"),
          voteScoreNet: 2.25,
          voteCountUp: 3,
          voteCountDown: 1),
    };
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VotePostAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(4, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Single(service.VoteCalls);
    Assert.Equal(("post-1", ElectionVoteChoice.Like), service.VoteCalls[0]);

    await viewModel.VotePostAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(4, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal(2, service.VoteCalls.Count);
    Assert.Equal(("post-1", ElectionVoteChoice.Like), service.VoteCalls[0]);
    Assert.Equal(("post-1", ElectionVoteChoice.Like), service.VoteCalls[1]);

    await viewModel.VotePostAsync(viewModel.Items[0], null, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.Equal(["post-1"], service.ClearVoteCalls);
  }

  [Fact]
  public async Task VotePostAsyncRollsBackFailedRowAfterLaterOptimisticUpdate()
  {
    var delayedVote = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingPostsService
    {
      FeedResponse = new PostsFeedResponse(
          [
            new EntityReference("post", "post-1", "post-1", null, null, null, null, null, null, null, null),
            new EntityReference("post", "post-2", "post-2", null, null, null, null, null, null, null, null),
          ],
          new PageInfo(null, false, null),
          new Dictionary<string, Post>(StringComparer.Ordinal)
          {
            ["post-1"] = new Post("post-1", "review", "First", "Body", "user-1"),
            ["post-2"] = new Post("post-2", "review", "Second", "Body", "user-1"),
          },
          new Dictionary<string, User>(StringComparer.Ordinal),
          new Dictionary<string, Community>(StringComparer.Ordinal),
          new Dictionary<string, PostElection>(StringComparer.Ordinal)
          {
            ["post-1"] = new PostElection("post_election", "post-1", 2.25, 3, 1),
            ["post-2"] = new PostElection("post_election", "post-2", 2.25, 3, 1),
          }),
      VoteCompletions = new Dictionary<string, TaskCompletionSource>(StringComparer.Ordinal)
      {
        ["post-1"] = delayedVote,
      },
    };
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    var firstVote = viewModel.VotePostAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await viewModel.VotePostAsync(viewModel.Items[1], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    delayedVote.SetException(new InvalidOperationException("Mutation failed."));
    await firstVote;

    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal(2.25, viewModel.Items[1].VoteScoreNet);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task VotePostAsyncRollsBackOnFailure()
  {
    var service = new RecordingPostsService
    {
      FeedResponse = MakeResponse(
          new Post("post-1", "review", "Card review", "Body", "user-1"),
          voteScoreNet: 2.25,
          voteCountUp: 3,
          voteCountDown: 1),
      FailVotes = true,
    };
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VotePostAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.Equal(2.25, viewModel.Items[0].VoteScoreNet);
    Assert.Equal("Mutation failed.", viewModel.ErrorMessage);
    Assert.True(viewModel.HasError);
  }

  [Fact]
  public async Task VotePostAsyncRollsBackAndRequestsEmailRecovery()
  {
    var service = new RecordingPostsService
    {
      FeedResponse = MakeResponse(
          new Post("post-1", "review", "Card review", "Body", "user-1"),
          voteScoreNet: 2.25,
          voteCountUp: 3,
          voteCountDown: 1),
      VoteException = VerificationException(),
    };
    var viewModel = new PostsListViewModel(service);
    await viewModel.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VotePostAsync(viewModel.Items[0], ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Items[0].VoteCountUp);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  private static PostsFeedResponse MakeResponse(
      Post post,
      double? voteScoreNet = null,
      int? voteCountUp = null,
      int? voteCountDown = null,
      PageInfo? pageInfo = null) =>
      new(
          [new EntityReference("post", post.Id, post.Id, null, null, null, null, null, null, null, null)],
          pageInfo ?? new PageInfo(null, false, null),
          new Dictionary<string, Post>(StringComparer.Ordinal) { [post.Id] = post },
          new Dictionary<string, User>(StringComparer.Ordinal),
          new Dictionary<string, Community>(StringComparer.Ordinal),
          voteScoreNet is null
              ? null
              : new Dictionary<string, PostElection>(StringComparer.Ordinal)
              {
                [post.Id] = new PostElection("post_election", post.Id, voteScoreNet.Value, voteCountUp ?? 0, voteCountDown ?? 0),
              },
          ElectionVotes: null);

  private class RecordingPostsService : IPostsService
  {
    public PostsFeedResponse? FeedResponse { get; init; }

    public FetchPostsFeedRequest? LastFeedRequest { get; private set; }

    public List<(string Id, ElectionVoteChoice Choice)> VoteCalls { get; } = [];

    public List<string> ClearVoteCalls { get; } = [];

    public bool FailVotes { get; init; }

    public Exception? VoteException { get; init; }

    public IReadOnlyDictionary<string, TaskCompletionSource>? VoteCompletions { get; init; }

    public virtual Task<PostsFeedResponse> FetchFeedAsync(
        FetchPostsFeedRequest request,
        CancellationToken cancellationToken = default)
    {
      LastFeedRequest = request;
      return Task.FromResult(FeedResponse ?? throw new InvalidOperationException("Missing response."));
    }

    public virtual Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(FeedResponse ?? throw new InvalidOperationException("Missing response."));

    public Task<PostResponse> FetchPostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreatePostAsync(CreatePostBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UpdatePostAsync(string postIdOrSlug, UpdatePostBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VotePostAsync(string postId, ElectionVoteChoice choice, CancellationToken cancellationToken = default)
    {
      VoteCalls.Add((postId, choice));
      if (VoteCompletions?.TryGetValue(postId, out var completion) == true)
      {
        return completion.Task;
      }

      if (VoteException is not null) return Task.FromException(VoteException);

      return FailVotes
          ? Task.FromException(new InvalidOperationException("Mutation failed."))
          : Task.CompletedTask;
    }

    public Task ClearPostVoteAsync(string postId, CancellationToken cancellationToken = default)
    {
      ClearVoteCalls.Add(postId);
      return Task.CompletedTask;
    }

    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private static VouchaApiException VerificationException() =>
      new(System.Net.HttpStatusCode.Forbidden, "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}");

  private sealed class ThrowingPostsService : RecordingPostsService
  {
    public override Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<PostsFeedResponse>(new InvalidOperationException("Posts unavailable."));
  }

  private sealed class CancelingPostsService : RecordingPostsService
  {
    public override Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromCanceled<PostsFeedResponse>(new CancellationToken(canceled: true));
  }

  private sealed class SequencePostsService(params PostsFeedResponse[] responses) : RecordingPostsService
  {
    private readonly Queue<PostsFeedResponse> responses = new(responses);
    public List<FetchPostsFeedRequest> Requests { get; } = [];

    public override Task<PostsFeedResponse> FetchFeedAsync(
        FetchPostsFeedRequest request,
        CancellationToken cancellationToken = default)
    {
      Requests.Add(request);
      return Task.FromResult(responses.Dequeue());
    }
  }
}
