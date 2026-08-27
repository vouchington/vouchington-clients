using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task OpenRouteAsync(string route, string? slug = null) =>
      MainThread.InvokeOnMainThreadAsync(() => GoToAsync(
          slug is { Length: > 0 }
              ? $"//{route}?slug={Uri.EscapeDataString(slug)}"
              : $"//{route}"));

  public async Task OpenIntentAsync(string intentId, string? slug = null, NativeRouteMatch? match = null)
  {
    await EnsureIntentAvailableAsync(intentId);
    var shouldStoreRouteMatch = await ShouldStoreIntentRouteMatchAsync(intentId, match);
    lock (pendingIntentRouteMatchesGate)
    {
      if (match is not null && shouldStoreRouteMatch)
      {
        pendingIntentRouteMatches[intentId] = match;
      }
      else
      {
        pendingIntentRouteMatches.Remove(intentId);
      }
    }

    await OpenRouteAsync(intentId, slug);
  }
}
