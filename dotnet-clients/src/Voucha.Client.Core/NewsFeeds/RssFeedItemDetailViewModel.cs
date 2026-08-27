using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public sealed class RssFeedItemDetailViewModel :
    INotifyPropertyChanged,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IRssFeedItemDetailService service;
  private readonly string itemId;
  private readonly NewsFeedItemKind kind;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private RssFeedItemDetail? detail;
  private string? errorMessage;
  private bool isLoading;

  public RssFeedItemDetailViewModel(
      IRssFeedItemDetailService service,
      string itemId,
      NewsFeedItemKind kind,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.itemId = string.IsNullOrWhiteSpace(itemId) ? throw new ArgumentException("An item ID is required.", nameof(itemId)) : itemId;
    this.kind = kind;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public RssFeedItemDetail? Detail
  {
    get => detail;
    private set
    {
      detail = value;
      Changed();
      Changed(nameof(HasDetail));
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
    private set { errorMessage = value; Changed(); Changed(nameof(HasError)); }
  }

  public bool HasError => ErrorMessage is not null;

  public bool IsLoading
  {
    get => isLoading;
    private set { isLoading = value; Changed(); }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Focused feed item errors are presented with retry.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
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

  private void Changed([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
    Changed(nameof(UserContentSource));
    Changed(nameof(UserContentTitle));
    Changed(nameof(ExternalContentSummary));
    Changed(nameof(LocalizedPublishedAt));
    Changed(nameof(LocalizedVoteCounts));
    Changed(nameof(LocalizedSavedState));
    Changed(nameof(LocalizedHiddenState));
  }
}
