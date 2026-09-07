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

  [Fact]
  public void FromUsesTheServerProvidedLinkEmbed()
  {
    var row = PostRows.From(
        new Post("post-1", "link", "Title", null, "user-1"),
        null,
        null,
        null,
        new Dictionary<string, UrlEmbed>
        {
          ["post-1"] = new(Title: "Provider title", ThumbnailUrl: "https://cdn.example/thumbnail.jpg"),
        });

    Assert.Equal("Provider title", row.EmbedPreview?.Title);
    Assert.Equal(new Uri("https://cdn.example/thumbnail.jpg"), row.EmbedPreview?.ThumbnailUrl);
  }

  [Fact]
  public void FromNormalizesAuthoredTitlesBeforeApplyingTheirLanguage()
  {
    var authored = PostRows.From(
        new Post("post-1", "discussion", "  عنوان  ", null, "user-1", DeclaredLanguage: "ar"),
        null, null, null);
    var fallback = PostRows.From(
        new Post("post-2", "discussion", "  ", null, "user-1", DeclaredLanguage: "ar"),
        null, null, null);

    Assert.Equal("عنوان", authored.Title);
    Assert.Equal("RightToLeft", authored.TitleFlowDirection);
    Assert.Null(fallback.TitleFlowDirection);
  }
}
