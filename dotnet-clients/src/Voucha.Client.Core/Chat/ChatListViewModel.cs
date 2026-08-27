using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatListViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IChatService chatService;
  private readonly IUiLocalization localization;
  private readonly List<ChatConversationRow> conversations = [];
  private string? cursor;
  private bool hasMore = true;
  private bool inFlight;
  private bool hasLoaded;
  private int requestId;
  private string? errorMessage;
  private LoadState state = LoadState.Idle;

  private readonly IDisposable? localeSubscription;

  public ChatListViewModel(
      IChatService chatService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<ChatConversationRow> Conversations => conversations;

  public bool HasMore
  {
    get => hasMore;
    private set => SetProperty(ref hasMore, value);
  }

  public bool IsLoading => State == LoadState.Loading;

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

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (hasLoaded) { await ReloadAsync(cancellationToken).ConfigureAwait(true); hasLoaded = State == LoadState.Loaded; return; }

    await LoadNextPageAsync(cancellationToken).ConfigureAwait(true);
    hasLoaded = State == LoadState.Loaded;
  }

  public async Task ReloadAsync(CancellationToken cancellationToken = default)
  {
    Reset();
    await LoadNextPageAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadNextPageAsync(CancellationToken cancellationToken = default)
  {
    if (inFlight || !HasMore) return;
    var currentRequest = BeginLoad();
    try
    {
      var page = await chatService.FetchMyConversationsAsync(cursor, 25, cancellationToken).ConfigureAwait(true);
      if (currentRequest != Volatile.Read(ref requestId)) return;
      Append(page.Results.Select(MapConversation));
      cursor = page.PageInfo.EndCursor;
      HasMore = page.PageInfo.HasNextPage || page.PageInfo.HasMore == true;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        State = conversations.Count == 0 ? LoadState.Idle : LoadState.Loaded;
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }
    finally
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        inFlight = false;
      }
    }
  }

  public async Task DeleteConversationAsync(ChatConversationRow conversation)
  {
    ArgumentNullException.ThrowIfNull(conversation);
    var index = conversations.FindIndex(row => row.Id == conversation.Id);
    if (index < 0) return;

    conversations.RemoveAt(index);
    OnPropertyChanged(nameof(Conversations));
    try
    {
      await chatService.DeleteConversationAsync(conversation.Id).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      conversations.Insert(index, conversation);
      OnPropertyChanged(nameof(Conversations));
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  public void Reset()
  {
    _ = Interlocked.Increment(ref requestId);
    conversations.Clear();
    cursor = null;
    HasMore = true;
    inFlight = false;
    ErrorMessage = null;
    State = LoadState.Idle;
    OnPropertyChanged(nameof(Conversations));
  }

  private int BeginLoad()
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    inFlight = true;
    ErrorMessage = null;
    State = LoadState.Loading;
    return currentRequest;
  }

  private void Append(IEnumerable<ChatConversationRow> items)
  {
    var seenIds = conversations.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
    conversations.AddRange(items.Where(row => seenIds.Add(row.Id)));
    OnPropertyChanged(nameof(Conversations));
  }

  private ChatConversationRow MapConversation(ChatConversation conversation) =>
      new(
          conversation.Id,
          conversation.Title,
          string.IsNullOrWhiteSpace(conversation.Title)
              ? UiText.Localized(UiMessageKey.NativeDotnetDynamicNewChat)
              : UiText.Verbatim(conversation.Title),
          conversation.CreatedAt,
          conversation.UpdatedAt,
          localization,
          conversation.DeletedAt is not null);

  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(Conversations));

  public void Dispose() => localeSubscription?.Dispose();
}
