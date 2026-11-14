using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class CommentThreadViewModelTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("viewer")]
  public async Task BoundedPreviewContinuesPastFilteredHeadsAndExpandsReturnedChildren(string? viewer)
  {
    var first = DescendantFixture("short-continuation");
    var empty = DescendantFixture("empty-continuation");
    var last = DescendantFixture("after-filtered-head");
    var expansion = DescendantFixture("expand-preview-child");
    var head = first.Posts.Values.Single(post => post.ParentId == post.RootId);
    var previewChild = first.Posts.Values.Single(post => post.ParentId == head.Id);
    var root = NewPost(head.RootId!, postType: "discussion");
    var calls = new List<(string Id, string? Cursor)>();
    var service = new RecordingPostsService
    {
      RootResponse = MakeDetailResponse(root),
      DescendantsResponse = first,
      DescendantHandler = (id, cursor) =>
      {
        calls.Add((id, cursor));
        return Task.FromResult((id, cursor) switch
        {
          var request when request == (root.Id, null) => first,
          var request when request == (root.Id, first.PageInfo.EndCursor) => empty,
          var request when request == (root.Id, empty.PageInfo.EndCursor) => last,
          var request when request == (previewChild.Id, null) => expansion,
          _ => throw new InvalidOperationException("Unexpected descendants request."),
        });
      },
    };
    var model = new CommentThreadViewModel(service, root.Id, viewer);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasMoreDescendants);
    Assert.True(model.HasMoreSubtree(previewChild.Id));
    Assert.Single(Assert.Single(model.Comments).Children);
    await model.LoadMoreSubtreeAsync(previewChild.Id, TestContext.Current.CancellationToken);
    Assert.True(model.HasMoreDescendants);
    await model.LoadMoreDescendantsAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasMoreDescendants);
    Assert.Equal(3, model.DescendantsResponse!.Results.Count);
    await model.LoadMoreDescendantsAsync(TestContext.Current.CancellationToken);
    Assert.False(model.HasMoreDescendants);
    Assert.Equal(2, model.Comments.Count);
    var expandedHead = model.Comments.Single(node => node.Id == head.Id);
    var child = Assert.Single(expandedHead.Children);
    Assert.Equal(previewChild.Id, child.Id);
    Assert.Equal(expansion.Posts.Single().Key, Assert.Single(child.Children).Id);
    Assert.False(model.HasMoreSubtree(previewChild.Id));
    Assert.False(model.HasMoreSubtree(Assert.Single(child.Children).Id));
    Assert.False(model.HasMoreDescendants);
    Assert.Equal(4, model.DescendantsResponse!.Results.Select(reference => reference.EntityId ?? reference.Id).Distinct().Count());
    await model.LoadMoreSubtreeAsync(previewChild.Id, TestContext.Current.CancellationToken);
    Assert.Equal(new[] { (root.Id, (string?)null), (previewChild.Id, (string?)null),
        (root.Id, first.PageInfo.EndCursor), (root.Id, empty.PageInfo.EndCursor) }, calls);
    Assert.Empty(service.BookmarkCalls);
  }

  private static PostThreadResponse DescendantFixture(string name) =>
      JsonSerializer.Deserialize<PostThreadResponse>(ApiFixtureLoader.LoadResponse(
          $"native.comments.descendants.{name}"), VouchaApiJson.Options)!;
}
