using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class TopicLogoLocalPreviewTests
{
  private static readonly byte[] TinyPng = Convert.FromBase64String(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

  [Fact]
  public async Task SelectedLogoBytesRenderWhileUploadIsHeld()
  {
    var topic = FixtureTopic();
    var uploads = new HeldLogoUpload();
    var page = CreatePage(uploads);
    Apply(page, topic);
    var logoPlacement = Assert.IsType<UriImageSource>(page.FindByName<Image>("LogoPersistedImage").Source).Uri;
    Assert.Equal(PlacementUrl(topic.LogoImagePlacement!), logoPlacement);

    page.readSelectedLogoPreview = async (stream, cancellation) =>
    {
      using var buffer = new MemoryStream();
      await stream.CopyToAsync(buffer, cancellation);
      return new LocalImageSelection(buffer.ToArray(), TinyPng);
    };
    using var selected = new MemoryStream(TinyPng);
    var upload = page.UploadSelectedImageAsync(true, selected, "image/png");
    try
    {
      await uploads.LogoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
      var local = page.FindByName<Image>("LogoLocalPreviewImage");
      Assert.True(local.IsVisible);
      var source = Assert.IsType<StreamImageSource>(local.Source);
      await using var rendered = await source.Stream(TestContext.Current.CancellationToken);
      using var renderedBytes = new MemoryStream();
      await rendered.CopyToAsync(renderedBytes, TestContext.Current.CancellationToken);
      Assert.Equal(TinyPng, renderedBytes.ToArray());
      Assert.False(page.FindByName<Label>("LogoPreviewUnavailableLabel").IsVisible);
      Assert.Equal(logoPlacement, Assert.IsType<UriImageSource>(page.FindByName<Image>("LogoPersistedImage").Source).Uri);
      Assert.Equal(HeroUrl(topic), Assert.IsType<UriImageSource>(page.FindByName<Image>("HeroPersistedImage").Source).Uri);
    }
    finally
    {
      uploads.ReleaseLogo.TrySetResult(true);
      await upload.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    Assert.Equal("canonical-logo", page.FindByName<Entry>("LogoImageEntry").Text);
    var completed = page.FindByName<Image>("LogoLocalPreviewImage");
    Assert.True(completed.IsVisible);
    Assert.IsType<StreamImageSource>(completed.Source);
    Assert.False(page.FindByName<Image>("LogoPersistedImage").IsVisible);
    Assert.False(page.FindByName<Label>("LogoPreviewUnavailableLabel").IsVisible);
    Assert.Equal(HeroUrl(topic), Assert.IsType<UriImageSource>(page.FindByName<Image>("HeroPersistedImage").Source).Uri);
  }

  [Fact]
  public async Task UndecodableLogoShowsAccessibleFallbackInsteadOfFixturePlacement()
  {
    var topic = FixtureTopic();
    var page = CreatePage(new HeldLogoUpload(hold: false));
    Apply(page, topic);
    Assert.Equal(PlacementUrl(topic.LogoImagePlacement!),
        Assert.IsType<UriImageSource>(page.FindByName<Image>("LogoPersistedImage").Source).Uri);

    using var selected = new MemoryStream([1, 2, 3, 4]);
    await page.UploadSelectedImageAsync(true, selected, "image/png")
        .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    var unavailable = page.FindByName<Label>("LogoPreviewUnavailableLabel");
    Assert.True(unavailable.IsVisible);
    Assert.Equal(PreviewUnavailableCopy(), unavailable.Text);
    Assert.Equal(unavailable.Text, SemanticProperties.GetDescription(unavailable));
    Assert.False(page.FindByName<Image>("LogoLocalPreviewImage").IsVisible);
    Assert.False(page.FindByName<Image>("LogoPersistedImage").IsVisible);
    Assert.Equal("canonical-logo", page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.NotEqual(topic.LogoImageId, page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.False(page.FindByName<Label>("HeroPreviewUnavailableLabel").IsVisible);
    Assert.Equal(HeroUrl(topic), Assert.IsType<UriImageSource>(page.FindByName<Image>("HeroPersistedImage").Source).Uri);
  }

  private static Topic FixtureTopic()
  {
    var manifest = JsonNode.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("manifest.json")))!;
    var path = manifest["fixtures"]!.AsArray()
        .Single(entry => (string?)entry?["id"] == "web.topics.show.default")!["bodyFile"]!.GetValue<string>();
    var response = JsonSerializer.Deserialize<TopicResponse>(
        File.ReadAllText(FilamentsContractPaths.ApiFixture(path)),
        VouchaApiJson.Options)!;
    var logo = response.Topic.LogoImagePlacement!;
    Assert.Equal("00000000-0000-7000-8000-000000000306", logo.PlacementId);
    Assert.Equal(3, logo.PlacementRevision);
    Assert.Equal("00000000-0000-7000-8000-000000000205", logo.ImageId);
    var hero = response.Topic.HeroImagePlacement!;
    Assert.Equal("00000000-0000-7000-8000-000000000307", hero.PlacementId);
    Assert.Equal(4, hero.PlacementRevision);
    Assert.Equal("00000000-0000-7000-8000-000000000206", hero.ImageId);
    return response.Topic;
  }

  private static TopicManagementPage CreatePage(IImageUploadService uploads)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = 0,
        ["Metadata"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        [UiMessageKey.ImagesUploadPreviewUnavailable.Value] = PreviewUnavailableCopy(),
      },
    };
    return new TopicManagementPage(
        DispatchProxy.Create<ITopicsService, TopicImagePlacementPageTests.UnusedService>(),
        uploads,
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }),
        config: PageConfig());
  }

  private static void Apply(TopicManagementPage page, Topic topic) =>
      typeof(TopicManagementPage).GetMethod("ApplyTopic", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, [topic]);

  private static AppConfig PageConfig() =>
      new(new Uri("https://api.test"), "site-key", ImageBaseUrl: new Uri("https://images.test"));

  private static Uri PlacementUrl(TopicImagePlacement placement) =>
      PageConfig().ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 480)!;

  private static Uri HeroUrl(Topic topic) => PlacementUrl(topic.HeroImagePlacement!);

  private static string PreviewUnavailableCopy() =>
      UiLocalization.English.Localize(UiMessageKey.ImagesUploadPreviewUnavailable);

  private sealed class HeldLogoUpload(bool hold = true) : IImageUploadService
  {
    public TaskCompletionSource<bool> LogoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseLogo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
        CreateImageUploadUrlBody body, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImageUploadUrlResponse(new ImageUploadUrl(
            "image-1", "https://upload.test/image-1", body.ContentType, DateTimeOffset.UtcNow)));

    public Task UploadAsync(
        ImageUploadUrl upload, Stream content, long contentLength, CancellationToken cancellationToken = default)
    {
      LogoStarted.TrySetResult(true);
      return hold ? ReleaseLogo.Task : Task.CompletedTask;
    }

    public Task<CompleteImageUploadResponse> CompleteAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompleteImageUploadResponse(new ImageUpload(imageId, "complete")));

    public Task<ImageUploadStateResponse> FetchUploadStateAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImageUploadStateResponse(
            new ImageUploadState("canonical-logo", "complete", null, Ready: true, Blocked: false)));
  }
}
