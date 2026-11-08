using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.NewsFeeds;

public sealed class RssFeedItemDetailViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IRssFeedItemDetailService service;
  private readonly string itemId;
  private readonly NewsFeedItemKind kind;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly IProductSurfaceAnalytics? productSignals;
  private bool recordedItemView;
  private RssFeedItemDetail? detail;
  private string? errorMessage;
  private bool isLoading;

  public RssFeedItemDetailViewModel(
      IRssFeedItemDetailService service,
      string itemId,
      NewsFeedItemKind kind,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      IProductSurfaceAnalytics? productSignals = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.productSignals = productSignals;
    this.itemId = string.IsNullOrWhiteSpace(itemId) ? throw new ArgumentException("An item ID is required.", nameof(itemId)) : itemId;
    this.kind = kind;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public RssFeedItemDetail? Detail
  {
    get => detail;
    private set
    {
      detail = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasDetail));
      RaisePresentationChanged();
    }
  }

  public bool HasDetail => Detail is not null;

  public string? UserContentSource => Detail?.Item.Source;

  public string? UserContentTitle => Detail?.Item.Title;

  public string? ExternalContentSummary => Detail?.Item.Summary;

  public string? LocalizedPublishedAt => Detail is { } value
      ? localization.FormatDateTime(value.Item.PublishedAt, TimeZoneInfo.Local)
      : null;

  public string? LocalizedVoteCounts => Detail?.Item is { } item && (item.VoteCountUp is not null || item.VoteCountDown is not null)
      ? localization.Format(
          UiMessageKey.NativeDotnetResidualVotes,
          ("up", item.VoteCountUp ?? 0),
          ("down", item.VoteCountDown ?? 0))
      : null;

  public string? LocalizedSavedState => BooleanState(
      UiMessageKey.NativeSwiftRebasedRouteSurfacesSavedState,
      Detail?.Item.IsSaved);

  public string? LocalizedHiddenState => BooleanState(
      UiMessageKey.NativeSwiftRebasedRouteSurfacesHiddenState,
      Detail?.Item.IsHidden);

  public string? ErrorMessage
  {
    get => errorMessage;
    private set { errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
  }

  public bool HasError => ErrorMessage is not null;

  public bool IsLoading
  {
    get => isLoading;
    private set { isLoading = value; OnPropertyChanged(); }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Focused feed item errors are presented with retry.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    if (!recordedItemView)
    {
      recordedItemView = true;
      productSignals?.RecordRssFeedItemView(itemId);
    }
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      Detail = await service.FetchAsync(itemId, kind, cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

  public void OnUiLocaleChanged() => RaisePresentationChanged();

  public void Dispose() => localeSubscription?.Dispose();

  private string? BooleanState(UiMessageKey key, bool? value) =>
      value is { } state
          ? localization.Format(
              key,
              ("value", UiText.Localized(state
                  ? UiMessageKey.NativeSwiftRebasedRouteSurfacesYes
                  : UiMessageKey.NativeSwiftRebasedRouteSurfacesNo)))
          : null;

  private void RaisePresentationChanged()
  {
    OnPropertyChanged(nameof(UserContentSource));
    OnPropertyChanged(nameof(UserContentTitle));
    OnPropertyChanged(nameof(ExternalContentSummary));
    OnPropertyChanged(nameof(LocalizedPublishedAt));
    OnPropertyChanged(nameof(LocalizedVoteCounts));
    OnPropertyChanged(nameof(LocalizedSavedState));
    OnPropertyChanged(nameof(LocalizedHiddenState));
  }
}
