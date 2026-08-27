using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ReferralLinks;

public sealed partial class ReferralLinksViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IReferralLinksService referralLinksService;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<ReferralLinkRow> items = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? analyticsEndCursor;
  private string? linksEndCursor;
  private bool hasMoreAnalytics;
  private bool hasMoreLinks;
  private int requestId;

  public ReferralLinksViewModel(
      IReferralLinksService referralLinksService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.referralLinksService = referralLinksService ??
        throw new ArgumentNullException(nameof(referralLinksService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged() => Items = Items.ToArray();

  public IReadOnlyList<ReferralLinkRow> Items
  {
    get => items;
    private set
    {
      if (SetProperty(ref items, value))
      {
        OnPropertyChanged(nameof(HasItems));
      }
    }
  }

  public bool HasItems => Items.Count > 0;

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public bool IsLoading => State == LoadState.Loading;

  public bool HasError => State == LoadState.Error;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public bool HasMoreAnalytics
  {
    get => hasMoreAnalytics;
    private set => SetProperty(ref hasMoreAnalytics, value);
  }

  public bool HasMoreLinks
  {
    get => hasMoreLinks;
    private set => SetProperty(ref hasMoreLinks, value);
  }

  private string? AnalyticsEndCursor
  {
    get => analyticsEndCursor;
    set => analyticsEndCursor = value;
  }

  private string? LinksEndCursor
  {
    get => linksEndCursor;
    set => linksEndCursor = value;
  }

  public async Task LoadFeedAsync(
      bool mutual = false,
      CancellationToken cancellationToken = default)
  {
    var currentRequest = BeginLoad();
    try
    {
      var response = await referralLinksService
          .FetchFeedAsync(
              new FetchReferralLinksFeedRequest(mutual ? "mutual_follows" : "follow_users"),
              cancellationToken)
          .ConfigureAwait(true);
      CompleteReferralRows(currentRequest, response.Results.Select(RowFromFeed).ToArray());
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }

  public async Task LoadPrioritizedAsync(
      string referralProgramId,
      bool all = false,
      CancellationToken cancellationToken = default)
  {
    var currentRequest = BeginLoad();
    try
    {
      var response = await referralLinksService
          .FetchPrioritizedAsync(
              new FetchPrioritizedReferralLinksRequest(referralProgramId, all),
              cancellationToken)
          .ConfigureAwait(true);
      CompleteReferralRows(currentRequest, response.Links.Select(RowFromPrioritized).ToArray());
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }

  public async Task LoadTrendingProgramsAsync(CancellationToken cancellationToken = default)
  {
    var currentRequest = BeginLoad();
    try
    {
      var response = await referralLinksService
          .FetchTrendingProgramsAsync(new FetchTrendingReferralProgramsRequest(), cancellationToken)
          .ConfigureAwait(true);
      CompleteReferralRows(currentRequest, response.Results.Select(RowFromTrendingProgram).ToArray());
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }
}
