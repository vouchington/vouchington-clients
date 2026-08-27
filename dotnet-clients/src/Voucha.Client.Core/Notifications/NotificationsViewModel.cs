using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

public sealed partial class NotificationsViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly INotificationsService notificationsService;
  private readonly HashSet<string> locallyReadIds = new(StringComparer.Ordinal);
  private IReadOnlyList<NotificationRow> items = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? cursor;
  private bool hasMore = true;
  private bool inFlight;
  private int requestId;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;

  public NotificationsViewModel(
      INotificationsService notificationsService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.notificationsService = notificationsService ?? throw new ArgumentNullException(nameof(notificationsService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged() =>
      Items = Items.Select(item => item.WithLocalization(localization)).ToArray();

  public IReadOnlyList<NotificationRow> Items
  {
    get => items;
    private set
    {
      if (SetProperty(ref items, value))
      {
        OnPropertyChanged(nameof(CanMarkAllRead));
      }
    }
  }

  public bool CanMarkAllRead => Items.Count > 0;

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

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public bool HasMore
  {
    get => hasMore;
    private set => SetProperty(ref hasMore, value);
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      State == LoadState.Idle && HasMore ? LoadNextPageAsync(cancellationToken) : Task.CompletedTask;

  public async Task LoadNextPageAsync(CancellationToken cancellationToken = default)
  {
    if (inFlight || !HasMore) return;
    var currentRequest = BeginLoad();
    try
    {
      var response = await notificationsService
          .FetchAsync(new FetchNotificationsRequest(cursor), cancellationToken)
          .ConfigureAwait(true);
      CompleteLoad(currentRequest, response);
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
    finally
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        inFlight = false;
      }
    }
  }

  public async Task ReloadAsync(CancellationToken cancellationToken = default)
  {
    Reset();
    await LoadNextPageAsync(cancellationToken).ConfigureAwait(true);
  }

  public void Reset()
  {
    _ = Interlocked.Increment(ref requestId);
    Items = [];
    cursor = null;
    HasMore = true;
    inFlight = false;
    locallyReadIds.Clear();
    ErrorMessage = null;
    State = LoadState.Idle;
  }

  private int BeginLoad()
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    inFlight = true;
    ErrorMessage = null;
    State = LoadState.Loading;
    return currentRequest;
  }

  private void CompleteLoad(int currentRequest, NotificationsResponse response)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    var rows = RowsFrom(response);
    Items = cursor is null ? rows : [.. Items, .. rows];
    cursor = response.PageInfo.EndCursor;
    HasMore = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
    State = LoadState.Loaded;
  }

  private void CompleteError(int currentRequest, string message)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    if (Items.Count == 0)
    {
      Items = [];
    }

    ErrorMessage = message;
    State = LoadState.Error;
  }

  private void CompleteCanceled(int currentRequest)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    State = Items.Count == 0 ? LoadState.Idle : LoadState.Loaded;
  }

}
