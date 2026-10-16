using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class PublicEntityProvenanceRenderingTests
{
  [Fact]
  public void ProfileTopicCardRelocalizesVisibleProvenanceOnLocaleChange()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    InstallPageStyles();
    Application.Current!.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var copy = UiCopy.PushLocalization(localization);
    using var model = new ProfileViewModel(
        Proxy<ISettingsService>(), Proxy<ILandingPagesService>(), Proxy<IPostsService>(),
        Proxy<IImageUploadService>(), new AppConfig(new Uri("https://api.test")),
        localization, controller);
    var row = new TopicRow("topic-1", "Topic", "topic", "topic", UiText.ProtocolValue("topic"), null,
        Localization: localization,
        Provenance: new PublicContentProvenance("mcp", null),
        StaffProvenance: new StaffContentProvenance("mcp"),
        IsStaffViewer: true);
    typeof(ProfileViewModel).GetProperty(nameof(ProfileViewModel.TopicItems))!.SetValue(model, new[] { row });
    var page = new ProfilePage { BindingContext = model };
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<TopicRow>);
    var card = Assert.IsType<VerticalStackLayout>(collection.ItemTemplate.CreateContent());
    card.BindingContext = row;
    var labels = card.Children.OfType<Label>().ToArray();
    var englishPublic = labels[0].Text;
    var englishStaff = labels[1].Text;
    var originalItems = collection.ItemsSource;

    controller.ApplySavedLocale("es");

    Assert.NotSame(originalItems, collection.ItemsSource);
    card.BindingContext = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TopicRow>>(collection.ItemsSource));
    Assert.NotEqual(englishPublic, labels[0].Text);
    Assert.NotEqual(englishStaff, labels[1].Text);
    Assert.True(labels[0].IsVisible);
    Assert.True(labels[1].IsVisible);
  }

  private static T Proxy<T>() where T : class => DispatchProxy.Create<T, NoCallProxy>();

  public class NoCallProxy : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
  }

}
