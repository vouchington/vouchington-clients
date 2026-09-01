using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Xunit;

namespace Voucha.Client.Core.Tests.Content;

public sealed class UrlEmbedPreviewTests
{
  [Fact]
  public void SelectsSafeProjectionsThenRawTagsAndPreservesRawData()
  {
    var tags = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
      {"OG:TITLE":"OG","twitter:title":"Twitter","og:description":"OG description","nested":{"raw":true},"og:image":"https://third-party.example/image.jpg"}
      """)!;
    var preview = UrlEmbedPreviews.From(new UrlEmbed(
        Title: "Normalized",
        Description: "Safe description",
        ProviderName: "Safe provider",
        ThumbnailUrl: "https://images.voucha.ai/safe.jpg",
        MetaTags: tags));

    Assert.Equal("Normalized", preview.Title);
    Assert.Equal("Safe description", preview.Description);
    Assert.Equal("Safe provider", preview.Provider);
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
  public void UsesSafeTitleBeforeRawTwitterTitleButNotMarkdown()
  {
    var tags = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{" +
        "\"twitter:title\":\"Twitter\",\"twitter:description\":\"Twitter description\"}")!;
    var preview = UrlEmbedPreviews.From(new UrlEmbed(Title: "Flattened", Markdown: "Markdown", MetaTags: tags));

    Assert.Equal("Flattened", preview.Title);
    Assert.Equal("Twitter description", preview.Description);
  }

  [Fact]
  public void TrimsSelectedValuesAndSkipsWhitespaceOnlyProjections()
  {
    var tags = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{" +
        "\"og:title\":\"  OG title  \",\"twitter:title\":\"Twitter title\"}")!;
    var preview = UrlEmbedPreviews.From(new UrlEmbed(
        Title: "  ",
        Description: "  Safe description  ",
        ProviderName: "\tSafe provider\t",
        MetaTags: tags));

    Assert.Equal("OG title", preview.Title);
    Assert.Equal("Safe description", preview.Description);
    Assert.Equal("Safe provider", preview.Provider);
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

  [Theory]
  [InlineData("https:")]
  [InlineData("https:/hostless")]
  [InlineData("https:///hostless")]
  public void DoesNotUseMalformedHostlessHttpsSources(string value)
  {
    var preview = UrlEmbedPreviews.From(new UrlEmbed(SourceUrl: value));

    Assert.Null(preview.SourceUrl);
    Assert.False(preview.HasSource);
  }

  [Fact]
  public void PreservesUnknownNestedEmbedMetadata()
  {
    var embed = JsonSerializer.Deserialize<UrlEmbed>("""
      {"embed_metadata":{"provider":{"key":"vimeo","future":{"enabled":true}},"player":{"width":640.5},"unknown":[1,false]},"meta_tags":{"nested":{"raw":true},"array":[1,"two"]}}
      """)!;

    var metadata = embed.EmbedMetadata!.Value;
    Assert.Equal(JsonValueKind.Object, metadata.ValueKind);
    Assert.True(metadata.GetProperty("provider").GetProperty("future").GetProperty("enabled").GetBoolean());
    Assert.Equal(640.5, metadata.GetProperty("player").GetProperty("width").GetDouble());
    Assert.Equal(JsonValueKind.Array, metadata.GetProperty("unknown").ValueKind);
    Assert.Equal(JsonValueKind.Object, embed.MetaTags!["nested"].ValueKind);
    Assert.Equal(JsonValueKind.Array, embed.MetaTags["array"].ValueKind);
  }

  [Fact]
  public void DecodesBackendSafeDescriptionAndProviderName()
  {
    var embed = JsonSerializer.Deserialize<UrlEmbed>("""
      {"description":"Safe description","provider_name":"Safe provider"}
      """)!;

    Assert.Equal("Safe description", embed.Description);
    Assert.Equal("Safe provider", embed.ProviderName);
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
