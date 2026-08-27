using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class CommentThreadLocalizationTests
{
  [Theory]
  [InlineData("en", "blog_post", "Blog post")]
  [InlineData("es", "blog_post", "Publicación de blog")]
  [InlineData("fr", "blog_post", "Article de blog")]
  [InlineData("pt", "blog_post", "Publicação de blogue")]
  [InlineData("en", "data_point", "Data point")]
  [InlineData("es", "data_point", "Punto de datos")]
  [InlineData("fr", "data_point", "Point de données")]
  [InlineData("pt", "data_point", "Ponto de dados")]
  [InlineData("en", "topic_recommendation", "Topic recommendation")]
  [InlineData("es", "topic_recommendation", "Recomendación de tema")]
  [InlineData("fr", "topic_recommendation", "Recommandation de sujet")]
  [InlineData("pt", "topic_recommendation", "Recomendação de tópico")]
  public void TitlelessRowsLocalizePostTypeWithoutLosingProtocolValue(
      string locale,
      string protocolPostType,
      string expectedTitle)
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider(locale));
    var row = new CommentThreadRow(
        Post(protocolPostType, title: null),
        0,
        false,
        false,
        new UiLocalization(controller));

    Assert.Equal(protocolPostType, row.ProtocolPostType);
    Assert.Equal(expectedTitle, row.LocalizedTitle);
  }

  [Fact]
  public void TitlelessRowRefreshesThroughLiveLocalization()
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var row = new CommentThreadRow(
        Post("data_point", title: ""),
        0,
        false,
        false,
        new UiLocalization(controller));
    Assert.Equal("Data point", row.LocalizedTitle);

    controller.ApplySavedLocale("fr");

    Assert.Equal("Point de données", row.LocalizedTitle);
  }

  private static Post Post(string postType, string? title) =>
      new(
          "post-1",
          postType,
          title,
          null,
          "user-1");

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
