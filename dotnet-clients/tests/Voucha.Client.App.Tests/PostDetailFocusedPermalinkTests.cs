using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PostDetailFocusedPermalinkTests
{
  [Fact]
  public async Task AncestorOnlyFocusRendersOnceWithItsOwnMetadataAndActions()
  {
    var root = Post("root", null);
    var parent = Post("parent", root.Id);
    var target = Post("target", parent.Id) with { CanEditContent = true };
    var other = Post("other", root.Id);
    var service = new ThreadService(
        new PostResponse(root),
        Response([other], hasMore: true),
        Response([root, parent, target], target));
    service.NextDescendants = Response([target]);
    var model = new CommentThreadViewModel(service, root.Id, "author");
    var binding = new PostDetailPageBinding(model, new SessionStore(new(new User("author", "author"))));

    await model.LoadPermalinkAsync(target.Id, TestContext.Current.CancellationToken);
    binding.RefreshRows();

    Assert.DoesNotContain(model.AncestorPosts, post => post.Id == target.Id);
    Assert.DoesNotContain(model.Comments, node => node.Id == target.Id);
    var row = Assert.Single(binding.CommentRows, row => row.Id == target.Id);
    Assert.True(row.IsFocused);
    Assert.Equal("<p>Focused body</p>", row.BodyHtml);
    Assert.Equal(7, row.VoteCountUp);
    Assert.Equal(ElectionVoteChoice.Like, row.CurrentVoteChoice);
    Assert.True(row.IsSaved);
    Assert.True(row.CanReply);
    Assert.True(row.CanEdit);
    Assert.Equal(target.Id, row.Post.Id);

    await model.LoadMoreDescendantsAsync(TestContext.Current.CancellationToken);
    binding.RefreshRows();
    Assert.Single(binding.CommentRows, row => row.Id == target.Id);
    Assert.Equal("next", service.NextDescendantsCursor);
  }

  [Fact]
  public async Task FocusedRootWithSlugDoesNotDuplicateItsRow()
  {
    var root = Post("root", null);
    var service = new ThreadService(new PostResponse(root), Response([]), Response([root]));
    var model = new CommentThreadViewModel(service, "root-slug");
    var binding = new PostDetailPageBinding(model, new SessionStore(SessionSnapshot.Anonymous));

    await model.LoadPermalinkAsync(root.Id, TestContext.Current.CancellationToken);
    binding.RefreshRows();

    Assert.Equal(root.Id, binding.RootRow?.Id);
    Assert.Empty(binding.CommentRows);
  }

  [Fact]
  public async Task CollapsedDescendantDoesNotReappearAsDetachedFocus()
  {
    var root = Post("root", null);
    var parent = Post("parent", root.Id);
    var target = Post("target", parent.Id);
    var service = new ThreadService(new PostResponse(root), Response([parent, target]), Response([root, parent, target]));
    var model = new CommentThreadViewModel(service, root.Id);
    var binding = new PostDetailPageBinding(model, new SessionStore(SessionSnapshot.Anonymous));

    await model.LoadPermalinkAsync(target.Id, TestContext.Current.CancellationToken);
    model.ToggleCollapse(parent.Id);
    binding.RefreshRows();

    Assert.DoesNotContain(binding.CommentRows, row => row.Id == target.Id);
    Assert.Contains(binding.CommentRows, row => row.Id == parent.Id);
  }

  private static Post Post(string id, string? parentId) =>
      new(id, parentId is null ? "discussion" : "comment", null, id, "author",
          ParentId: parentId, RootId: parentId is null ? null : "root", CanEditContent: true);

  private static PostThreadResponse Response(Post[] posts, Post? target = null, bool hasMore = false) =>
      new(posts.Select(post => new EntityReference("post", post.Id, post.Id, null, null, null, null, null, null, null, null)).ToArray(),
          new PageInfo(hasMore ? "next" : null, hasMore, null),
          posts.ToDictionary(post => post.Id, StringComparer.Ordinal),
          PostElections: target is null ? null : new Dictionary<string, PostElection>
          {
            [target.Id] = new("post_election", target.Id, 6, 7, 1),
          },
          ElectionVotes: target is null ? null : new Dictionary<string, ElectionVote>
          {
            [target.Id] = new("election_vote", target.Id, "author", ElectionVoteChoice.Like, DateTimeOffset.UtcNow),
          },
          MarkdownToHtml: target is null ? null : new Dictionary<string, string>
          {
            [target.Id] = "<p>Focused body</p>",
          },
          Bookmarks: target is null ? null : new Dictionary<string, IReadOnlyDictionary<string, bool>>
          {
            [target.Id] = new Dictionary<string, bool> { ["save"] = true },
          });

  private sealed class SessionStore(SessionSnapshot current) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = current;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class ThreadService(PostResponse root, PostThreadResponse descendants, PostThreadResponse ancestors) : ICommentThreadService
  {
    public PostThreadResponse? NextDescendants { get; set; }
    public string? NextDescendantsCursor { get; private set; }
    public Task<PostResponse> FetchPostAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(root);
    public Task<PostThreadResponse> FetchPostDescendantsAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(descendants);
    public Task<PostThreadResponse> FetchPostDescendantsPageAsync(string id, string? after, int limit, CancellationToken cancellationToken = default)
    {
      if (after is null) return Task.FromResult(descendants);
      NextDescendantsCursor = after;
      return Task.FromResult(NextDescendants ?? throw new InvalidOperationException("Missing continuation response."));
    }
    public Task<PostThreadResponse> FetchPostAncestorsAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(ancestors);
    public Task LockPostAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnlockPostAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BookmarkPostAsync(string id, string predicate = "save", CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnbookmarkPostAsync(string id, string predicate = "save", CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReportAsync(ReportBody body, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeletePostAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
