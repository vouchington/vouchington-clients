using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class CommentThreadViewModelTests
{
  [Fact]
  public async Task LandedDeletedFixtureRetainsItsPlaceInTheThreadWithoutContent()
  {
    const string topLevelId = "70000000-0000-7000-8000-000000005401";
    const string nestedId = "70000000-0000-7000-8000-000000005402";
    var fixture = JsonSerializer.Deserialize<PostThreadResponse>(
        ApiFixtureLoader.LoadResponse("native.comments.descendants.default"), VouchaApiJson.Options)!;
    // The producer fixture no longer lands a deleted comment, so tombstone the nested reply here.
    var deleted = fixture.Posts[nestedId] with { PlaceholderState = "deleted", DeletedAt = DateTimeOffset.UnixEpoch };
    var response = fixture with
    {
      Posts = new Dictionary<string, Post>(fixture.Posts) { [nestedId] = deleted },
    };
    var root = JsonSerializer.Deserialize<PostResponse>(
        ApiFixtureLoader.LoadResponse("native.comments.post-detail.default"), VouchaApiJson.Options)!;
    var service = new RecordingPostsService { RootResponse = root, DescendantsResponse = response };
    var model = new CommentThreadViewModel(service, root.Post.Id);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var row = new CommentThreadRow(deleted, 2, false, false);
    Assert.Equal("deleted", deleted.PlaceholderState);
    Assert.Equal("[deleted]", row.LocalizedTitle);
    Assert.Empty(row.Body);
    Assert.Empty(row.Author);
    Assert.Equal(nestedId, model.Comments.Single(node => node.Id == topLevelId).Children[0].Id);
    Assert.Equal(1, model.DirectReplyCount(topLevelId));
  }

  [Theory]
  [InlineData("deleted", "[deleted]")]
  [InlineData("unmoderated", "pending")]
  public void PlaceholderRedactsEvenUnexpectedContentAndAuthor(string state, string label)
  {
    var post = NewPost("placeholder", markdown: "sensitive") with { PlaceholderState = state, Title = "sensitive" };
    var row = new CommentThreadRow(post, 1, false, false);
    Assert.Equal(label, row.LocalizedTitle);
    Assert.Equal(label, row.Metadata);
    Assert.Empty(row.Body);
    Assert.Empty(row.Author);
    Assert.Null(row.AuthorAccountTypeLabel);
  }

  [Fact]
  public async Task ServerHideExpandsItsOwnPagesAndRootPagingRetainsMergedReplies()
  {
    var root = NewPost("root-1", postType: "discussion");
    var hidden = NewPost("hidden", parentId: root.Id);
    var child = NewPost("child", parentId: hidden.Id);
    var later = NewPost("later", parentId: hidden.Id);
    var other = NewPost("other", parentId: root.Id);
    var initial = MakeThreadResponse(hidden) with
    {
      PageInfo = new("root-opaque+/=", true, null),
      Bookmarks = Hide(hidden.Id),
      PostsMetrics = new Dictionary<string, PostMetrics> { [hidden.Id] = new(Count: new(20, Children: 7)) },
    };
    var calls = new List<(string Id, string? Cursor)>();
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = initial,
      DescendantHandler = (id, cursor) =>
      {
        calls.Add((id, cursor));
        return Task.FromResult((id, cursor) switch
        {
          ("root-1", null) => initial,
          ("hidden", null) => MakeThreadResponse(child) with { PageInfo = new("subtree-opaque+/=", true, null) },
          ("hidden", "subtree-opaque+/=") => MakeThreadResponse(child, later),
          ("root-1", "root-opaque+/=") => MakeThreadResponse(hidden, other),
          _ => throw new InvalidOperationException("Unexpected comment page."),
        });
      },
    };
    var model = new CommentThreadViewModel(service, root.Id, "viewer");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Contains(hidden.Id, model.CollapsedCommentIds);
    Assert.Equal(7, model.DirectReplyCount(hidden.Id));
    Assert.Null(model.CollapseStateKey);
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(hidden.Id, model.CollapsedCommentIds);
    Assert.True(model.HasMoreSubtree(hidden.Id));
    Assert.True(model.HasMoreDescendants);
    await model.LoadMoreSubtreeAsync(hidden.Id, TestContext.Current.CancellationToken);
    await model.LoadMoreDescendantsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, model.Comments.Single(node => node.Id == hidden.Id).Children.Count);
    Assert.Equal(4, model.DescendantsResponse!.Results.Count);
    Assert.False(model.HasMoreSubtree(hidden.Id));
    Assert.False(model.HasMoreDescendants);
    Assert.Contains((hidden.Id, "subtree-opaque+/="), calls);
    Assert.Contains((root.Id, "root-opaque+/="), calls);
    Assert.Equal([(hidden.Id, "hide", false)], service.BookmarkCalls);
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.Contains(hidden.Id, model.CollapsedCommentIds);
    Assert.Equal((hidden.Id, "hide", true), service.BookmarkCalls[^1]);
  }

  [Fact]
  public async Task FailedHideLeavesRowOpenAndFailedSubtreeRetriesSameOpaqueCursor()
  {
    var root = NewPost("root-1");
    var hidden = NewPost("hidden", parentId: root.Id);
    var fail = true;
    var failBookmark = true;
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(hidden),
      BookmarkHandler = (_, _) => failBookmark ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask,
    };
    var model = new CommentThreadViewModel(service, root.Id, "viewer");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(hidden.Id, model.CollapsedCommentIds);
    Assert.Equal("offline", model.ErrorMessage);
    failBookmark = false;
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    service.DescendantHandler = (_, cursor) => fail
        ? Task.FromException<PostThreadResponse>(new HttpRequestException("offline"))
        : Task.FromResult(MakeThreadResponse(NewPost("child", parentId: hidden.Id)));
    fail = true;
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.False(model.IsCollapseBusy(hidden.Id));
    Assert.True(model.HasSubtreeError(hidden.Id));
    Assert.DoesNotContain(hidden.Id, model.CollapsedCommentIds);
    fail = false;
    await model.LoadMoreSubtreeAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.False(model.HasSubtreeError(hidden.Id));
    Assert.Equal("child", Assert.Single(Assert.Single(model.Comments).Children).Id);
  }

  [Fact]
  public async Task AccountChangeInvalidatesPendingHideAndSubtreeResponses()
  {
    var root = NewPost("root-1");
    var hidden = NewPost("hidden", parentId: root.Id);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(hidden),
      BookmarkHandler = (_, _) => release.Task,
    };
    var model = new CommentThreadViewModel(service, root.Id, "viewer");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var hiding = model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.Single(service.BookmarkCalls);
    Assert.True(model.SetViewer("other"));
    release.SetResult();
    await hiding;
    Assert.Empty(model.CollapsedCommentIds);
    Assert.Empty(model.Comments);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = new TaskCompletionSource<PostThreadResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.DescendantHandler = (_, _) => page.Task;
    var loading = model.LoadMoreSubtreeAsync(hidden.Id, TestContext.Current.CancellationToken);
    model.SetViewer(null);
    page.SetResult(MakeThreadResponse(NewPost("private-child", parentId: hidden.Id)));
    await loading;
    Assert.Empty(model.Comments);
    Assert.Null(model.DescendantsResponse);
    Assert.Equal("comments-collapsed:root-1", model.CollapseStateKey);
  }

  [Fact]
  public async Task AnonymousCollapseUsesOnlyAnonymousStateAndDoesNotCallBookmarkApi()
  {
    var root = NewPost("root-1");
    var child = NewPost("child", parentId: root.Id);
    var service = new RecordingPostsService { RootResponse = MakeDetailResponse(root), DescendantsResponse = MakeThreadResponse(child) with { Bookmarks = Hide(child.Id) } };
    var model = new CommentThreadViewModel(service, root.Id);
    model.RestoreAnonymousCollapse([child.Id]);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Contains(child.Id, model.CollapsedCommentIds);
    await model.ToggleCollapseAsync(child.Id, TestContext.Current.CancellationToken);
    Assert.Empty(model.CollapsedCommentIds);
    Assert.Empty(service.BookmarkCalls);
    model.SetViewer("signed-in");
    model.RestoreAnonymousCollapse([child.Id]);
    Assert.Empty(model.CollapsedCommentIds);
  }

  [Fact]
  public async Task PermalinkPreservesPendingAncestorAndFocusedReply()
  {
    var root = NewPost("root-1");
    var pending = NewPost("pending", parentId: root.Id) with { PlaceholderState = "unmoderated" };
    var focused = NewPost("focused", parentId: pending.Id);
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(),
      PermalinkAncestorsResponse = MakeThreadResponse(root, pending, focused),
    };
    var model = new CommentThreadViewModel(service, root.Id);
    await model.LoadPermalinkAsync(focused.Id, TestContext.Current.CancellationToken);
    Assert.False(model.HasError);
    Assert.Equal("focused", model.FocusedComment?.Id);
    var row = new CommentThreadRow(model.AncestorPosts.Single(post => post.Id == pending.Id), 0, false, false);
    Assert.Equal("pending", row.LocalizedTitle);
    Assert.Empty(row.Body);
    Assert.Empty(row.Author);
  }

  [Fact]
  public async Task UnexpectedMutationAndMalformedSubtreeFailuresSettleAndAllowRetry()
  {
    var root = NewPost("root-1");
    var hidden = NewPost("hidden", parentId: root.Id);
    var malformed = true;
    var mutationFailure = true;
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(hidden),
      BookmarkHandler = (_, _) => mutationFailure ? Task.FromException(new JsonException("invalid bookmark response")) : Task.CompletedTask,
    };
    var model = new CommentThreadViewModel(service, root.Id, "viewer");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(hidden.Id, model.CollapsedCommentIds);
    Assert.False(model.IsCollapseBusy(hidden.Id));
    Assert.Equal("invalid bookmark response", model.ErrorMessage);
    mutationFailure = false;
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    service.DescendantHandler = (_, _) => malformed
        ? Task.FromException<PostThreadResponse>(new JsonException("invalid subtree response"))
        : Task.FromResult(MakeThreadResponse(NewPost("child", parentId: hidden.Id)));
    await model.ToggleCollapseAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.True(model.HasSubtreeError(hidden.Id));
    Assert.False(model.IsCollapseBusy(hidden.Id));
    malformed = false;
    await model.LoadMoreSubtreeAsync(hidden.Id, TestContext.Current.CancellationToken);
    Assert.False(model.HasSubtreeError(hidden.Id));
    Assert.False(model.IsCollapseBusy(hidden.Id));
    Assert.Equal("child", Assert.Single(Assert.Single(model.Comments).Children).Id);
  }

  [Fact]
  public async Task DeletingAParentRefetchesItsPlaceholderAndLeafDeleteRetainsExpandedSiblings()
  {
    var root = NewPost("root-1");
    var parent = NewPost("parent", parentId: root.Id);
    var child = NewPost("child", parentId: parent.Id);
    var sibling = NewPost("sibling", parentId: parent.Id);
    var response = MakeThreadResponse(parent, child);
    var service = new RecordingPostsService { RootResponse = MakeDetailResponse(root), DescendantsResponse = response };
    var model = new CommentThreadViewModel(service, root.Id, "viewer");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.DescendantHandler = (_, _) => Task.FromResult(MakeThreadResponse(parent with { PlaceholderState = "deleted", Markdown = "", CreatedBy = null }, child));
    await model.DeleteAsync(parent.Id, TestContext.Current.CancellationToken);
    Assert.Equal(2, service.RootFetchCount);
    Assert.Equal("deleted", Assert.Single(model.Comments).Post.PlaceholderState);
    service.DescendantHandler = (_, _) => Task.FromResult(MakeThreadResponse(child, sibling));
    await model.LoadMoreSubtreeAsync(parent.Id, TestContext.Current.CancellationToken);
    await model.DeleteAsync(child.Id, TestContext.Current.CancellationToken);
    Assert.Equal("sibling", Assert.Single(Assert.Single(model.Comments).Children).Id);
    Assert.Equal(2, model.DescendantsResponse!.Results.Count);
  }

  private static Dictionary<string, IReadOnlyDictionary<string, bool>> Hide(string id) =>
      new() { [id] = new Dictionary<string, bool> { ["hide"] = true } };

  private sealed partial class RecordingPostsService
  {
    public Func<string, string?, Task<PostThreadResponse>>? DescendantHandler { get; set; }
    public Func<string, bool, Task>? BookmarkHandler { get; init; }
  }
}
