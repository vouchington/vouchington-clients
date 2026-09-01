using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Xunit;

namespace Voucha.Client.Core.Tests.Content;

public sealed class UrlEmbedPreviewTests
{
  [Fact]
  public void SelectsNormalizedThenOgAndPreservesRawTags()
  {
    var tags = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
      {"OG:TITLE":"OG","twitter:title":"Twitter","og:description":"OG description","nested":{"raw":true},"og:image":"https://third-party.example/image.jpg"}
      """)!;
    var preview = UrlEmbedPreviews.From(new UrlEmbed(
        Title: "Normalized",
        ThumbnailUrl: "https://images.voucha.ai/safe.jpg",
        EmbedMetadata: new ResolvedEmbed("article", "https://source.example", "https://source.example", Title: "oEmbed", Description: "oEmbed description", Provider: new EmbedProviderMetadata(Name: "Provider")),
        MetaTags: tags));

    Assert.Equal("oEmbed", preview.Title);
    Assert.Equal("oEmbed description", preview.Description);
    Assert.Equal("Provider", preview.Provider);
    Assert.Equal("https://images.voucha.ai/safe.jpg", preview.ThumbnailUrl?.AbsoluteUri);
    Assert.Equal(JsonValueKind.Object, tags["nested"].ValueKind);
  }

  [Fact]
  public void DoesNotUseThirdPartyMetadataImages()
  {
    var tags = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{" + "\"og:image\":\"https://third-party.example/image.jpg\"}")!;
    var preview = UrlEmbedPreviews.From(new UrlEmbed(MetaTags: tags));
    Assert.Null(preview.ThumbnailUrl);
  }

  [Fact]
  public void UsesSourceHostForProviderAndKeepsActionOnlyPreviewsVisible()
  {
    var preview = UrlEmbedPreviews.From(new UrlEmbed(
        SourceUrl: "https://source.example/article",
        PlayerUrl: "https://player.vimeo.com/video/123"));

    Assert.Equal("source.example", preview.Provider);
    Assert.True(preview.HasPreview);
    Assert.True(preview.HasSource);
    Assert.True(preview.CanPlay);
  }

  [Fact]
  public void UsesTwitterBeforeFlattenedFieldsButNotMarkdown()
  {
    var tags = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{" +
        "\"twitter:title\":\"Twitter\",\"twitter:description\":\"Twitter description\"}")!;
    var preview = UrlEmbedPreviews.From(new UrlEmbed(Title: "Flattened", Markdown: "Markdown", MetaTags: tags));

    Assert.Equal("Twitter", preview.Title);
    Assert.Equal("Twitter description", preview.Description);
  }

  [Fact]
  public void DoesNotAllowPlayerWithoutAValidHttpsSource()
  {
    var withoutSource = UrlEmbedPreviews.From(new UrlEmbed(PlayerUrl: "https://player.vimeo.com/video/123"));
    var invalidSource = UrlEmbedPreviews.From(new UrlEmbed(
        PlayerUrl: "https://player.vimeo.com/video/123",
        SourceUrl: "http://source.example/article"));

    Assert.False(withoutSource.CanPlay);
    Assert.False(invalidSource.CanPlay);
    Assert.True(withoutSource.HasPreview);
  }

  [Fact]
  public void DecodesFractionalNestedEmbedDimensions()
  {
    var embed = JsonSerializer.Deserialize<UrlEmbed>("""
      {"embed_metadata":{"kind":"video","requestedUrl":"https://source.example","resolvedUrl":"https://source.example","player":{"url":"https://player.vimeo.com/video/123","width":640.5,"height":360.25},"thumbnail":{"url":"https://images.voucha.ai/safe.jpg","width":1280.5,"height":720.25}}}
      """)!;

    Assert.Equal(640.5, embed.EmbedMetadata?.Player?.Width);
    Assert.Equal(720.25, embed.EmbedMetadata?.Thumbnail?.Height);
  }

  [Theory]
  [InlineData("https://www.youtube-nocookie.com/embed/video-id", true)]
  [InlineData("https://player.vimeo.com/video/123", true)]
  [InlineData("https://www.youtube-nocookie.com:443/embed/video-id", false)]
  [InlineData("https://user@www.youtube-nocookie.com/embed/video-id", false)]
  [InlineData("https://www.youtube.com/embed/video-id", false)]
  [InlineData("http://player.vimeo.com/video/123", false)]
  public void AllowsOnlyExactApprovedPlayers(string url, bool expected)
  {
    Assert.Equal(expected, UrlEmbedPreviews.IsApprovedPlayer(new Uri(url)));
  }
}
