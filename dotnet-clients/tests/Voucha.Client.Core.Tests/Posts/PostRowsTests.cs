using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostRowsTests
{
  [Fact]
  public void FromCarriesCommentRootId()
  {
    var row = PostRows.From(
        new Post("comment-1", "comment", "Reply", "Body", "user-1", RootId: "root-1"),
        null,
        null,
        null);

    Assert.Equal("root-1", row.RootId);
  }

  [Fact]
  public void FromUsesRenderedMarkdownSidecarWhenPresent()
  {
    var row = PostRows.From(
        new Post("post-1", "post", "Title", "**Body**", "user-1", Html: null),
        null,
        null,
        null,
        new Dictionary<string, string> { ["post-1"] = "<p><strong>Body</strong></p>" });

    Assert.Equal("<p><strong>Body</strong></p>", row.BodyHtml);
  }
}
