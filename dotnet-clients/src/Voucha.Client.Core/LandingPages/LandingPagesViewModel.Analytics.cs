using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  private LandingPageAnalytics? selectedPageAnalytics;
  private string? selectedPageAnalyticsError;
  private int selectedPageAnalyticsLoadGeneration;

  public LandingPageAnalytics? SelectedPageAnalytics
  {
    get => selectedPageAnalytics;
    private set
    {
      if (SetProperty(ref selectedPageAnalytics, value))
      {
        OnPropertyChanged(nameof(HasSelectedPageAnalytics));
        OnPropertyChanged(nameof(HasSelectedPageAnalyticsSection));
        OnPropertyChanged(nameof(SelectedAnalyticsItemClicks));
      }
    }
  }

  public bool HasSelectedPageAnalytics => SelectedPageAnalytics is not null;

  public bool IsAnalyticsPaidAccessRequired => SelectedPage is not null && !canViewAnalytics;

  public IReadOnlyList<LandingPageItemClickRow> SelectedAnalyticsItemClicks =>
      SelectedPageAnalytics?.ItemClicks.Select(item =>
          LandingPageItemClickRow.From(item, localization)).ToArray() ?? [];

  public bool HasSelectedPageAnalyticsSection =>
      IsAnalyticsPaidAccessRequired || SelectedPageAnalytics is not null || !string.IsNullOrWhiteSpace(SelectedPageAnalyticsError);

  public string? SelectedPageAnalyticsError
  {
    get => selectedPageAnalyticsError;
    private set
    {
      if (SetProperty(ref selectedPageAnalyticsError, value))
      {
        OnPropertyChanged(nameof(HasSelectedPageAnalyticsSection));
        OnPropertyChanged(nameof(HasSelectedPageAnalyticsError));
      }
    }
  }

  public bool HasSelectedPageAnalyticsError => !string.IsNullOrWhiteSpace(SelectedPageAnalyticsError);

  private void ClearSelectedPageAnalytics()
  {
    SelectedPageAnalytics = null;
    SelectedPageAnalyticsError = null;
  }

  public async Task LoadSelectedAnalyticsAsync(CancellationToken cancellationToken = default)
  {
    ClearSelectedPageAnalytics();
    if (SelectedPage is null)
    {
      return;
    }

    var requestedPageId = SelectedPage.Id;
    var requestGeneration = unchecked(++selectedPageAnalyticsLoadGeneration);
    if (!await RefreshAnalyticsEntitlementAsync(requestedPageId, requestGeneration, cancellationToken).ConfigureAwait(true)) return;
    try
    {
      var response = await service
          .FetchMyLandingPageAnalyticsAsync(requestedPageId, cancellationToken)
          .ConfigureAwait(true);
      if (!string.Equals(SelectedPage?.Id, requestedPageId, StringComparison.Ordinal) ||
          selectedPageAnalyticsLoadGeneration != requestGeneration)
      {
        return;
      }

      SelectedPageAnalytics = response.Analytics;
      SelectedPageAnalyticsError = null;
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!string.Equals(SelectedPage?.Id, requestedPageId, StringComparison.Ordinal) ||
          selectedPageAnalyticsLoadGeneration != requestGeneration)
      {
        return;
      }

      SelectedPageAnalytics = null;
      SelectedPageAnalyticsError = ex.Message;
    }
  }

  private async Task<bool> RefreshAnalyticsEntitlementAsync(
      string requestedPageId,
      int requestGeneration,
      CancellationToken cancellationToken)
  {
    if (fetchMembership is null) return canViewAnalytics;
    try
    {
      var response = await fetchMembership(cancellationToken).ConfigureAwait(true);
      if (!string.Equals(SelectedPage?.Id, requestedPageId, StringComparison.Ordinal) ||
          selectedPageAnalyticsLoadGeneration != requestGeneration) return false;
      SetCanViewAnalytics(response.Membership.CanViewLandingPageAnalytics());
      return canViewAnalytics;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      return false;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (selectedPageAnalyticsLoadGeneration == requestGeneration) SetCanViewAnalytics(false);
      return false;
    }
  }

  private void SetCanViewAnalytics(bool value)
  {
    if (canViewAnalytics == value) return;
    canViewAnalytics = value;
    OnPropertyChanged(nameof(IsAnalyticsPaidAccessRequired));
    OnPropertyChanged(nameof(HasSelectedPageAnalyticsSection));
  }
}
