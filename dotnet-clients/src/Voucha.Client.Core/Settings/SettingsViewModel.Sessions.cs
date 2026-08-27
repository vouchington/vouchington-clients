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

    try
    {
      ErrorMessage = null;
      await settingsService.DeleteAuthSessionAsync(session.Id, cancellationToken).ConfigureAwait(true);
      RemoveSessionFromPage(session.Id);
      return session.IsCurrent;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      return false;
    }
  }

  public async Task<bool> RevokeAllSessionsAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      ErrorMessage = null;
      await settingsService.RevokeAuthSessionsAsync(cancellationToken).ConfigureAwait(true);
      ClearSessionPage();
      return true;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      return false;
    }
  }
}
#pragma warning restore CA1031
