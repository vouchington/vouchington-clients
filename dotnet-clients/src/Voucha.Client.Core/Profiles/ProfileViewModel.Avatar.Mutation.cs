using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private int avatarMutationGeneration;

  // Once dispatched, await the server response before admitting another identity mutation.
  // Navigation cancellation suppresses local publication without releasing this ownership early.
  private Task<MyIdentityResponse> UpdateAvatarIdentityAsync(UpdateMyIdentityBody body) =>
      settingsService.UpdateMyIdentityAsync(body, CancellationToken.None);

  public async Task RemoveAvatarAsync(CancellationToken cancellationToken = default)
  {
    if (!CanMutateAvatar || cancellationToken.IsCancellationRequested) return;
    var generation = Interlocked.Increment(ref avatarMutationGeneration);
    IsUploadingAvatar = true;
    ErrorMessage = null;

    try
    {
      var response = await UpdateAvatarIdentityAsync(
          new UpdateMyIdentityBody(ProfileImageId: JsonNullableString.Null)).ConfigureAwait(true);
      if (!IsCurrentAvatarMutation(generation, cancellationToken)) return;
      Identity = response.Identity;
      if (User is not null) User = User with { ProfileImageId = null, ProfileImagePlacement = null };
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (IsCurrentAvatarMutation(generation, cancellationToken)) ErrorMessage = ex.Message;
    }
    finally
    {
      IsUploadingAvatar = false;
    }
  }

  private bool IsCurrentAvatarMutation(int generation, CancellationToken cancellationToken) =>
      Volatile.Read(ref avatarMutationGeneration) == generation && !cancellationToken.IsCancellationRequested;
}
