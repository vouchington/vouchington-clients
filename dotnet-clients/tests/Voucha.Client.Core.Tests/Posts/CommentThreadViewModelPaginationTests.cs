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
}
