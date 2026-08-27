using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PaymentCards;

public sealed partial class PaymentCardsViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly IPaymentCardsService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private PaymentCard[] cards = [];
  private PaymentCardRow[] rows = [];
  private IReadOnlyList<PaymentCardTopic> topicResults = [];
  private PaymentCardTopicOption[] topicRows = [];
  private PageInfo pageInfo = new(null, false, null);
  private PaymentCardDraft? draft;
  private UiText? errorText;
  private UiText? initialLoadError;
  private PaymentCardsErrorOwner errorOwner;
  private bool isLoading;
  private bool isLoadingMore;
  private bool isMutating;
  private int loadGeneration;
  private bool hasLoaded;
  private bool hasContinuationError;
  private readonly HashSet<string> deletedCardIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> locallyCreatedCardIds = new(StringComparer.Ordinal);

  public PaymentCardsViewModel(
      IPaymentCardsService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public IReadOnlyList<PaymentCard> Cards => cards;
  public IReadOnlyList<PaymentCardRow> Rows => rows;
  public IReadOnlyList<PaymentCardTopic> TopicResults
  {
    get => topicResults;
    private set { if (Set(ref topicResults, value)) RebuildTopicRows(); }
  }
  public IReadOnlyList<PaymentCardTopicOption> TopicRows => topicRows;
  public PaymentCardDraft? Draft
  {
    get => draft;
    private set
    {
      if (ReferenceEquals(draft, value)) return;
      if (draft is not null) draft.PropertyChanged -= OnDraftPropertyChanged;
      draft = value;
      if (draft is not null) draft.PropertyChanged += OnDraftPropertyChanged;
      OnPropertyChanged();
      NotifyDraftState();
    }
  }
  public bool HasDraft => Draft is not null;
  public bool HasCards => cards.Length > 0;
  public bool ShowsEmptyState => !IsLoading && !HasCards && !HasError;
  public bool HasNextPage => pageInfo.HasNextPage;
  public UiText? ErrorText => errorText;
  public string? LocalizedErrorMessage => errorText is { } value ? localization.Resolve(value) : null;
  public bool HasError => errorText is not null;
  public bool HasContinuationError { get => hasContinuationError; private set => Set(ref hasContinuationError, value); }
  public bool IsLoading { get => isLoading; private set { if (Set(ref isLoading, value)) OnPropertyChanged(nameof(ShowsEmptyState)); } }
  public bool IsLoadingMore { get => isLoadingMore; private set => Set(ref isLoadingMore, value); }
  public bool IsMutating { get => isMutating; private set => Set(ref isMutating, value); }

  public IReadOnlyList<PaymentCardOption> ParentCandidates => Draft is null ? [] : ParentCandidatesFor(Draft);

  public void BeginEdit(PaymentCardRow row)
  {
    ArgumentNullException.ThrowIfNull(row);
    Draft = new(row.Value, localization.Culture);
  }

  public void CancelEdit() => Draft = null;
  public void Dispose()
  {
    if (draft is not null) draft.PropertyChanged -= OnDraftPropertyChanged;
    localeSubscription?.Dispose();
  }
  public void OnUiLocaleChanged()
  {
    RebuildRows();
    OnPropertyChanged(nameof(ParentCandidates));
    RebuildTopicRows();
    OnPropertyChanged(nameof(LocalizedErrorMessage));
    Draft?.ApplyCulture(localization.Culture);
  }

  private void ReplaceCards(IEnumerable<PaymentCard> values)
  {
    cards = values.Where(card => !deletedCardIds.Contains(card.Id))
        .Select(ClearDeletedParent)
        .GroupBy(card => card.Id, StringComparer.Ordinal).Select(group => group.Last())
        .OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
    RebuildRows();
    OnPropertyChanged(nameof(Cards));
    OnPropertyChanged(nameof(HasCards)); OnPropertyChanged(nameof(ShowsEmptyState));
    OnPropertyChanged(nameof(ParentCandidates));
  }

  private PaymentCard ClearDeletedParent(PaymentCard card)
  {
    if (card.AuthorizedUserOfId is { } parentId && deletedCardIds.Contains(parentId))
      return card.WithoutDeletedParent(parentId);
    return card.AuthorizedUserOfCard is { } parent && deletedCardIds.Contains(parent.Id)
        ? card.WithoutDeletedParent(parent.Id)
        : card;
  }

  private void RebuildRows()
  {
    rows = cards.Select(card => PaymentCardRow.From(card, localization)).ToArray();
    OnPropertyChanged(nameof(Rows));
  }

  private void RebuildTopicRows()
  {
    topicRows = topicResults.Select(topic => PaymentCardTopicOption.From(topic, localization)).ToArray();
    OnPropertyChanged(nameof(TopicRows));
  }

  private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value; OnPropertyChanged(name); return true;
  }

  private void NotifyDraftState()
  {
    OnPropertyChanged(nameof(HasDraft)); OnPropertyChanged(nameof(ParentCandidates));
  }

  private void SetError(UiText? value, PaymentCardsErrorOwner owner = PaymentCardsErrorOwner.General)
  {
    var nextOwner = value is null ? PaymentCardsErrorOwner.None : owner;
    if (errorText == value && errorOwner == nextOwner) return;
    errorText = value;
    errorOwner = nextOwner;
    OnPropertyChanged(nameof(ErrorText));
    OnPropertyChanged(nameof(LocalizedErrorMessage));
    OnPropertyChanged(nameof(HasError));
    OnPropertyChanged(nameof(ShowsEmptyState));
  }

  private void SetFailure(
      Exception error,
      UiMessageKey fallback,
      PaymentCardsErrorOwner owner = PaymentCardsErrorOwner.General)
  {
    var message = error.Message;
    SetError(
        string.IsNullOrEmpty(message) ? UiText.Localized(fallback) : UiText.Verbatim(message),
        owner);
  }

  private void BeginMutationErrorScope()
  {
    HasContinuationError = false;
    SetError(initialLoadError);
  }

  private void CompleteMutationErrorScope() => SetError(initialLoadError);

  private enum PaymentCardsErrorOwner { None, General, Search }

  private void OnDraftPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
  {
    if (eventArgs.PropertyName is nameof(PaymentCardDraft.AuthorizedUserOfId) or nameof(PaymentCardDraft.IsAuthorizedUser))
      OnPropertyChanged(nameof(ParentCandidates));
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
