using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdInvitationsViewModel
{
  public async Task<bool> SendAsync(string username, string? relationship, CancellationToken cancellationToken = default)
  {
    if (householdId is not { } id || IsBusy || string.IsNullOrWhiteSpace(username)) return false;
    return await MutateAsync(async () =>
    {
      var invitation = await service.CreateAsync(id, username.Trim(), string.IsNullOrWhiteSpace(relationship) ? null : relationship.Trim(), cancellationToken)
          .ConfigureAwait(true);
      removed.Remove(invitation.Id);
      pagination.ReplaceItems([invitation, .. pagination.Items.Where(item => item.Id != invitation.Id)]);
    }, UiMessageKey.SettingsHouseholdInvitationsInvitationSent,
        UiMessageKey.SettingsHouseholdInvitationsInvitationSendFailed).ConfigureAwait(true);
  }

  public Task<bool> RevokeAsync(HouseholdInvitation invitation, CancellationToken cancellationToken = default) =>
      DecideAsync(invitation, "revoke", cancellationToken);

  public Task<bool> AcceptAsync(HouseholdInvitation invitation, CancellationToken cancellationToken = default) =>
      DecideAsync(invitation, "accept", cancellationToken);

  public Task<bool> DeclineAsync(HouseholdInvitation invitation, CancellationToken cancellationToken = default) =>
      DecideAsync(invitation, "decline", cancellationToken);

  private Task<bool> DecideAsync(HouseholdInvitation invitation, string action, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(invitation);
    if (IsBusy || removed.Contains(invitation.Id) || !pagination.Items.Contains(invitation) ||
        (action == "revoke" ? householdId != invitation.HouseholdId : IsSent)) return Task.FromResult(false);
    return MutateAsync(async () =>
    {
      if (action == "revoke") await service.RevokeAsync(householdId!, invitation.Id, cancellationToken).ConfigureAwait(true);
      else if (action == "accept") await service.AcceptAsync(invitation.Id, cancellationToken).ConfigureAwait(true);
      else await service.DeclineAsync(invitation.Id, cancellationToken).ConfigureAwait(true);
      removed.Add(invitation.Id);
    }, action switch
    {
      "revoke" => UiMessageKey.SettingsHouseholdInvitationsInvitationRevoked,
      "accept" => UiMessageKey.SettingsHouseholdInvitationsInvitationAccepted,
      _ => UiMessageKey.SettingsHouseholdInvitationsInvitationDeclined,
    }, UiMessageKey.SettingsHouseholdInvitationsInvitationUpdateFailed);
  }

  private async Task<bool> MutateAsync(Func<Task> operation, UiMessageKey success, UiMessageKey failure)
  {
    busy = true;
    message = null;
    Publish();
    try
    {
      await operation().ConfigureAwait(true);
      message = success;
      return true;
    }
    catch (OperationCanceledException) { return false; }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      message = failure;
      return false;
    }
    finally { busy = false; Publish(); }
  }
}
