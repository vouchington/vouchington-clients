using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  public void Dispose()
  {
    if (viewerProvider is not null)
    {
      viewerProvider.ViewerChanged -= OnViewerChanged;
    }
    if (sessionStore is not null)
    {
      sessionStore.SessionChanged -= OnSessionChanged;
    }
    localeSubscription?.Dispose();

    var cancellation = Interlocked.Exchange(ref activeSearchCancellation, null);
    cancellation?.Cancel();
    cancellation?.Dispose();
  }

  public void OnUiLocaleChanged()
  {
    if (remapGroups is not null) Groups = remapGroups();
    OnPropertyChanged(nameof(ReportButtonText));
  }

  private static string L(IUiLocalization localization, UiMessageKey key) => localization.Localize(key);

  private static string F(IUiLocalization localization, UiMessageKey key, params (string Name, object? Value)[] arguments) =>
      localization.Format(key, arguments);
}
