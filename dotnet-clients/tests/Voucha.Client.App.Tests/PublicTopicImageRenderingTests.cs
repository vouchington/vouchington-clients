using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PublicTopicImageRenderingTests
{
  [Fact]
  public async Task TopicsRouteUsesPlacementDescriptorAndKeepsTitleWhenImageIsAbsent()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var locale = InstallPageResources();
    var withImage = TopicWithPlacement("topic-image", "Visible topic", new TopicImagePlacement("image-7", "placement-4", 3));
    var withoutImage = TopicWithPlacement("topic-plain", "Text-only topic", null, "legacy-image-id");
    var service = Service([withImage, withoutImage]);
    using var model = new TopicsViewModel(service);
    var config = new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test"));
    var api = new VouchaApiClient(new HttpClient { BaseAddress = config.ApiBaseUrl });
    var page = new TopicsPage(model, new AnonymousSessionStore(), null!, service, null!, api,
        Recovery(locale), null!, UiLocalization.English, locale, config);

    await model.LoadAsync(TestContext.Current.CancellationToken);

    var collection = Descendants<CollectionView>(page).Single();
    var binding = Assert.IsType<TopicsPageBinding>(page.BindingContext);
    var template = Assert.IsType<Border>(collection.ItemTemplate.CreateContent());
    var image = Assert.Single(Descendants<Image>(template));
    var name = Assert.Single(Descendants<Label>(template), label => label.AutomationId == "topic-name");

    var imageRow = Assert.Single(binding.Items, row => row.Id == withImage.Id);
    template.BindingContext = imageRow;
    Assert.True(image.IsVisible);
    Assert.Equal(new Uri("https://images.test/images/placements/placement-4/3/image-7?w=96"),
        Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.Equal(withImage.Name, name.Text);
    Assert.Equal(withImage.Name, SemanticProperties.GetDescription(image));

    var textRow = Assert.Single(binding.Items, row => row.Id == withoutImage.Id);
    template.BindingContext = textRow;
    Assert.False(image.IsVisible);
    Assert.Null(image.Source);
    Assert.Equal(withoutImage.Name, name.Text);
  }

  [Fact]
  public async Task TopicDetailPageRendersPlacementAndClearsItWhenReusedWithoutValidPlacement()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var locale = InstallPageResources();
    var config = new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test"));
    var withImage = TopicWithPlacement("topic-image", "Visible topic", new TopicImagePlacement("image-7", "placement-4", 3));
    var service = Service([withImage]);
    using var model = new TopicDetailViewModel(service);
    await model.LoadAsync(withImage.Id, TestContext.Current.CancellationToken);
    var page = DetailPage(model, withImage.Id, config, locale);

    var image = page.FindByName<Image>("TopicLogoImage");
    var title = Assert.Single(Descendants<Label>(page), label => label.Text == withImage.Name);
    Assert.True(image.IsVisible);
    Assert.Equal(144, image.WidthRequest);
    Assert.Equal(144, image.HeightRequest);
    Assert.Equal(new Uri("https://images.test/images/placements/placement-4/3/image-7?w=320"),
        Assert.IsType<UriImageSource>(image.Source).Uri);
    Assert.Equal(withImage.Name, title.Text);

    var withoutImage = TopicWithPlacement("topic-plain", "Text-only topic", null) with { LogoImageId = "legacy-image-id" };
    ((TopicServiceProxy)service).Topics = [withoutImage];
    await model.LoadAsync(withoutImage.Id, TestContext.Current.CancellationToken);
    Assert.False(image.IsVisible);
    Assert.Null(image.Source);
    Assert.Equal(withoutImage.Name, title.Text);

    ((TopicServiceProxy)service).Topics = [TopicWithPlacement(
        "topic-invalid", "Invalid placement", new TopicImagePlacement("image-9", "", 1), "legacy-image-id")];
    await model.LoadAsync("topic-invalid", TestContext.Current.CancellationToken);
    Assert.False(image.IsVisible);
    Assert.Null(image.Source);
    Assert.Equal("Invalid placement", title.Text);
  }

  private static TopicDetailPage DetailPage(
      TopicDetailViewModel model,
      string topicId,
      AppConfig config,
      IUiLocaleController locale) =>
      new(model, new AnonymousSessionStore(), topicId, Recovery(locale), config);

  private static IUiLocaleController InstallPageResources()
  {
    var locale = new UiLocaleController(new DeviceLanguage("en"));
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
      app.Resources[key] = new Style(typeof(Label));
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(locale);
    return locale;
  }

  private static EmailVerificationRecoveryCoordinator Recovery(IUiLocaleController locale) =>
      new(DispatchProxy.Create<IEmailAddressService, PublicEntityProvenanceRenderingTests.UnusedService>(),
          UiLocalization.English, locale);

  private static Topic TopicWithPlacement(
      string id,
      string name,
      TopicImagePlacement? placement,
      string? legacyImageId = null) =>
      new(id, name, id, "topic", LogoImageId: legacyImageId, LogoImagePlacement: placement);

  private static ITopicsService Service(IReadOnlyList<Topic> topics)
  {
    var service = DispatchProxy.Create<ITopicsService, TopicServiceProxy>();
    ((TopicServiceProxy)service).Topics = topics;
    return service;
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  public class TopicServiceProxy : DispatchProxy
  {
    public IReadOnlyList<Topic> Topics { get; set; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      if (targetMethod?.Name == nameof(ITopicsService.SearchAsync))
      {
        var topics = Topics.ToDictionary(topic => topic.Id, StringComparer.Ordinal);
        var references = Topics.Select(topic => new EntityReference(
            "topic", topic.Id, topic.Id, topic.Name, topic.Slug, topic.TopicType, null, null, null, null, null));
        return Task.FromResult(new TopicSearchResponse(
            references.ToArray(), new PageInfo(null, false, null), topics,
            new Dictionary<string, object>(StringComparer.Ordinal)));
      }
      if (targetMethod?.Name == nameof(ITopicsService.FetchTopicAsync))
      {
        var topicId = args?[0] as string;
        var topic = Topics.Single(candidate => candidate.Id == topicId);
        return Task.FromResult(new TopicResponse(topic));
      }
      if (targetMethod?.ReturnType == typeof(Task)) return Task.CompletedTask;
      throw new NotSupportedException($"Unexpected topic service call: {targetMethod?.Name}");
    }
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class DeviceLanguage(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
