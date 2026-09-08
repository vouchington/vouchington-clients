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
}
