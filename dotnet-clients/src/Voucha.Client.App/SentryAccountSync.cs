using Sentry;
using Voucha.Client.Core;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

internal static class SentryAccountSync
{
  private static string? boundAccountId;

  public static void Attach(ISessionStore store)
  {
    store.SessionChanged += (_, args) => Apply(args.Snapshot.Identity?.Id);
    Apply(store.Current.Identity?.Id);
  }

  private static void Apply(string? accountId)
  {
    var decision = SentryAccountDecision.Update(boundAccountId, accountId);
    if (decision.Action == SentryAccountAction.Unchanged)
    {
      return;
    }

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
    boundAccountId = decision.AccountId;
  }

  internal const string AccountMarker = "voucha.account";

  internal static string? BoundAccountId => boundAccountId;
}
