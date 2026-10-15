using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ProfileTopicImageRenderingTests
{
  [Fact]
  public void ProfileTopicRowUsesCanonicalPlacementAndKeepsFallbackAndNavigation()
  {
    InstallPageResources();
    var page = new ProfilePage();
    var collection = Assert.Single(Descendants<CollectionView>(page), view =>
        view.AutomationId == "profile-topic-collection");
    var row = Assert.IsType<VerticalStackLayout>(collection.ItemTemplate.CreateContent());
    var placement = new TopicImagePlacement("image-1", "placement-1", 3);
    var topic = TopicRow(placement);
    row.BindingContext = topic;

    var image = Assert.Single(Descendants<Image>(row));
    var navigation = Assert.Single(Descendants<Button>(row), button =>
        button.AutomationId == "public-profile-topic-navigation");
    var logoButton = Assert.Single(Descendants<Button>(row), button =>
        button.AutomationId == "public-profile-topic-logo-button");
    Assert.Equal(new Uri("https://images.test/images/placements/placement-1/3/image-1?w=96"),
        Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.Equal("Following topic", navigation.Text);
    Assert.Same(topic, navigation.CommandParameter);
    Assert.Same(topic, logoButton.CommandParameter);

    var provenanceLabels = row.Children.OfType<Label>().ToArray();
    Assert.Equal(2, provenanceLabels.Length);
    row.BindingContext = TopicRow(placement, includeProvenance: true);
    Assert.True(provenanceLabels[0].IsVisible);
    Assert.False(provenanceLabels[1].IsVisible);
    row.BindingContext = TopicRow(placement, includeProvenance: true, isStaffViewer: true);
    Assert.True(provenanceLabels[0].IsVisible);
    Assert.True(provenanceLabels[1].IsVisible);

    row.BindingContext = TopicRow(null);

    Assert.Null(image.Source);
    Assert.Equal("Following topic", navigation.Text);
  }

  private static TopicRow TopicRow(TopicImagePlacement? placement, bool includeProvenance = false, bool isStaffViewer = false) => new(
      "topic-1", "Following topic", "following-topic", "topic", UiTaxonomy.TopicType("topic"), null,
      Localization: UiLocalization.English,
      Provenance: includeProvenance ? new PublicContentProvenance("mcp", null) : null,
      StaffProvenance: includeProvenance ? new StaffContentProvenance("mcp") : null,
      IsStaffViewer: isStaffViewer,
      LogoImagePlacement: placement);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static void InstallPageResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new DeviceLanguage()));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class DeviceLanguage : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }
}
