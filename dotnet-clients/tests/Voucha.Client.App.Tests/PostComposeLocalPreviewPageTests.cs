using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Storage;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PostComposeLocalPreviewPageTests
{
  [Fact]
  public async Task UndecodableBytesDoNotBecomeRetainedThumbnail()
  {
    using var content = new MemoryStream([1, 2, 3]);
    var selection = await LocalImagePreview.ReadSelectedBytesAsync(content, TestContext.Current.CancellationToken);
    Assert.Null(selection.PreviewBytes);
    Assert.Equal([1, 2, 3], selection.Bytes);
  }

  [Fact]
  public async Task RemovingSelectionDuringFileLoadDoesNotShowUploadFailure()
  {
    var (page, viewModel, locale) = CreatePage(new HeldUploadService());
    using var localeScope = locale;
    var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var add = page.AddImagesAsync(
        () => Task.FromResult<IReadOnlyList<FileResult>?>([new FileResult("selected.png")]),
        async (_, cancellationToken) =>
        {
          loadStarted.TrySetResult();
          await Task.Delay(Timeout.Infinite, cancellationToken);
          throw new InvalidOperationException("Canceled file load unexpectedly completed.");
        });
    await loadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    typeof(PostComposePage).GetMethod("OnRemovePendingPreviewClicked", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, [null, EventArgs.Empty]);
    await add.WaitAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Empty(viewModel.Images);
    Assert.Null(viewModel.PendingLocalPreviewBytes);
  }

  [Fact]
  public async Task CanceledFileOpenDisposesStreamReturnedAfterCancellation()
  {
    using var cancellation = new CancellationTokenSource();
    var returnedStream = new TrackingStream();
    var releaseOpen = new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);
    var load = ImageSelectionLoader.LoadAsync(
        new FileResult("selected.png", "image/png"), () => releaseOpen.Task, cancellation.Token);
    cancellation.Cancel();
    releaseOpen.TrySetResult(returnedStream);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
    Assert.True(returnedStream.WasDisposed);
  }

  [Fact]
  public async Task MountedPageShowsSelectedBytesBeforeUploadUrlAndRemovesPendingSelection()
  {
    var uploads = new HeldUploadService();
    var (page, viewModel, locale) = CreatePage(uploads);
    using var _ = locale;
    using var content = new MemoryStream([1, 2, 3]);
    var upload = viewModel.UploadImageWithPreviewAsync(
        content, "image/png", content.Length, new byte[] { 1, 2, 3 }, false,
        cancellationToken: TestContext.Current.CancellationToken);
    try
    {
      await uploads.CreateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.True(page.FindByName<Grid>("PendingImagePreviewGrid").IsVisible);
      Assert.True(page.FindByName<Image>("PendingImagePreview").IsVisible);
      Assert.IsType<StreamImageSource>(page.FindByName<Image>("PendingImagePreview").Source);
      Assert.Empty(viewModel.Images);
      typeof(PostComposePage).GetMethod("OnRemovePendingPreviewClicked", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, [page.FindByName<Button>("RemovePendingPreviewButton"), EventArgs.Empty]);
      Assert.False(page.FindByName<Grid>("PendingImagePreviewGrid").IsVisible);
    }
    finally
    {
      uploads.ReleaseCreate.TrySetResult(true);
      await upload;
    }
    Assert.False(await upload);
    Assert.Empty(viewModel.Images);

    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("ready-image", 0, PreviewUnavailable: true)));
    Assert.Contains(page.GetVisualTreeDescendants().OfType<Label>(),
        label => label.Text == "Image uploaded. Preview unavailable." && label.IsVisible);
  }

  private static (PostComposePage Page, PostComposeViewModel ViewModel, UiLocaleController Locale) CreatePage(
      IImageUploadService? uploads = null)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var locale = new UiLocaleController(new EnglishLanguages());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = new UiLocaleVersion(locale),
        ["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    var viewModel = new PostComposeViewModel(
        DispatchProxy.Create<IPostsService, UnusedService>(),
        new AppConfig(new Uri("https://api.test"), "site-key"),
        imageUploadService: uploads);
    var page = new PostComposePage(
        viewModel,
        DispatchProxy.Create<ITurnstileTokenProvider, UnusedService>(),
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }),
        new EmailVerificationRecoveryCoordinator(
            DispatchProxy.Create<IEmailAddressService, UnusedService>(),
            UiLocalization.English,
            locale));
    return (page, viewModel, locale);
  }

  private sealed class HeldUploadService : IImageUploadService
  {
    public TaskCompletionSource<bool> CreateStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ReleaseCreate { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
        CreateImageUploadUrlBody body, CancellationToken cancellationToken = default)
    {
      CreateStarted.TrySetResult(true);
      await ReleaseCreate.Task;
      return new ImageUploadUrlResponse(new ImageUploadUrl(
          "image-1", "https://upload.test/image-1", body.ContentType, DateTimeOffset.UtcNow));
    }

    public Task UploadAsync(ImageUploadUrl upload, Stream content, long contentLength,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CompleteImageUploadResponse> CompleteAsync(
        string imageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ImageUploadStateResponse> FetchUploadStateAsync(
        string imageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class TrackingStream : MemoryStream
  {
    public bool WasDisposed { get; private set; }

    public override ValueTask DisposeAsync()
    {
      WasDisposed = true;
      return base.DisposeAsync();
    }
  }

  public class UnusedService : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
  }

  private sealed class EnglishLanguages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
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
