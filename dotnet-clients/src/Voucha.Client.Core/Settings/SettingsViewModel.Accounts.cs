#pragma warning disable CA1031
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public async Task CreateDataRequestAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await EnsureUserIdAsync(cancellationToken).ConfigureAwait(true);
      var response = await settingsService.CreateUserDataRequestAsync(currentUserIdOrSlug!, cancellationToken)
          .ConfigureAwait(true);
      DataRequest = new UserDataRequestResponse(
          Id: response.Id,
          UserId: currentUserIdOrSlug,
          Status: response.Status,
          CreatedAt: response.CreatedAt,
          ExpiresAt: response.ExpiresAt);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task RefreshDataRequestAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await EnsureUserIdAsync(cancellationToken).ConfigureAwait(true);
      DataRequest = await settingsService.FetchUserDataRequestAsync(currentUserIdOrSlug!, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task<bool> DeleteAccountAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      if (!CanDeleteAccount)
      {
        ErrorMessage = localization.Format(
            UiMessageKey.NativeDotnetCsharpTypeDeleteConfirmation,
            ("phrase", DeleteAccountConfirmationPhrase));
        return false;
      }

      await EnsureUserIdAsync(cancellationToken).ConfigureAwait(true);
      var response = await settingsService.DeleteUserAsync(currentUserIdOrSlug!, cancellationToken)
          .ConfigureAwait(true);
      IdentitySummary = response.Logout
          ? localization.Localize(UiMessageKey.NativeDotnetCsharpAccountDeleted)
          : IdentitySummary;
      return response.Logout;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      return false;
    }
  }

  public async Task<string> CheckoutMembershipAsync(
      MembershipSku membershipSku,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(membershipSku);
    try
    {
      var response = await settingsService.CreateMembershipCheckoutSessionAsync(
          new MembershipCheckoutBody(
              membershipSku.StripePriceId,
              new Uri("/my/membership", UriKind.Relative),
              new Uri("/my/membership", UriKind.Relative)),
          cancellationToken).ConfigureAwait(true);
      return response.CheckoutSession.Url.ToString();
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      return string.Empty;
    }
  }

  public async Task<string> OpenMembershipPortalAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      var response = await settingsService.CreateMembershipPortalSessionAsync(
          new MembershipPortalBody(new Uri("/my/membership", UriKind.Relative)),
          cancellationToken).ConfigureAwait(true);
      return response.PortalSession.Url.ToString();
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      return string.Empty;
    }
  }

  public async Task CancelMembershipAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await settingsService.CancelMembershipAsync(cancellationToken).ConfigureAwait(true);
      await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task DeletePushSubscriptionAsync(
      WebPushSubscription subscription,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(subscription);
    try
    {
      await settingsService.DeletePushSubscriptionAsync(subscription.Id, cancellationToken).ConfigureAwait(true);
      RemovePushSubscriptionFromPage(subscription.Id);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }
}
#pragma warning restore CA1031
