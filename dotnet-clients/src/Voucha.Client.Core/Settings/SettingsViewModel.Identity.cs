#pragma warning disable CA1031
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public async Task SaveIdentityAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await EnsureUserIdAsync(cancellationToken).ConfigureAwait(true);
      await settingsService.UpdateMyIdentityAsync(
          new UpdateMyIdentityBody(
              Username,
              DisplayNameSource,
              ProfileImageId is null ? null : JsonNullableString.FromString(ProfileImageId)),
          cancellationToken).ConfigureAwait(true);
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task ClearAvatarAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await EnsureUserIdAsync(cancellationToken).ConfigureAwait(true);
      await settingsService.UpdateMyIdentityAsync(
          new UpdateMyIdentityBody(
              Username,
              DisplayNameSource,
              JsonNullableString.Null),
          cancellationToken).ConfigureAwait(true);
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task SaveProfileAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await settingsService.UpdateMyProfileAsync(ProfileMarkdown, cancellationToken).ConfigureAwait(true);
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }
}
#pragma warning restore CA1031
