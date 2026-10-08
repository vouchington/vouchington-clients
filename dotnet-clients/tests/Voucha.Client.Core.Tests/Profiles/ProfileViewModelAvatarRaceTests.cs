using System.Reflection;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelTests
{
  [Fact]
  public async Task CancelledAvatarUploadIgnoresLateIdentityResponse()
  {
    var (viewModel, proxy) = await NewHeldAvatarViewModelAsync();
    using var cancellation = new CancellationTokenSource();
    await using var content = new MemoryStream([1, 2, 3]);
    var upload = viewModel.UploadAvatarAsync(content, "image/png", content.Length, cancellation.Token);
    try
    {
      await proxy.UpdateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      cancellation.Cancel();
    }
    finally
    {
      proxy.ReleaseUpdate.TrySetResult(true);
      await upload;
    }
    Assert.False(await upload);

    Assert.Equal(new Uri("https://images.example.test/images/placements/avatar-placement/2/avatar-1?w=144"),
        viewModel.AvatarUrl);
    Assert.False(viewModel.IsUploadingAvatar);
  }

  [Fact]
  public async Task RemoveAvatarWinsOverHeldCancelledUploadResponse()
  {
    var (viewModel, proxy) = await NewHeldAvatarViewModelAsync();
    using var cancellation = new CancellationTokenSource();
    await using var content = new MemoryStream([1, 2, 3]);
    var upload = viewModel.UploadAvatarAsync(content, "image/png", content.Length, cancellation.Token);
    try
    {
      await proxy.UpdateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      cancellation.Cancel();
      await viewModel.RemoveAvatarAsync(TestContext.Current.CancellationToken);
    }
    finally
    {
      proxy.ReleaseUpdate.TrySetResult(true);
      await upload;
    }
    Assert.False(await upload);

    Assert.Null(viewModel.AvatarUrl);
    Assert.Null(viewModel.ProfileImageId);
    Assert.False(viewModel.IsUploadingAvatar);
  }

  [Fact]
  public async Task ReplacementAvatarWinsOverHeldFirstUploadResponse()
  {
    var (viewModel, proxy) = await NewHeldAvatarViewModelAsync(new SequentialAvatarImageService());
    using var firstCancellation = new CancellationTokenSource();
    await using var firstContent = new MemoryStream([1, 2, 3]);
    await using var secondContent = new MemoryStream([4, 5, 6]);
    var first = viewModel.UploadAvatarAsync(
        firstContent, "image/png", firstContent.Length, firstCancellation.Token);
    try
    {
      await proxy.UpdateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
      firstCancellation.Cancel();
      Assert.True(await viewModel.UploadAvatarAsync(
          secondContent, "image/png", secondContent.Length, TestContext.Current.CancellationToken));
    }
    finally
    {
      proxy.ReleaseUpdate.TrySetResult(true);
      await first;
    }
    Assert.False(await first);

    Assert.Equal("image-2", viewModel.ProfileImageId);
    Assert.Equal(new Uri("https://images.example.test/images/placements/avatar-placement/3/image-2?w=144"),
        viewModel.AvatarUrl);
  }

  private static async Task<(ProfileViewModel ViewModel, HeldIdentityUpdateProxy Proxy)> NewHeldAvatarViewModelAsync(
      IImageUploadService? images = null)
  {
    var settings = new RecordingSettingsService();
    var service = DispatchProxy.Create<ISettingsService, HeldIdentityUpdateProxy>();
    var proxy = (HeldIdentityUpdateProxy)service;
    proxy.Target = settings;
    var viewModel = new ProfileViewModel(
        service,
        new RecordingLandingPagesService(),
        new RecordingPostsService(),
        images ?? new RecordingImageUploadService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key",
            ImageBaseUrl: new Uri("https://images.example.test")));
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);
    return (viewModel, proxy);
  }

  public class HeldIdentityUpdateProxy : DispatchProxy
  {
    private int heldUpdates;
    public ISettingsService Target { get; set; } = null!;

    public TaskCompletionSource<bool> UpdateStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> ReleaseUpdate { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      if (targetMethod?.Name == nameof(ISettingsService.UpdateMyIdentityAsync) &&
          args?[0] is UpdateMyIdentityBody { ProfileImageId.Value: not null } &&
          Interlocked.Increment(ref heldUpdates) == 1)
      {
        UpdateStarted.TrySetResult(true);
        return HeldUpdateAsync(targetMethod, args);
      }

      return targetMethod!.Invoke(Target, args);
    }

    private async Task<MyIdentityResponse> HeldUpdateAsync(MethodInfo method, object?[] args)
    {
      await ReleaseUpdate.Task;
      return await (Task<MyIdentityResponse>)method.Invoke(Target, args)!;
    }
  }

  private sealed class SequentialAvatarImageService : IImageUploadService
  {
    private int uploads;

    public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
        CreateImageUploadUrlBody body, CancellationToken cancellationToken = default)
    {
      var id = $"image-{Interlocked.Increment(ref uploads)}";
      return Task.FromResult(new ImageUploadUrlResponse(new ImageUploadUrl(
          id, $"https://upload.test/{id}", body.ContentType, DateTimeOffset.UtcNow)));
    }

    public Task UploadAsync(ImageUploadUrl upload, Stream content, long contentLength,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<CompleteImageUploadResponse> CompleteAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompleteImageUploadResponse(new ImageUpload(imageId, "complete")));

    public Task<ImageUploadStateResponse> FetchUploadStateAsync(
        string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImageUploadStateResponse(
            new ImageUploadState(imageId, "complete", null, true, false)));
  }
}
