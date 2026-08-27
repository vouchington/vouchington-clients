using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class SupportThreadViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IChatService chatService;
  private readonly List<SupportMessageRow> messages = [];
  private SupportThreadRow? thread;
  private bool hasMoreMessages;
  private bool hasLoaded;
  private string? nextMessageCursor;
  private int requestId;
  private string? errorMessage;
  private LoadState state = LoadState.Idle;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;

  public SupportThreadViewModel(
      IChatService chatService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public SupportThreadRow? Thread
  {
    get => thread;
    private set
    {
      if (SetProperty(ref thread, value))
      {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ConversationId));
      }
    }
  }

  public IReadOnlyList<SupportMessageRow> Messages => messages;

  public string Title => Thread?.DisplaySubject
      ?? localization.Localize(UiMessageKey.NativeDotnetDynamicSupportThread);

  public string? ConversationId => Thread?.ConversationId;

  public bool HasMoreMessages
  {
    get => hasMoreMessages;
    private set => SetProperty(ref hasMoreMessages, value);
  }

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

  public async Task LoadAsync(string threadId, CancellationToken cancellationToken = default)
  {
    await ReloadAsync(threadId, cancellationToken).ConfigureAwait(true);
  }

  public async Task ReloadAsync(string threadId, CancellationToken cancellationToken = default)
  {
    Reset();
    var loaded = await LoadThreadAsync(threadId, cancellationToken).ConfigureAwait(true);
    hasLoaded = loaded;
  }

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (State == LoadState.Loading || Thread is null || !HasMoreMessages || string.IsNullOrWhiteSpace(nextMessageCursor))
    {
      return;
    }

    await LoadThreadPageAsync(Thread.Id, nextMessageCursor, append: true, cancellationToken).ConfigureAwait(true);
  }

  public void Reset()
  {
    _ = Interlocked.Increment(ref requestId);
    messages.Clear();
    Thread = null;
    HasMoreMessages = false;
    nextMessageCursor = null;
    ErrorMessage = null;
    State = LoadState.Idle;
    OnPropertyChanged(nameof(Messages));
  }

  private async Task<bool> LoadThreadAsync(string threadId, CancellationToken cancellationToken)
  {
    return await LoadThreadPageAsync(threadId, null, append: false, cancellationToken).ConfigureAwait(true);
  }

  private async Task<bool> LoadThreadPageAsync(
      string threadId,
      string? after,
      bool append,
      CancellationToken cancellationToken)
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await chatService.FetchSupportThreadAsync(threadId, after, 25, cancellationToken).ConfigureAwait(true);
      if (currentRequest != Volatile.Read(ref requestId)) return false;
      Thread = MapThread(response.Thread);
      if (append)
      {
        messages.InsertRange(0, response.Messages.Select(MapMessage));
      }
      else
      {
        messages.Clear();
        messages.AddRange(response.Messages.Select(MapMessage));
      }

      nextMessageCursor = response.PageInfo.EndCursor;
      HasMoreMessages = (response.PageInfo.HasNextPage || response.PageInfo.HasMore == true) &&
          !string.IsNullOrWhiteSpace(nextMessageCursor);
      OnPropertyChanged(nameof(Messages));
      State = LoadState.Loaded;
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        hasLoaded = false;
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }

    return false;
  }

  private SupportThreadRow MapThread(SupportThread thread) =>
      new(
          thread.Id,
          thread.Subject,
          string.IsNullOrWhiteSpace(thread.Subject)
              ? UiText.Localized(UiMessageKey.NativeDotnetSupportNewThread)
              : UiText.Verbatim(thread.Subject),
          thread.Status.ToWireValue(),
          UiTaxonomy.SupportStatus(thread.Status.ToWireValue()),
          thread.ConversationId,
          thread.CreatedAt,
          thread.UpdatedAt,
          localization);

  private SupportMessageRow MapMessage(SupportMessage message) =>
      new(
          message.Id,
          message.Direction,
          UiTaxonomy.MessageDirection(message.Direction),
          message.BodyText,
          message.CreatedAt,
          localization);

}
