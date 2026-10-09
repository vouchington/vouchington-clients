#pragma warning disable CA1031
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private IReadOnlyList<AuthSession> sessions = [];

  public IReadOnlyList<AuthSession> Sessions
  {
    get => sessions;
    private set
    {
      if (SetProperty(ref sessions, value)) sessionPages.ReplaceItems(value);
    }
  }

  public async Task<bool> RevokeSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(session);
    var ownerInvalidationGeneration = Volatile.Read(ref apiKeyOwnerInvalidationGeneration);

    try
    {
      ErrorMessage = null;
      await settingsService.DeleteAuthSessionAsync(session.Id, cancellationToken).ConfigureAwait(true);
      if (ownerInvalidationGeneration != Volatile.Read(ref apiKeyOwnerInvalidationGeneration)) return false;
      RemoveSessionFromPage(session.Id);
      if (session.IsCurrent) InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
      return session.IsCurrent;
    }
    catch (Exception ex)
    {
      if (ownerInvalidationGeneration != Volatile.Read(ref apiKeyOwnerInvalidationGeneration)) return false;
      if (ex is VouchaApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
        InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
      ErrorMessage = ex.Message;
      return false;
    }
  }

  public async Task<bool> RevokeAllSessionsAsync(CancellationToken cancellationToken = default)
  {
    var ownerInvalidationGeneration = Volatile.Read(ref apiKeyOwnerInvalidationGeneration);
    try
    {
      ErrorMessage = null;
      await settingsService.RevokeAuthSessionsAsync(cancellationToken).ConfigureAwait(true);
      if (ownerInvalidationGeneration != Volatile.Read(ref apiKeyOwnerInvalidationGeneration)) return false;
      ClearSessionPage();
      InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
      return true;
    }
    catch (Exception ex)
    {
      if (ownerInvalidationGeneration != Volatile.Read(ref apiKeyOwnerInvalidationGeneration)) return false;
      if (ex is VouchaApiException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
        InvalidateApiKeyOwnerIdentityIfCurrent(ownerInvalidationGeneration);
      ErrorMessage = ex.Message;
      return false;
    }
  }
}
#pragma warning restore CA1031
