using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Households;

public sealed partial class HouseholdInvitationsViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IHouseholdInvitationService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? subscription;
  private readonly CursorPaginationState<HouseholdInvitation, string> pagination = new(item => item.Id);
  private readonly HashSet<string> removed = new(StringComparer.Ordinal);
  private UiMessageKey? message;
  private bool busy;
  private string? householdId;

  public HouseholdInvitationsViewModel(IHouseholdInvitationService service,
      IUiLocalization? localization = null, IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    subscription = localeController?.SubscribeLocaleChanges(this);
  }

  public bool IsSent => householdId is not null;
  public bool IsReceived => !IsSent;
  public bool IsBusy => busy || pagination.IsLoading;
  public bool CanMutate => !IsBusy;
  public bool HasMore => pagination.HasMore;
  public bool HasError => pagination.LastError is not null;
  public bool ShowsEmpty => pagination.HasLoadedPage && Rows.Count == 0;
  public string Message => message is { } key ? localization.Format(key) : string.Empty;
  public string LoadError => HasError ? localization.Format(UiMessageKey.SettingsHouseholdInvitationsLoadFailed) : string.Empty;
  public IReadOnlyList<HouseholdInvitationRow> Rows => pagination.Items
      .Where(item => !removed.Contains(item.Id) && item.AcceptedAt is null && item.DeclinedAt is null && item.RevokedAt is null)
      .Select(item => new HouseholdInvitationRow(item, IsSent, localization)).ToArray();

  public void ConfigureSent(string id)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(id);
    householdId = id;
    pagination.Reset();
    removed.Clear();
    Publish();
  }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    pagination.Reset();
    removed.Clear();
    await LoadMoreAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    var request = pagination.BeginNextPage();
    if (request is null) return;
    Publish();
    try
    {
      var response = householdId is { } id
          ? await service.FetchSentAsync(id, request.Cursor, 25, cancellationToken).ConfigureAwait(true)
          : await service.FetchReceivedAsync(request.Cursor, 25, cancellationToken).ConfigureAwait(true);
      pagination.Complete(request, response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
    }
    catch (OperationCanceledException) { pagination.Cancel(request); }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      pagination.Fail(request, ex.Message);
    }
    Publish();
  }

  public void OnUiLocaleChanged() => Publish();
  public void Dispose() => subscription?.Dispose();

  private void Publish()
  {
    foreach (var property in new[] { nameof(IsSent), nameof(IsReceived), nameof(IsBusy), nameof(CanMutate),
        nameof(HasMore), nameof(HasError), nameof(ShowsEmpty), nameof(Message), nameof(LoadError), nameof(Rows) })
      OnPropertyChanged(property);
  }
}
