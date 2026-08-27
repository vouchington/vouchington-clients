using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPageAnalyticsViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly ILandingPagesService service;
  private LandingPage? landingPage;
  private LandingPageAnalytics? analytics;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;

  public LandingPageAnalyticsViewModel(
      ILandingPagesService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public LandingPage? LandingPage
  {
    get => landingPage;
    private set
    {
      if (SetProperty(ref landingPage, value))
      {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSlug));
      }
    }
  }

  public LandingPageAnalytics? Analytics
  {
    get => analytics;
    private set
    {
      if (SetProperty(ref analytics, value))
      {
        OnPropertyChanged(nameof(HasAnalytics));
        OnPropertyChanged(nameof(LocalizedItemClicks));
      }
    }
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(IsLoading));
      }
    }
  }

  public bool IsLoading => State == LoadState.Loading;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public bool HasAnalytics => Analytics is not null;

  public IReadOnlyList<LandingPageItemClickRow> LocalizedItemClicks =>
      Analytics?.ItemClicks.Select(item =>
          LandingPageItemClickRow.From(item, localization)).ToArray() ?? [];

  public string PageTitle => LandingPage?.Title
      ?? localization.Localize(UiMessageKey.NativeDotnetDynamicLandingPage);

  public string PageSlug => LandingPage?.Slug ?? string.Empty;

  public async Task LoadAsync(string pageId, CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    State = LoadState.Loading;
    ErrorMessage = null;
    LandingPage = null;
    Analytics = null;

    try
    {
      var response = await service.FetchAdminLandingPageAnalyticsAsync(pageId, cancellationToken).ConfigureAwait(true);
      LandingPage = response.LandingPage;
      Analytics = response.Analytics;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(PageTitle));
    OnPropertyChanged(nameof(Analytics));
    OnPropertyChanged(nameof(LocalizedItemClicks));
  }

  public void Dispose() => localeSubscription?.Dispose();
}
