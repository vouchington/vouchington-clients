using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class CommentThreadViewModelTests
{
  [Fact]
  public async Task LoadAsyncBuildsTreeAndSupportsSortChanges()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var firstReply = NewPost("comment-1", parentId: root.Id, createdById: "user-2", markdown: "First reply", createdAt: Now(2));
    var secondReply = NewPost("comment-2", parentId: root.Id, createdById: "user-3", markdown: "Second reply", createdAt: Now(3));
    var nestedReply = NewPost("comment-3", parentId: firstReply.Id, createdById: "user-4", markdown: "Nested reply", createdAt: Now(4));

    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(
          firstReply,
          secondReply,
          nestedReply,
          new Dictionary<string, PostElection>(StringComparer.Ordinal)
          {
            [firstReply.Id] = new PostElection("post_election", firstReply.Id, 2, 3, 1),
            [secondReply.Id] = new PostElection("post_election", secondReply.Id, 6, 6, 0),
            [nestedReply.Id] = new PostElection("post_election", nestedReply.Id, 1, 1, 0),
          }),
    };
    var viewModel = new CommentThreadViewModel(service, root.Id, "user-1");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("comments-collapsed:root-1:user-1", viewModel.CollapseStateKey);
    Assert.Equal(root.Id, viewModel.RootPost?.Id);
    Assert.Equal(root.Id, viewModel.RootPostId);
    Assert.Equal("user-1", viewModel.CurrentUserId);
    Assert.Equal("Root body", viewModel.RootPost?.Markdown);
    Assert.Equal("<p>Root body</p>", viewModel.RootHtml);
    Assert.Same(service.RootResponse, viewModel.RootResponse);
    Assert.Same(service.DescendantsResponse, viewModel.DescendantsResponse);
    Assert.Null(viewModel.AncestorsResponse);
    Assert.Null(viewModel.RootElection);
    Assert.Null(viewModel.RootElectionVote);
    Assert.Empty(viewModel.RootBookmarks);
    Assert.Equal(3, viewModel.CommentElections.Count);
    Assert.Empty(viewModel.CommentElectionVotes);
    Assert.Equal(3, viewModel.CommentBookmarks.Count);
    Assert.Equal("<p>First reply</p>", viewModel.CommentHtml[firstReply.Id]);
    Assert.False(viewModel.HasError);
    Assert.Collection(viewModel.Comments,
        comment =>
        {
          Assert.Equal(secondReply.Id, comment.Id);
          Assert.Empty(comment.Children);
        },
        comment =>
        {
          Assert.Equal(firstReply.Id, comment.Id);
          Assert.Single(comment.Children);
          Assert.Equal(nestedReply.Id, comment.Children[0].Id);
        });

    viewModel.Sort = CommentThreadSorts.New;

    Assert.Collection(viewModel.Comments,
        comment => Assert.Equal(secondReply.Id, comment.Id),
        comment => Assert.Equal(firstReply.Id, comment.Id));

    viewModel.Sort = "unknown";
    Assert.Equal(CommentThreadSorts.Best, viewModel.Sort);
    viewModel.ToggleCollapse(firstReply.Id);
    Assert.Contains(firstReply.Id, viewModel.CollapsedCommentIds);
    viewModel.ToggleCollapse(firstReply.Id);
    Assert.DoesNotContain(firstReply.Id, viewModel.CollapsedCommentIds);
  }

  [Fact]
  public async Task LoadPermalinkAsyncTracksFocusedAncestorLoadingState()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var parent = NewPost("comment-1", parentId: root.Id, createdById: "user-2", markdown: "Parent reply", createdAt: Now(1));
    var target = NewPost("comment-2", parentId: parent.Id, createdById: "user-3", markdown: "Target reply", createdAt: Now(2));

    var ancestorsRelease = new TaskCompletionSource<PostThreadResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(parent, target),
      AncestorsResponseFactory = () => ancestorsRelease.Task,
      AncestorsRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
      PermalinkAncestorsResponse = MakeThreadResponse(root, parent, target),
    };
    var viewModel = new CommentThreadViewModel(service, root.Id);

    var loadTask = viewModel.LoadPermalinkAsync(target.Id, TestContext.Current.CancellationToken);
    await service.AncestorsRequested!.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsLoading);
    Assert.True(viewModel.IsPermalinkLoading);
    Assert.True(viewModel.IsLoadingAncestors);
    Assert.True(viewModel.IsLoadingDescendants);
    Assert.Equal(target.Id, viewModel.FocusedCommentId);

    ancestorsRelease.SetResult(service.PermalinkAncestorsResponse!);
    await loadTask;

    Assert.False(viewModel.IsLoadingAncestors);
    Assert.False(viewModel.IsLoadingDescendants);
    Assert.Equal(target.Id, viewModel.FocusedComment?.Id);
    Assert.Collection(viewModel.AncestorPosts,
        ancestor => Assert.Equal(root.Id, ancestor.Id),
        ancestor => Assert.Equal(parent.Id, ancestor.Id));
  }

  [Fact]
  public async Task MutationHelpersTargetExpectedEndpoints()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var comment = NewPost("comment-1", parentId: root.Id, createdById: "user-2", markdown: "Reply", createdAt: Now(1));
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(comment),
    };
    var viewModel = new CommentThreadViewModel(service, service, root.Id, "user-1");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.VotePostAsync(comment.Id, ElectionVoteChoice.Like, TestContext.Current.CancellationToken);
    await viewModel.ToggleSavePostAsync(comment.Id, false, TestContext.Current.CancellationToken);
    await viewModel.ReplyAsync(comment.Id, "Reply body", true, TestContext.Current.CancellationToken);
    await viewModel.EditAsync(comment.Id, "Edited body", TestContext.Current.CancellationToken);
    await viewModel.ReportAsync(comment.Id, "spam", "details", "turnstile", TestContext.Current.CancellationToken);
    await viewModel.DeleteAsync(comment.Id, TestContext.Current.CancellationToken);
    await viewModel.LockAsync(comment.Id, TestContext.Current.CancellationToken);
    await viewModel.UnlockAsync(comment.Id, TestContext.Current.CancellationToken);

    Assert.Equal(new[] { ("comment-1", ElectionVoteChoice.Like) }, service.VoteCalls);
    Assert.Equal(new[] { ("comment-1", "save", true) }, service.BookmarkCalls);
    Assert.Equal("comment-1", service.CreatePostPostId);
    Assert.Equal("root-1", service.CreatePostRootId);
    Assert.Equal("comment-1", service.CreatePostParentId);
    Assert.Equal("Reply body", service.CreatePostMarkdown);
    Assert.True(service.CreatePostAnonymous);
    Assert.Equal("comment-1", service.UpdatePostId);
    Assert.Equal("Edited body", service.UpdatePostMarkdown);
    Assert.Equal(("post", "comment-1", "spam", "details", "turnstile"), service.ReportCall);
    Assert.Equal(new[] { "comment-1" }, service.DeleteCalls);
    Assert.Equal(new[] { "comment-1" }, service.LockCalls);
    Assert.Equal(new[] { "comment-1" }, service.UnlockCalls);
  }

  [Fact]
  public async Task VoteRequestsEmailRecoveryWithoutReloadingWhenVerificationIsRequired()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(),
      VoteException = new VouchaApiException(
          System.Net.HttpStatusCode.Forbidden,
          "{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}"),
    };
    var viewModel = new CommentThreadViewModel(service, service, root.Id, "user-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var rootFetches = service.RootFetchCount;

    await viewModel.VotePostAsync(root.Id, ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(rootFetches, service.RootFetchCount);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    Assert.False(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task LoadPermalinkAsyncNormalizesBlankFocusAndLoadErrorsResetState()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root body", createdAt: Now(0));
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(),
    };
    var viewModel = new CommentThreadViewModel(service, root.Id);

    await viewModel.LoadPermalinkAsync("  ", TestContext.Current.CancellationToken);

    Assert.Null(viewModel.FocusedCommentId);
    Assert.False(viewModel.IsPermalinkLoading);
    Assert.False(viewModel.IsLoadingAncestors);
    Assert.False(viewModel.IsLoadingDescendants);
    Assert.Equal(LoadState.Loaded, viewModel.State);

    var failing = new CommentThreadViewModel(new RecordingPostsService(), root.Id);
    await failing.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(failing.HasError);
    Assert.False(failing.IsLoading);
    Assert.False(failing.IsLoadingAncestors);
    Assert.False(failing.IsLoadingDescendants);
    Assert.NotNull(failing.ErrorMessage);
    Assert.Empty(failing.Comments);
    Assert.Empty(failing.AncestorPosts);
    Assert.Null(failing.FocusedComment);
  }

  [Fact]
  public void CommentThreadRowsExposeDisplayFallbacks()
  {
    var unlocked = NewPost(
        "comment-1",
        createdById: null,
        title: "",
        markdown: null,
        html: "<p>Body</p>",
        createdAt: Now(1));
    var locked = unlocked with
    {
      Id = "comment-2",
      CreatedById = "user-2",
      CreatedBy = new User("user-2", "alice", null),
      LockedAt = Now(2),
      Title = "Locked title",
      Markdown = "Locked body",
    };

    var node = new CommentThreadNodeViewModel(locked, [new CommentThreadNodeViewModel(unlocked, [])]);
    Assert.Equal("comment-2", node.Id);
    Assert.True(node.HasChildren);

    var fallbackRow = new CommentThreadRow(unlocked, 1, false, false);
    Assert.Equal("comment-1", fallbackRow.Id);
    Assert.Equal("Comment", fallbackRow.LocalizedTitle);
    Assert.Equal("<p>Body</p>", fallbackRow.Body);
    Assert.Equal("Anonymous", fallbackRow.Author);
    Assert.Equal("Anonymous", fallbackRow.Metadata);

    var lockedRow = new CommentThreadRow(locked, 0, true, true);
    Assert.Equal("Locked title", lockedRow.LocalizedTitle);
    Assert.Equal("Locked body", lockedRow.Body);
    Assert.Equal("alice", lockedRow.Author);
    Assert.Equal("alice · locked", lockedRow.Metadata);
  }

  [Theory]
  [InlineData("root-1", null, "comments-collapsed:root-1")]
  [InlineData("root-1", "user-1", "comments-collapsed:root-1:user-1")]
  public void CollapseStateKeyUsesOptionalUserId(
      string rootPostId,
      string? userId,
      string expected)
  {
    Assert.Equal(expected, CommentThreadViewModel.BuildCollapseStateKey(rootPostId, userId));
  }

  private static DateTimeOffset Now(int minutes) =>
      new(2026, 6, 28, 10, minutes, 0, TimeSpan.Zero);

  private static Post NewPost(
      string id,
      string? parentId = null,
      string? createdById = "user-1",
      string postType = "comment",
      string? title = null,
      string? markdown = "Reply",
      string? html = null,
      DateTimeOffset? createdAt = null) =>
      new(
          id,
          postType,
          title,
          markdown,
          createdById,
          CreatedBy: createdById is null
              ? null
              : new User(createdById, $"{createdById}-name", markdown),
          CreatedAt: createdAt ?? Now(0),
          Html: html,
          ParentId: parentId,
          RootId: parentId is null ? null : "root-1",
          Privacy: "public",
          Broadcast: "everyone",
          IsAnonymous: false,
          CanEditContent: true,
          CanDelete: true,
          CanLock: true);

  private static PostResponse MakeDetailResponse(Post post) =>
      new(post, Html: $"<p>{post.Markdown}</p>", PostMetrics: new PostMetrics("post_metrics", post.Id, new PostMetricCounts(0)));

  private static PostThreadResponse MakeThreadResponse(
      params object[] items)
  {
    var posts = items.OfType<Post>().ToArray();
    var elections = items.OfType<IReadOnlyDictionary<string, PostElection>>().SingleOrDefault();
    var postElections = elections is null
        ? null
        : new Dictionary<string, PostElection>(elections, StringComparer.Ordinal);

    IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> bookmarks = posts.ToDictionary(
        post => post.Id,
        _ => (IReadOnlyDictionary<string, bool>)new Dictionary<string, bool>(StringComparer.Ordinal),
        StringComparer.Ordinal);

    return new PostThreadResponse(
        posts.Select(post => new EntityReference("post", post.Id, post.Id, null, null, null, null, null, null, null, null)).ToArray(),
        new PageInfo(null, false, null),
        posts.ToDictionary(post => post.Id, StringComparer.Ordinal),
        Users: new Dictionary<string, User>(StringComparer.Ordinal),
        Communities: new Dictionary<string, Community>(StringComparer.Ordinal),
        PostsMetrics: posts.ToDictionary(post => post.Id, post => new PostMetrics("post_metrics", post.Id, new PostMetricCounts(0)), StringComparer.Ordinal),
        PostElections: postElections,
        MarkdownToHtml: posts.ToDictionary(post => post.Id, post => $"<p>{post.Markdown}</p>", StringComparer.Ordinal),
        Bookmarks: bookmarks);
  }

  private sealed class RecordingPostsService : ICommentThreadService, IPostsService
  {
    public PostResponse? RootResponse { get; init; }

    public PostThreadResponse? DescendantsResponse { get; init; }

    public Queue<PostThreadResponse> DescendantPageResponses { get; } = [];

    public List<string?> DescendantPageCursors { get; } = [];

    public Func<Task<PostThreadResponse>>? AncestorsResponseFactory { get; init; }

    public TaskCompletionSource<bool>? AncestorsRequested { get; init; }

    public PostThreadResponse? PermalinkAncestorsResponse { get; init; }

    public List<(string PostId, ElectionVoteChoice Choice)> VoteCalls { get; } = [];

    public Exception? VoteException { get; init; }

    public int RootFetchCount { get; private set; }

    public List<(string PostId, string Predicate, bool IsSaved)> BookmarkCalls { get; } = [];

    public string? CreatePostPostId { get; private set; }

    public string? CreatePostRootId { get; private set; }

    public string? CreatePostParentId { get; private set; }

    public string? CreatePostMarkdown { get; private set; }

    public bool CreatePostAnonymous { get; private set; }

    public string? UpdatePostId { get; private set; }

    public string? UpdatePostMarkdown { get; private set; }

    public (string EntityType, string EntityId, string Reason, string? Note, string? Token)? ReportCall { get; private set; }

    public List<string> DeleteCalls { get; } = [];

    public List<string> LockCalls { get; } = [];

    public List<string> UnlockCalls { get; } = [];

    public Task<PostsFeedResponse> FetchFeedAsync(FetchPostsFeedRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostsFeedResponse> FetchPostsAsync(FetchPostsRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostResponse> FetchPostAsync(string postIdOrSlug, CancellationToken cancellationToken = default)
    {
      RootFetchCount++;
      return Task.FromResult(RootResponse ?? throw new InvalidOperationException("Missing root response."));
    }

    public Task<PostThreadResponse> FetchPostDescendantsAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult(DescendantsResponse ?? throw new InvalidOperationException("Missing descendants response."));

    public Task<PostThreadResponse> FetchPostDescendantsPageAsync(
        string postIdOrSlug,
        string? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
      DescendantPageCursors.Add(after);
      return Task.FromResult(after is null
          ? DescendantsResponse ?? throw new InvalidOperationException("Missing descendants response.")
          : DescendantPageResponses.Dequeue());
    }

    public Task<PostThreadResponse> FetchPostAncestorsAsync(string postIdOrSlug, CancellationToken cancellationToken = default)
    {
      AncestorsRequested?.TrySetResult(true);
      return AncestorsResponseFactory is null
          ? Task.FromResult(PermalinkAncestorsResponse ?? throw new InvalidOperationException("Missing ancestors response."))
          : AncestorsResponseFactory();
    }

    public Task LockPostAsync(string postIdOrSlug, CancellationToken cancellationToken = default)
    {
      LockCalls.Add(postIdOrSlug);
      return Task.CompletedTask;
    }

    public Task UnlockPostAsync(string postIdOrSlug, CancellationToken cancellationToken = default)
    {
      UnlockCalls.Add(postIdOrSlug);
      return Task.CompletedTask;
    }

    public Task VotePostAsync(string postId, ElectionVoteChoice choice, CancellationToken cancellationToken = default)
    {
      VoteCalls.Add((postId, choice));
      return VoteException is null ? Task.CompletedTask : Task.FromException(VoteException);
    }

    public Task ClearPostVoteAsync(string postId, CancellationToken cancellationToken = default) =>
        VotePostAsync(postId, ElectionVoteChoice.Neutral, cancellationToken);

    public Task BookmarkPostAsync(string postIdOrSlug, string predicate = "save", CancellationToken cancellationToken = default)
    {
      BookmarkCalls.Add((postIdOrSlug, predicate, true));
      return Task.CompletedTask;
    }

    public Task UnbookmarkPostAsync(string postIdOrSlug, string predicate = "save", CancellationToken cancellationToken = default)
    {
      BookmarkCalls.Add((postIdOrSlug, predicate, false));
      return Task.CompletedTask;
    }

    public Task ReportAsync(ReportBody body, CancellationToken cancellationToken = default)
    {
      ReportCall = (body.EntityType, body.EntityId, body.Reason, body.Note, body.TurnstileToken);
      return Task.CompletedTask;
    }

    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default)
    {
      DeleteCalls.Add(postIdOrSlug);
      return Task.CompletedTask;
    }

    public Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      CreatePostPostId = body.ParentId ?? body.RootId ?? "post-1";
      CreatePostRootId = body.RootId;
      CreatePostParentId = body.ParentId;
      CreatePostMarkdown = body.Markdown;
      CreatePostAnonymous = body.IsAnonymous;
      return Task.FromResult(
          new PostMutationResponse(NewPost("created-comment", body.ParentId, postType: body.PostType, markdown: body.Markdown)));
    }

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UpdatePostAsync(string postIdOrSlug, UpdatePostBody body, CancellationToken cancellationToken = default)
    {
      UpdatePostId = postIdOrSlug;
      UpdatePostMarkdown = body.Markdown;
      return Task.FromResult(new PostMutationResponse(NewPost(postIdOrSlug, markdown: body.Markdown ?? string.Empty)));
    }

    public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
