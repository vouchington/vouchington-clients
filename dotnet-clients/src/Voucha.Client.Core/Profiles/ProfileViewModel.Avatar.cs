using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private const int AvatarUploadPollAttempts = 30;
  private static readonly TimeSpan AvatarUploadPollInterval = TimeSpan.FromSeconds(2);

  public async Task SaveBioAsync(string markdown, CancellationToken cancellationToken = default)
  {
    if (!CanEdit) return;
    IsSavingBio = true;
    ErrorMessage = null;

    try
    {
      var response = await settingsService.UpdateMyProfileAsync(markdown, cancellationToken).ConfigureAwait(true);
      BioMarkdown = response.Profile.Markdown ?? string.Empty;
      if (User is not null) User = User with { Markdown = BioMarkdown };
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsSavingBio = false;
    }
  }

  public async Task<bool> UploadAvatarAsync(
      Stream content,
      string contentType,
      long contentLength,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(content);
    if (!CanMutateAvatar || cancellationToken.IsCancellationRequested) return false;
    if (contentLength <= 0 || contentLength > MaxAvatarUploadBytes)
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpChooseImageUpTo50Mb);
      return false;
    }

    var generation = Interlocked.Increment(ref avatarMutationGeneration);
    IsUploadingAvatar = true;
    ErrorMessage = null;

    try
    {
      var upload = await imageUploadService.CreateUploadUrlAsync(
          new CreateImageUploadUrlBody(contentType, checked((int)contentLength)),
          cancellationToken).ConfigureAwait(true);
      await imageUploadService.UploadAsync(upload.Upload, content, contentLength, cancellationToken).ConfigureAwait(true);
      var completion = await imageUploadService.CompleteAsync(upload.Upload.ImageId, cancellationToken).ConfigureAwait(true);
      var uploadState = await WaitForAvatarUploadStateAsync(completion.Image.Id, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentAvatarMutation(generation, cancellationToken)) return false;
      if (!IsReadyAvatarUploadState(uploadState))
      {
        ErrorMessage = uploadState.Blocked
            ? localization.Localize(UiMessageKey.NativeDotnetCsharpImageBlocked)
            : uploadState.UploadError ?? localization.Localize(UiMessageKey.NativeDotnetCsharpAvatarUploadFailed);
        return false;
      }

      var response = await UpdateAvatarIdentityAsync(
          // The processing pipeline may return a canonical ready image id that differs from the original upload id.
          new UpdateMyIdentityBody(ProfileImageId: JsonNullableString.FromString(uploadState.Id))).ConfigureAwait(true);
      if (!IsCurrentAvatarMutation(generation, cancellationToken)) return false;
      Identity = response.Identity;
      if (User is not null) User = User with
      {
        ProfileImageId = response.Identity.ProfileImageId,
        ProfileImagePlacement = response.Identity.ProfileImagePlacement,
      };
      return true;
    }
    catch (TimeoutException ex)
    {
      if (IsCurrentAvatarMutation(generation, cancellationToken)) ErrorMessage = ex.Message;
      return false;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (IsCurrentAvatarMutation(generation, cancellationToken)) ErrorMessage = ex.Message;
      return false;
    }
    finally
    {
      IsUploadingAvatar = false;
    }
  }

  public void ReportAvatarUploadFailure(string message) =>
      ErrorMessage = string.IsNullOrWhiteSpace(message)
          ? localization.Localize(UiMessageKey.NativeDotnetCsharpAvatarUploadFailed)
          : message;

  private async Task<ImageUploadState> WaitForAvatarUploadStateAsync(
      string imageId,
      CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < AvatarUploadPollAttempts; attempt++)
    {
      try
      {
        var response = await imageUploadService.FetchUploadStateAsync(imageId, cancellationToken).ConfigureAwait(false);
        var state = response.UploadState;
        if (IsReadyAvatarUploadState(state) || state.Blocked || IsTerminalAvatarUploadState(state.UploadStatus))
        {
          return state;
        }
      }
      catch (Exception ex) when (IsTransientAvatarUploadFailure(ex))
      {
        if (attempt + 1 >= AvatarUploadPollAttempts)
        {
          throw;
        }
      }

      if (attempt + 1 < AvatarUploadPollAttempts)
      {
        await Task.Delay(AvatarUploadPollInterval, cancellationToken).ConfigureAwait(false);
      }
    }

    throw new TimeoutException(localization.Localize(UiMessageKey.NativeDotnetCsharpAvatarUploadTimedOut));
  }

  private static bool IsReadyAvatarUploadState(ImageUploadState state) =>
      !state.Blocked && state.Ready;

  private static bool IsTerminalAvatarUploadState(string uploadStatus) =>
      string.Equals(uploadStatus, "failed", StringComparison.Ordinal) ||
      string.Equals(uploadStatus, "error", StringComparison.Ordinal);

  private static bool IsTransientAvatarUploadFailure(Exception exception) =>
      exception switch
      {
        OperationCanceledException => false,
        VouchaApiException apiException when apiException.StatusCode is HttpStatusCode statusCode =>
            IsTransientHttpStatusCode(statusCode),
        HttpRequestException requestException when requestException.StatusCode is null => true,
        HttpRequestException requestException when requestException.StatusCode is HttpStatusCode statusCode =>
            IsTransientHttpStatusCode(statusCode),
        _ => false,
      };

  private static bool IsTransientHttpStatusCode(HttpStatusCode statusCode)
  {
    var status = (int)statusCode;
    return status == 429 || status is >= 500 and <= 599;
  }

  public async Task RefreshAsync(CancellationToken cancellationToken = default)
  {
    if (CanEdit)
    {
      await LoadOwnAsync(cancellationToken).ConfigureAwait(true);
      return;
    }
    if (string.IsNullOrWhiteSpace(loadedIdOrUsername)) return;

    await LoadPublicScopeAsync(loadedIdOrUsername, ProfileScope, cancellationToken).ConfigureAwait(true);
    if (State == LoadState.Loaded) await LoadUserTagsAsync(cancellationToken).ConfigureAwait(true);
  }
}
