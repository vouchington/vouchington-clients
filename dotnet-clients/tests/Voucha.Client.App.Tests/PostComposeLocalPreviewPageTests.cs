using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
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
  public async Task MountedPageShowsSelectedBytesBeforeUploadUrlAndRemovesPendingSelection()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = 0,
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
    var uploads = new HeldUploadService();
    var viewModel = new PostComposeViewModel(
        DispatchProxy.Create<IPostsService, UnusedService>(),
        new AppConfig(new Uri("https://api.test"), "site-key"),
        imageUploadService: uploads);
    using var locale = new UiLocaleController(new EnglishLanguages());
    var page = new PostComposePage(
        viewModel,
        DispatchProxy.Create<ITurnstileTokenProvider, UnusedService>(),
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }),
        new EmailVerificationRecoveryCoordinator(
            DispatchProxy.Create<IEmailAddressService, UnusedService>(),
            UiLocalization.English,
            locale));
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
