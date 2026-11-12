using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Theory]
  [InlineData("web.topics.show.viewer-lower-bound", "Posts (0+)", "Reviews (0)", "Data Points (0)")]
  [InlineData("web.topics.show.viewer-review-data-lower-bound", "Posts (0+)", "Reviews (0+)", "Data Points (0+)")]
  public async Task TopicCategoryCountsRenderIndependentBoundsAndRefreshLocale(string fixture, string posts, string reviews, string data)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var model = new TopicDetailViewModel(new ApiTopicsService(Client(Fixture(fixture))),
        localization: new UiLocalization(locale), localeController: locale);
    var page = new TopicDetailPage(model, new AnonymousSessionStore(), "test-topic", Recovery(locale),
        new AppConfig(new Uri("https://api.test")));
    await model.LoadAsync("test-topic", followSource: false, TestContext.Current.CancellationToken);
    var labels = VisualTreeElements.Descendants<Label>(page).Where(label =>
        label.AutomationId?.StartsWith("topic-", StringComparison.Ordinal) == true).ToDictionary(label => label.AutomationId);
    Assert.Equal(posts, labels["topic-posts-count"].Text);
    Assert.Equal(reviews, labels["topic-reviews-count"].Text);
    Assert.Equal(data, labels["topic-data-points-count"].Text);
    locale.ApplySavedLocale("es");
    Assert.NotEqual(posts, labels["topic-posts-count"].Text);
    Assert.Equal(model.PostsCountLabel, labels["topic-posts-count"].Text);
    Assert.Equal(model.ReviewsCountLabel, labels["topic-reviews-count"].Text);
    Assert.Equal(model.DataPointsCountLabel, labels["topic-data-points-count"].Text);
  }
}
