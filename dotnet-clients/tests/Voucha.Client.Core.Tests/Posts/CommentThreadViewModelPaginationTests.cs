using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class CommentThreadViewModelTests
{
  [Fact]
  public async Task DescendantContinuationAppendsUniqueRowsAndForwardsTheCursor()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root");
    var first = NewPost("comment-1", parentId: root.Id, markdown: "First");
    var second = NewPost("comment-2", parentId: root.Id, markdown: "Second");
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(first) with
      {
        PageInfo = new PageInfo("next", true, null),
      },
    };
    service.DescendantPageResponses.Enqueue(MakeThreadResponse(first, second));
    var viewModel = new CommentThreadViewModel(service, root.Id);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreDescendantsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["comment-1", "comment-2"], viewModel.Comments.Select(row => row.Id));
    Assert.Equal([null, "next"], service.DescendantPageCursors);
    Assert.False(viewModel.HasMoreDescendants);
  }

  [Fact]
  public async Task AncestorContinuationPrependsUniqueRowsAndRetriesSameCursor()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root");
    var near = NewPost("comment-near", parentId: root.Id, markdown: "Near");
    var target = NewPost("comment-target", parentId: near.Id, markdown: "Target");
    var far = NewPost("comment-far", parentId: root.Id, markdown: "Far");
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(target),
      PermalinkAncestorsResponse = MakeThreadResponse(root, near, target) with
      {
        PageInfo = new PageInfo("ancestor-next", true, null),
      },
    };
    service.AncestorPageResponses.Enqueue(new InvalidOperationException("offline"));
    service.AncestorPageResponses.Enqueue(MakeThreadResponse(root, far, near) with
    {
      PageInfo = new PageInfo(null, false, null),
    });
    var viewModel = new CommentThreadViewModel(service, root.Id);

    await viewModel.LoadPermalinkAsync(target.Id, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAncestorsAsync(TestContext.Current.CancellationToken);
    Assert.Equal([root.Id, near.Id], viewModel.AncestorPosts.Select(post => post.Id));
    Assert.True(viewModel.HasAncestorPaginationError);

    await viewModel.LoadMoreAncestorsAsync(TestContext.Current.CancellationToken);
    Assert.Equal([root.Id, far.Id, near.Id], viewModel.AncestorPosts.Select(post => post.Id));
    Assert.False(viewModel.HasAncestorPaginationError);
    Assert.Equal([null, "ancestor-next", "ancestor-next"], service.AncestorPageCursors);
    Assert.Equal([5, 5, 5], service.AncestorPageLimits);
  }

  [Fact]
  public async Task DescendantContinuationKeepsSideMapsFromBothPages()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root");
    var first = NewPost("comment-1", parentId: root.Id, markdown: "First");
    var second = NewPost("comment-2", parentId: root.Id, markdown: "Second");
    var earlierElection = new PostElection("post_election", "election-earlier", 1, 1, 0);
    var earlierVote = new ElectionVote(
        "election_vote", first.Id, "user-earlier", ElectionVoteChoice.Vouch, Now(1));
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(first) with
      {
        PageInfo = new PageInfo("next", true, null),
        Users = Users(("user-earlier", "earlier"), ("user-shared", "before")),
        Communities = Communities("community-earlier"),
        PostElections = Elections((first.Id, earlierElection)),
        ElectionVotes = Votes((first.Id, earlierVote)),
        PostLinkEmbeds = Embeds((first.Id, "earlier")),
      },
    };
    service.DescendantPageResponses.Enqueue(MakeThreadResponse(second) with
    {
      Users = Users(("user-later", "later"), ("user-shared", "after")),
      Communities = Communities("community-later"),
      PostLinkEmbeds = Embeds((second.Id, "later")),
    });
    var viewModel = new CommentThreadViewModel(service, root.Id);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreDescendantsAsync(TestContext.Current.CancellationToken);

    var merged = viewModel.DescendantsResponse;
    Assert.NotNull(merged);
    Assert.Equal([first.Id, second.Id], merged.Posts.Keys.Order().ToArray());
    Assert.NotNull(merged.Users);
    Assert.Equal("earlier", merged.Users["user-earlier"].Username);
    Assert.Equal("later", merged.Users["user-later"].Username);
    Assert.Equal("after", merged.Users["user-shared"].Username);
    Assert.NotNull(merged.Communities);
    Assert.Equal("community-earlier", merged.Communities["community-earlier"].Name);
    Assert.Equal("community-later", merged.Communities["community-later"].Name);
    Assert.NotNull(merged.MarkdownToHtml);
    Assert.Equal("<p>First</p>", merged.MarkdownToHtml[first.Id]);
    Assert.Equal("<p>Second</p>", merged.MarkdownToHtml[second.Id]);
    Assert.NotNull(merged.Bookmarks);
    Assert.Contains(first.Id, merged.Bookmarks.Keys);
    Assert.Contains(second.Id, merged.Bookmarks.Keys);
    Assert.NotNull(merged.PostsMetrics);
    Assert.Equal(first.Id, merged.PostsMetrics[first.Id].Id);
    Assert.Equal(second.Id, merged.PostsMetrics[second.Id].Id);
    Assert.NotNull(merged.PostElections);
    Assert.Equal(earlierElection, merged.PostElections[first.Id]);
    Assert.NotNull(merged.ElectionVotes);
    Assert.Equal(earlierVote, merged.ElectionVotes[first.Id]);
    Assert.NotNull(merged.PostLinkEmbeds);
    Assert.Equal("earlier", merged.PostLinkEmbeds[first.Id].Title);
    Assert.Equal("later", merged.PostLinkEmbeds[second.Id].Title);
  }

  [Fact]
  public async Task AncestorContinuationKeepsSideMapsFromBothPages()
  {
    var root = NewPost("root-1", postType: "discussion", title: "Root", markdown: "Root");
    var near = NewPost("comment-near", parentId: root.Id, markdown: "Near");
    var target = NewPost("comment-target", parentId: near.Id, markdown: "Target");
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = MakeThreadResponse(target),
      PermalinkAncestorsResponse = MakeThreadResponse(root, near, target) with
      {
        PageInfo = new PageInfo("ancestor-next", true, null),
        Users = Users(("user-earlier", "earlier")),
        PostLinkEmbeds = Embeds((near.Id, "earlier")),
      },
    };
    service.AncestorPageResponses.Enqueue(MakeThreadResponse(root, near, target) with
    {
      PageInfo = new PageInfo(null, false, null),
      Users = Users(("user-later", "later")),
      PostLinkEmbeds = Embeds((target.Id, "later")),
    });
    var viewModel = new CommentThreadViewModel(service, root.Id);

    await viewModel.LoadPermalinkAsync(target.Id, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAncestorsAsync(TestContext.Current.CancellationToken);

    var merged = viewModel.AncestorsResponse;
    Assert.NotNull(merged);
    Assert.NotNull(merged.Users);
    Assert.Equal("earlier", merged.Users["user-earlier"].Username);
    Assert.Equal("later", merged.Users["user-later"].Username);
    Assert.NotNull(merged.PostLinkEmbeds);
    Assert.Equal("earlier", merged.PostLinkEmbeds[near.Id].Title);
    Assert.Equal("later", merged.PostLinkEmbeds[target.Id].Title);
  }

  private static Dictionary<string, User> Users(params (string Id, string Username)[] users) =>
      users.ToDictionary(
          user => user.Id,
          user => new User(user.Id, user.Username),
          StringComparer.Ordinal);

  private static Dictionary<string, Community> Communities(params string[] ids)
  {
    var created = Now(0);
    return ids.ToDictionary(
        id => id,
        id => new Community(
            id, id, id, null, "public", "public", null,
            null, null, false, false, "user-1", created, created),
        StringComparer.Ordinal);
  }

  private static Dictionary<string, PostElection> Elections(
      params (string PostId, PostElection Election)[] entries) =>
      entries.ToDictionary(entry => entry.PostId, entry => entry.Election, StringComparer.Ordinal);

  private static Dictionary<string, ElectionVote> Votes(
      params (string PostId, ElectionVote Vote)[] entries) =>
      entries.ToDictionary(entry => entry.PostId, entry => entry.Vote, StringComparer.Ordinal);

  private static Dictionary<string, UrlEmbed> Embeds(params (string PostId, string Title)[] embeds) =>
      embeds.ToDictionary(
          embed => embed.PostId,
          embed => new UrlEmbed(Title: embed.Title),
          StringComparer.Ordinal);
}
