using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostRowsTests
{
  [Fact]
  public void ProvenanceLabelsExposeOnlyTrustedAppNames()
  {
    var channel = PublicProvenanceLabels.Resolve(new("mcp", null), UiLocalization.English);
    var known = PublicProvenanceLabels.Resolve(
        new("mcp", new PublicProvenanceApp("known", Key: "private-key")), UiLocalization.English);
    var verified = PublicProvenanceLabels.Resolve(
        new("api", new PublicProvenanceApp("verified", ClientId: "client-1", ClientName: "Example App")),
        UiLocalization.English);
    var hostname = PublicProvenanceLabels.Resolve(
        new("api", new PublicProvenanceApp("hostname", Hostname: "example.test")), UiLocalization.English);

    Assert.Equal(channel, known);
    Assert.DoesNotContain("private-key", known ?? string.Empty, StringComparison.Ordinal);
    Assert.DoesNotContain("client-1", verified ?? string.Empty, StringComparison.Ordinal);
    Assert.Contains("Example App", verified ?? string.Empty, StringComparison.Ordinal);
    Assert.Contains("example.test", hostname ?? string.Empty, StringComparison.Ordinal);
  }

  [Fact]
  public void FromCarriesOnlyPublicProvenanceFacts()
  {
    var labeled = PostRows.From(
        new Post("post-1", "discussion", "Title", null, "user-1",
            Provenance: new PublicContentProvenance("api", null)),
        null, null, null);
    var unlabeled = PostRows.From(
        new Post("post-2", "story", "Title", null, "user-1"),
        null, null, null);

    Assert.Equal("api", labeled.Provenance?.Via);
    Assert.True(labeled.HasProvenance);
    Assert.Null(unlabeled.Provenance);
    Assert.False(unlabeled.HasProvenance);
  }

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

  [Fact]
  public void AccountTypeLabelIsVisibleOnlyForIdentifiedActiveAuthors()
  {
    var author = new User("user-1", "alice", AccountType: AccountType.Official);
    var visible = new Post("post-1", "discussion", "Title", null, "user-1", CreatedBy: author);
    var anonymous = visible with { IsAnonymous = true };
    var deleted = visible with { DeletedAt = DateTimeOffset.UtcNow };

    Assert.NotNull(PostRows.From(visible, null, null, null).AuthorAccountTypeLabel);
    Assert.Null(PostRows.From(anonymous, null, null, null).AuthorAccountTypeLabel);
    Assert.Null(PostRows.From(deleted, null, null, null).AuthorAccountTypeLabel);
    Assert.NotNull(new CommentThreadRow(visible, 0, false, false).AuthorAccountTypeLabel);
    Assert.Null(new CommentThreadRow(anonymous, 0, false, false).AuthorAccountTypeLabel);
    Assert.Null(new CommentThreadRow(deleted, 0, false, false).AuthorAccountTypeLabel);
  }
}
