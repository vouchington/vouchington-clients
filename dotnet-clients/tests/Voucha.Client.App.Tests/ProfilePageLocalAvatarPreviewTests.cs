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
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ProfilePageLocalAvatarPreviewTests
{
  private const string TinyPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

  [Fact]
  public async Task SelectedAvatarBytesAreUploadedAndDisappearingFencesLateCompletion()
  {
    var selectedBytes = Convert.FromBase64String(TinyPng);
    var fixture = await CreatePageAsync(selectedBytes, holdUpload: true);
    try
    {
      Assert.True(fixture.ViewModel.CanMutateAvatar, fixture.ViewModel.ErrorMessage);
      var uploadSelection = InvokeTask(fixture.Page, "UploadAvatarSelectionAsync");
      var createStarted = await Task.WhenAny(
          fixture.Upload.CreateStarted.Task,
          Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
      Assert.True(ReferenceEquals(fixture.Upload.CreateStarted.Task, createStarted), fixture.ViewModel.ErrorMessage);

      var preview = fixture.Page.FindByName<Image>("AvatarLocalPreviewImage");
      // The portable App.Tests target has no native thumbnail decoder. The real
      // production decode path runs on Windows and Mac Catalyst.
      Assert.Null(preview.Source);
      Assert.False(preview.IsVisible);
      Assert.False(fixture.Page.FindByName<Label>("AvatarPreviewUnavailableLabel").IsVisible);

      Invoke(fixture.Page, "OnDisappearing");
      Assert.Null(preview.Source);
      Assert.False(preview.IsVisible);
      Assert.False(fixture.Page.FindByName<Label>("AvatarPreviewUnavailableLabel").IsVisible);

      fixture.Upload.ReleaseCreate.TrySetResult(new ImageUploadUrlResponse(fixture.Upload.Url));
      await fixture.Upload.UploadFinished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
      await fixture.Upload.UploadStateFetched.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
      await uploadSelection.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

      Assert.Equal(selectedBytes, fixture.Upload.UploadedBytes);
      Assert.Null(preview.Source);
      Assert.False(preview.IsVisible);
      Assert.False(fixture.Page.FindByName<Label>("AvatarPreviewUnavailableLabel").IsVisible);
      Assert.Equal(0, fixture.Settings.IdentityUpdates);
    }
    finally
    {
      fixture.Upload.ReleaseCreate.TrySetResult(new ImageUploadUrlResponse(fixture.Upload.Url));
      fixture.Dispose();
    }
  }

  [Fact]
  public async Task UndecodableAvatarShowsAccessiblePortableFallbackAfterUploadCompletes()
  {
    var fixture = await CreatePageAsync([1, 2, 3, 4], holdUpload: false);
    try
    {
      await InvokeTask(fixture.Page, "UploadAvatarSelectionAsync")
          .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

      var preview = fixture.Page.FindByName<Image>("AvatarLocalPreviewImage");
      var unavailable = fixture.Page.FindByName<Label>("AvatarPreviewUnavailableLabel");
      Assert.Null(preview.Source);
      Assert.False(preview.IsVisible);
      Assert.True(unavailable.IsVisible);
      Assert.Equal("Uploaded image preview unavailable", unavailable.Text);
      Assert.Equal(1, fixture.Settings.IdentityUpdates);
      Assert.Equal(fixture.Upload.ReadyImageId, fixture.Settings.LastIdentityUpdateBody?.ProfileImageId?.Value);
    }
    finally
    {
      fixture.Dispose();
    }
  }

  private static async Task<PageFixture> CreatePageAsync(byte[] bytes, bool holdUpload)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" })
    {
      app.Resources[key] = new Style(typeof(Label));
    }
    app.Resources["images.uploadPreviewUnavailable"] = "Uploaded image preview unavailable";
    app.Resources["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English);
    app.Resources["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new StubDeviceLanguageProvider("en")));
    app.Resources["ProfilePlacementImageSource96"] = new PlacementImageSourceConverter(
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")), 96);

    var path = Path.Combine(Path.GetTempPath(), $"profile-avatar-{Guid.NewGuid():N}.png");
    await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

    var settings = DispatchProxy.Create<ISettingsService, SettingsProxy>();
    var settingsProxy = (SettingsProxy)(object)settings;
    var upload = new HeldAvatarUploadService(holdUpload);
    var model = new ProfileViewModel(
        settings,
        DispatchProxy.Create<ILandingPagesService, UnusedService>(),
        DispatchProxy.Create<IPostsService, PostsProxy>(),
        upload,
        new AppConfig(new Uri("https://api.test")));
    await model.LoadOwnAsync(TestContext.Current.CancellationToken);
    var picker = new TestFilePicker(new FileResult(path, "image/png"));
    var page = new ProfilePage(
        model,
        picker.PickAsync,
        file => Task.FromResult<Stream>(File.OpenRead(file.FullPath)));
    return new PageFixture(page, model, settingsProxy, upload, path);
  }

  private static Task InvokeTask(ProfilePage page, string method)
  {
    var target = typeof(ProfilePage).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!;
    return (Task)target.Invoke(page, null)!;
  }

  private static void Invoke(ProfilePage page, string method)
  {
    var target = typeof(ProfilePage).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!;
    var arguments = target.GetParameters().Length == 0 ? null : new object?[] { null, EventArgs.Empty };
    target.Invoke(page, arguments);
  }

  private sealed class PageFixture(
      ProfilePage page,
      ProfileViewModel viewModel,
      SettingsProxy settings,
      HeldAvatarUploadService upload,
      string imagePath) : IDisposable
  {
    public ProfilePage Page { get; } = page;
    public ProfileViewModel ViewModel { get; } = viewModel;
    public SettingsProxy Settings { get; } = settings;
    public HeldAvatarUploadService Upload { get; } = upload;

    public void Dispose()
    {
      ViewModel.Dispose();
      File.Delete(imagePath);
    }
  }

  private sealed class TestFilePicker(FileResult result)
  {
    public Task<FileResult?> PickAsync() => Task.FromResult<FileResult?>(result);
  }

  private sealed class HeldAvatarUploadService(bool holdCreate) : IImageUploadService
  {
    public ImageUploadUrl Url { get; } = new("image-1", "https://upload.test/image-1", "image/png", DateTimeOffset.UtcNow);
    public string ReadyImageId { get; } = "ready-image-2";
    public TaskCompletionSource<bool> CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<ImageUploadUrlResponse> ReleaseCreate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> UploadStateFetched { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> UploadFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public byte[]? UploadedBytes { get; private set; }

    public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(CreateImageUploadUrlBody body, CancellationToken cancellationToken = default)
    {
      CreateStarted.TrySetResult(true);
      return holdCreate
          ? ReleaseCreate.Task
          : Task.FromResult(new ImageUploadUrlResponse(Url));
    }

    public async Task UploadAsync(ImageUploadUrl upload, Stream content, long contentLength, CancellationToken cancellationToken = default)
    {
      using var buffer = new MemoryStream();
      await content.CopyToAsync(buffer, TestContext.Current.CancellationToken);
      UploadedBytes = buffer.ToArray();
      UploadFinished.TrySetResult(true);
    }

    public Task<CompleteImageUploadResponse> CompleteAsync(string imageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CompleteImageUploadResponse(new ImageUpload(imageId, "complete")));

    public Task<ImageUploadStateResponse> FetchUploadStateAsync(string imageId, CancellationToken cancellationToken = default)
    {
      UploadStateFetched.TrySetResult(true);
      return Task.FromResult(new ImageUploadStateResponse(new ImageUploadState(ReadyImageId, "complete", null, Ready: true, Blocked: false)));
    }
  }

  public class SettingsProxy : DispatchProxy
  {
    private static readonly User InitialUser = new("user-1", "alice");
    public int IdentityUpdates { get; private set; }
    public UpdateMyIdentityBody? LastIdentityUpdateBody { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
      nameof(ISettingsService.FetchMyIdentityAsync) => Task.FromResult(new MyIdentityResponse(InitialUser)),
      nameof(ISettingsService.FetchUserAsync) => Task.FromResult(new UserResponse(InitialUser)),
      nameof(ISettingsService.UpdateMyIdentityAsync) => UpdateIdentity((UpdateMyIdentityBody)args![0]!),
      _ => throw new InvalidOperationException($"Unexpected settings call: {targetMethod?.Name}"),
    };

    private Task<MyIdentityResponse> UpdateIdentity(UpdateMyIdentityBody body)
    {
      IdentityUpdates++;
      LastIdentityUpdateBody = body;
      return Task.FromResult(new MyIdentityResponse(InitialUser with
      {
        ProfileImageId = body.ProfileImageId?.Value,
      }));
    }
  }

  public class PostsProxy : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
      nameof(IPostsService.FetchPostsAsync) => Task.FromResult(new PostsFeedResponse(
          [], new PageInfo(null, false, null), new Dictionary<string, Post>(), new Dictionary<string, User>(),
          new Dictionary<string, Community>())),
      _ => throw new InvalidOperationException($"Unexpected posts call: {targetMethod?.Name}"),
    };
  }

  public class UnusedService : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException($"Unexpected service call: {targetMethod?.Name}");
  }
}
