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
      Assert.False(page.FindByName<Button>("SaveButton").IsEnabled);
      Assert.False(page.FindByName<Button>("UploadLogoButton").IsEnabled);
      Assert.True(page.FindByName<Button>("UploadHeroButton").IsEnabled);
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
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);
  }

  [Fact]
  public async Task ReadyStateUsesServerCanonicalImageId()
  {
    var uploads = new HeldTopicUploadService { LogoState = HeldTopicUploadService.ReadyState("canonical-logo") };
    var page = CreateUploadPage(uploads);
    using var bytes = new MemoryStream([1, 2, 3]);
    var upload = page.UploadSelectedImageAsync(true, bytes, "image/png");
    uploads.ReleaseLogo.TrySetResult(true);
    await upload;

    Assert.Equal("canonical-logo", page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);
  }

  [Fact]
  public async Task SameFieldReentryCannotReleaseSaveWhileCancelledUploadIsStillHeld()
  {
    var uploads = new HeldTopicUploadService();
    var page = CreateUploadPage(uploads);
    using var originalBytes = new MemoryStream([1, 2, 3]);
    using var replacementBytes = new MemoryStream([4, 5, 6]);
    var original = page.UploadSelectedImageAsync(true, originalBytes, "image/png");
    try
    {
      await uploads.LogoStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      await page.UploadSelectedImageAsync(true, replacementBytes, "image/png");
      Assert.Equal(1, uploads.CreatedCount);
      page.FindByName<Entry>("LogoImageEntry").Text = "manual-id";
      Assert.True(uploads.LogoPutToken.IsCancellationRequested);
      Assert.False(page.FindByName<Button>("SaveButton").IsEnabled);
      Assert.False(page.FindByName<Button>("UploadLogoButton").IsEnabled);
    }
    finally
    {
      uploads.ReleaseLogo.TrySetResult(true);
      await original;
    }
    Assert.Equal("manual-id", page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);
    Assert.True(page.FindByName<Button>("UploadLogoButton").IsEnabled);
  }

  [Fact]
  public async Task UploadCannotBeginWhileSaveIsInFlight()
  {
    var uploads = new HeldTopicUploadService();
    var topics = DispatchProxy.Create<ITopicsService, HeldSaveTopicService>();
    var page = CreateUploadPage(uploads, topics);
    var save = page.SaveTopicAsync();
    try
    {
      await ((HeldSaveTopicService)topics).Started.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.False(page.FindByName<Button>("SaveButton").IsEnabled);
      Assert.False(page.FindByName<Button>("UploadLogoButton").IsEnabled);
      Assert.False(page.FindByName<Button>("UploadHeroButton").IsEnabled);
      using var bytes = new MemoryStream([1, 2, 3]);
      await page.UploadSelectedImageAsync(true, bytes, "image/png");
      Assert.Equal(0, uploads.CreatedCount);
    }
    finally
    {
      ((HeldSaveTopicService)topics).ReleaseCreate.TrySetException(new InvalidOperationException("save failed"));
      await save;
    }
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);
    Assert.True(page.FindByName<Button>("UploadLogoButton").IsEnabled);
  }

  [Fact]
  public async Task HeldReadyCheckKeepsSaveDisabledAndImageIdUnavailable()
  {
    var uploads = new HeldTopicUploadService { HoldLogoState = true };
    var topics = DispatchProxy.Create<ITopicsService, CountingTopicService>();
    var page = CreateUploadPage(uploads, topics);
    using var bytes = new MemoryStream([1, 2, 3]);
    var upload = page.UploadSelectedImageAsync(true, bytes, "image/png");
    try
    {
      await uploads.LogoStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      uploads.ReleaseLogo.TrySetResult(true);
      await uploads.LogoStateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.False(page.FindByName<Button>("SaveButton").IsEnabled);
      Assert.Null(page.FindByName<Entry>("LogoImageEntry").Text);
      typeof(TopicManagementPage).GetMethod("OnSaveClicked", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, [null, EventArgs.Empty]);
      Assert.Equal(0, ((CountingTopicService)topics).Calls);
    }
    finally
    {
      uploads.ReleaseLogo.TrySetResult(true);
      uploads.ReleaseLogoState.TrySetResult(HeldTopicUploadService.ReadyState("image-1"));
      await upload;
    }
    Assert.Equal("image-1", page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);
  }

  [Theory]
  [InlineData("failed", false)]
  [InlineData("complete", true)]
  public async Task TerminalOrBlockedImageIsNeverAssigned(string status, bool blocked)
  {
    var uploads = new HeldTopicUploadService
    {
      LogoState = new ImageUploadState("image-1", status, "Upload rejected", Ready: false, Blocked: blocked),
    };
    var page = CreateUploadPage(uploads);
    using var bytes = new MemoryStream([1, 2, 3]);
    var upload = page.UploadSelectedImageAsync(true, bytes, "image/png");
    uploads.ReleaseLogo.TrySetResult(true);
    await upload;

    Assert.Null(page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.Equal("Upload rejected", page.FindByName<Label>("StatusLabel").Text);
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);
  }

  [Fact]
  public async Task NavigationCancelsHeldReadyCheckWithoutReleasingSaveEarly()
  {
    var uploads = new HeldTopicUploadService { HoldLogoState = true };
    var page = CreateUploadPage(uploads);
    using var bytes = new MemoryStream([1, 2, 3]);
    var upload = page.UploadSelectedImageAsync(true, bytes, "image/png");
    try
    {
      await uploads.LogoStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      uploads.ReleaseLogo.TrySetResult(true);
      await uploads.LogoStateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      typeof(TopicManagementPage).GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, null);
      Assert.True(uploads.LogoPollToken.IsCancellationRequested);
      Assert.False(page.FindByName<Button>("SaveButton").IsEnabled);
    }
    finally
    {
      uploads.ReleaseLogo.TrySetResult(true);
      uploads.ReleaseLogoState.TrySetResult(HeldTopicUploadService.ReadyState("image-1"));
      await upload;
    }
    Assert.Null(page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);
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

  private static TopicManagementPage CreateUploadPage(IImageUploadService uploads, ITopicsService? topics = null)
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
        topics ?? DispatchProxy.Create<ITopicsService, UnusedService>(),
        uploads,
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }));
  }

  private sealed class HeldTopicUploadService : IImageUploadService
  {
    private int created;
    public int CreatedCount => created;
    public TaskCompletionSource<bool> LogoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> HeroStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseLogo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseHero { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> LogoStateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<ImageUploadState> ReleaseLogoState { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool HoldLogoState { get; set; }
    public ImageUploadState? LogoState { get; set; }
    public CancellationToken LogoPollToken { get; private set; }
    public CancellationToken LogoPutToken { get; private set; }

    public static ImageUploadState ReadyState(string imageId) =>
        new(imageId, "complete", null, Ready: true, Blocked: false);

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
        LogoPutToken = cancellationToken;
        LogoStarted.TrySetResult(true);
        return ReleaseLogo.Task;
      }
      HeroStarted.TrySetResult(true);
      return ReleaseHero.Task;
    }

    public Task<CompleteImageUploadResponse> CompleteAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompleteImageUploadResponse(new ImageUpload(imageId, "complete")));

    public async Task<ImageUploadStateResponse> FetchUploadStateAsync(
        string imageId, CancellationToken cancellationToken = default)
    {
      if (imageId == "image-1")
      {
        LogoPollToken = cancellationToken;
        if (HoldLogoState)
        {
          LogoStateStarted.TrySetResult(true);
          return new ImageUploadStateResponse(await ReleaseLogoState.Task.ConfigureAwait(true));
        }
        return new ImageUploadStateResponse(LogoState ?? ReadyState(imageId));
      }
      return new ImageUploadStateResponse(ReadyState(imageId));
    }
  }

  public class CountingTopicService : DispatchProxy
  {
    public int Calls { get; private set; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      Calls++;
      throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
    }
  }

  public class HeldSaveTopicService : DispatchProxy
  {
    public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<TopicMutationResponse> ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      if (targetMethod?.Name != "CreateTopicAsync") throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
      Started.TrySetResult(true);
      return ReleaseCreate.Task;
    }
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
