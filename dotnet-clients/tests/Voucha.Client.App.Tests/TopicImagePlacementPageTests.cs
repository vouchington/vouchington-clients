using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class TopicImagePlacementPageTests
{
  [Fact]
  public void RenderedTopicImagesUseOnlyMatchingServerPlacements()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = 0,
        ["Metadata"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
      },
    };
    var config = new AppConfig(new Uri("https://api.test"), "site-key",
        ImageBaseUrl: new Uri("https://images.test"));
    var page = new TopicManagementPage(
        DispatchProxy.Create<ITopicsService, UnusedService>(),
        DispatchProxy.Create<IImageUploadService, UnusedService>(),
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }),
        config: config);
    var apply = typeof(TopicManagementPage).GetMethod("ApplyTopic", BindingFlags.Instance | BindingFlags.NonPublic)!;
    apply.Invoke(page, [new Topic("topic-1", "Topic", "topic", "topic",
        LogoImageId: "logo-1", HeroImageId: "hero-1",
        LogoImagePlacement: new TopicImagePlacement("logo-1", "placement-logo", 2),
        HeroImagePlacement: new TopicImagePlacement("hero-1", "placement-hero", 3))]);

    var logo = page.FindByName<Image>("LogoPersistedImage");
    var hero = page.FindByName<Image>("HeroPersistedImage");
    Assert.Equal(new Uri("https://images.test/images/placements/placement-logo/2/logo-1?w=480"),
        Assert.IsType<UriImageSource>(logo.Source).Uri);
    Assert.Equal(new Uri("https://images.test/images/placements/placement-hero/3/hero-1?w=480"),
        Assert.IsType<UriImageSource>(hero.Source).Uri);

    page.FindByName<Entry>("LogoImageEntry").Text = "other-image";
    Assert.False(logo.IsVisible);
    Assert.Null(logo.Source);
    Assert.True(hero.IsVisible);
  }

  [Fact]
  public void IdWithoutServerPlacementHasNoPersistedPreview()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = 0,
        ["Metadata"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
      },
    };
    var page = new TopicManagementPage(
        DispatchProxy.Create<ITopicsService, UnusedService>(),
        DispatchProxy.Create<IImageUploadService, UnusedService>(),
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }));
    var apply = typeof(TopicManagementPage).GetMethod("ApplyTopic", BindingFlags.Instance | BindingFlags.NonPublic)!;
    apply.Invoke(page, [new Topic("topic-1", "Topic", "topic", "topic",
        LogoImageId: "logo-1", HeroImageId: "hero-1")]);

    Assert.Null(page.FindByName<Image>("LogoPersistedImage").Source);
    Assert.Null(page.FindByName<Image>("HeroPersistedImage").Source);
  }

  [Fact]
  public async Task FailedLocalDecodePreservesSelectedBytes()
  {
    var bytes = new byte[] { 1, 2, 3, 4 };
    using var content = new MemoryStream(bytes);

    var selection = await LocalImagePreview.ReadSelectedBytesAsync(
        content, TestContext.Current.CancellationToken);

    Assert.False(selection.CanPreview);
    Assert.Equal(bytes, selection.Bytes);
  }

  [Fact]
  public async Task HeldLogoAndHeroUploadsKeepIndependentPreviewState()
  {
    var uploads = new HeldTopicUploadService();
    var page = CreateUploadPage(uploads);
    using var logoBytes = new MemoryStream([1, 2, 3]);
    using var heroBytes = new MemoryStream([4, 5, 6]);
    var logo = page.UploadSelectedImageAsync(true, logoBytes, "image/png");
    Task? hero = null;
    try
    {
      await uploads.LogoStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      hero = page.UploadSelectedImageAsync(false, heroBytes, "image/png");
      await uploads.HeroStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.False(page.FindByName<Label>("LogoPreviewUnavailableLabel").IsVisible);
      Assert.False(page.FindByName<Label>("HeroPreviewUnavailableLabel").IsVisible);
      uploads.ReleaseHero.TrySetResult(true);
      await hero;
    }
    finally
    {
      uploads.ReleaseHero.TrySetResult(true);
      uploads.ReleaseLogo.TrySetResult(true);
      await Task.WhenAll(logo, hero ?? Task.CompletedTask);
    }
    Assert.Equal("image-2", page.FindByName<Entry>("HeroImageEntry").Text);
    Assert.True(page.FindByName<Label>("HeroPreviewUnavailableLabel").IsVisible);
    Assert.Equal("image-1", page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.True(page.FindByName<Label>("LogoPreviewUnavailableLabel").IsVisible);
  }

  [Fact]
  public async Task ManualReplacementRejectsHeldLogoUploadResponse()
  {
    var uploads = new HeldTopicUploadService();
    var page = CreateUploadPage(uploads);
    using var logoBytes = new MemoryStream([1, 2, 3]);
    var logo = page.UploadSelectedImageAsync(true, logoBytes, "image/png");
    try
    {
      await uploads.LogoStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      page.FindByName<Entry>("LogoImageEntry").Text = "manual-id";
    }
    finally
    {
      uploads.ReleaseLogo.TrySetResult(true);
      await logo;
    }

    Assert.Equal("manual-id", page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.False(page.FindByName<Label>("LogoPreviewUnavailableLabel").IsVisible);
  }

  [Fact]
  public async Task NavigationRejectsHeldLogoUploadResponse()
  {
    var uploads = new HeldTopicUploadService();
    var page = CreateUploadPage(uploads);
    using var logoBytes = new MemoryStream([1, 2, 3]);
    var logo = page.UploadSelectedImageAsync(true, logoBytes, "image/png");
    try
    {
      await uploads.LogoStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      typeof(TopicManagementPage).GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, null);
    }
    finally
    {
      uploads.ReleaseLogo.TrySetResult(true);
      await logo;
    }

    Assert.Null(page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.False(page.FindByName<Label>("LogoPreviewUnavailableLabel").IsVisible);
  }

  private static TopicManagementPage CreateUploadPage(IImageUploadService uploads)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = 0,
        ["Metadata"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
      },
    };
    return new TopicManagementPage(
        DispatchProxy.Create<ITopicsService, UnusedService>(),
        uploads,
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }));
  }

  private sealed class HeldTopicUploadService : IImageUploadService
  {
    private int created;
    public TaskCompletionSource<bool> LogoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> HeroStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseLogo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseHero { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
        CreateImageUploadUrlBody body, CancellationToken cancellationToken = default)
    {
      var id = $"image-{Interlocked.Increment(ref created)}";
      return Task.FromResult(new ImageUploadUrlResponse(new ImageUploadUrl(
          id, $"https://upload.test/{id}", body.ContentType, DateTimeOffset.UtcNow)));
    }

    public Task UploadAsync(ImageUploadUrl upload, Stream content, long contentLength,
        CancellationToken cancellationToken = default)
    {
      if (upload.ImageId == "image-1")
      {
        LogoStarted.TrySetResult(true);
        return ReleaseLogo.Task;
      }
      HeroStarted.TrySetResult(true);
      return ReleaseHero.Task;
    }

    public Task<CompleteImageUploadResponse> CompleteAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompleteImageUploadResponse(new ImageUpload(imageId, "complete")));

    public Task<ImageUploadStateResponse> FetchUploadStateAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  public class UnusedService : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
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
