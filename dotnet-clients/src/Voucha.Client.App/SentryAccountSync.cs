using Sentry;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

internal static class SentryAccountSync
{
  private static readonly object Gate = new();
  private static string? boundAccountId;

  public static void Attach(ISessionStore store)
  {
    store.SessionChanged += (_, _) => Reconcile(store, null);
    Reconcile(store, null);
  }

  internal static void Reconcile(ISessionStore store, Action<SentryAccountDecision>? write)
  {
    lock (Gate)
    {
      var decision = SentryAccountDecision.Update(boundAccountId, store.Current.Identity?.Id);
      if (decision.Action == SentryAccountAction.Unchanged)
      {
        return;
      }

      (write ?? WriteScope)(decision);
      boundAccountId = decision.AccountId;
    }
  }

  internal static void ResetBoundAccount()
  {
    lock (Gate)
    {
      boundAccountId = null;
    }
  }

  private static void WriteScope(SentryAccountDecision decision)
  {
    SentrySdk.ConfigureScope(scope =>
    {
      scope.User.Id = decision.AccountId;
      scope.User.Email = null;
      scope.User.Username = null;
      scope.User.IpAddress = null;
      if (decision.AccountId is null)
      {
        scope.User.Other.Remove(AccountMarker);
      }
      else
      {
        scope.User.Other[AccountMarker] = decision.AccountId;
      }
    });
  }

  internal const string AccountMarker = "voucha.account";

  internal static string? BoundAccountId => boundAccountId;
}
