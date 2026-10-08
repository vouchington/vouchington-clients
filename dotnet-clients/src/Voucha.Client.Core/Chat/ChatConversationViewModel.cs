using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IChatService chatService;
  private readonly IChatProviderResolver providerResolver;
  private readonly ILocalChatProvider localChatProvider;
  private IReadOnlyList<ChatProviderStatus> providerStatuses;
  private readonly List<ChatMessageRow> messages = [];
  private CancellationTokenSource? streamingCts;
  private string? conversationId;
  private string? title;
  private string? errorMessage;
  private bool hasMoreMessages;
  private string? nextMessageCursor;
  private bool isDeleted;
  private bool isStreaming;
  private LoadState state = LoadState.Idle;
  private int requestId;
  private ChatProviderStatus? selectedProviderStatus;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;

  public ChatConversationViewModel(IChatService chatService)
      : this(chatService, UnavailableOnlyChatProviderResolver.Instance, UnavailableLocalChatProvider.Instance)
  {
  }

  public ChatConversationViewModel(
      IChatService chatService,
      IChatProviderResolver providerResolver,
      ILocalChatProvider localChatProvider,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    this.providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
    this.localChatProvider = localChatProvider ?? throw new ArgumentNullException(nameof(localChatProvider));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
    providerStatuses = this.providerResolver.GetProviderStatuses();
    selectedProviderStatus = this.providerResolver.GetDefaultProviderStatus();
  }

  public IReadOnlyList<ChatMessageRow> Messages => messages;

  public IReadOnlyList<ChatProviderStatus> ProviderStatuses => providerStatuses;

  public string? ConversationId
  {
    get => conversationId;
    private set
    {
      if (SetProperty(ref conversationId, value))
      {
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(CanDelete));
      }
    }
  }

  public string Title
  {
    get => title ?? string.Empty;
    private set
    {
      if (SetProperty(ref title, value))
      {
        OnPropertyChanged(nameof(DisplayTitle));
      }
    }
  }

  public string DisplayTitle => string.IsNullOrWhiteSpace(Title)
      ? localization.Localize(UiMessageKey.NativeDotnetDynamicNewChat)
      : Title;

  public ChatProviderStatus SelectedProviderStatus
  {
    get => selectedProviderStatus ?? providerResolver.GetDefaultProviderStatus();
    set
    {
      ArgumentNullException.ThrowIfNull(value);
      if (SetProperty(ref selectedProviderStatus, value))
      {
        if (pendingLocalTurn is { } pending && !SameProviderIdentity(pending.Provider, value))
        {
          pendingLocalTurn = null;
        }
        OnPropertyChanged(nameof(ProviderStatusText));
        OnPropertyChanged(nameof(CanSetUpSelectedWindowsSystemLanguageModel));
      }
    }
  }

  public string ProviderStatusText => SelectedProviderStatus.StatusText;

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
        OnPropertyChanged(nameof(CanSend));
      }
    }
  }

  public bool HasMoreMessages
  {
    get => hasMoreMessages;
    private set
    {
      if (SetProperty(ref hasMoreMessages, value))
      {
        OnPropertyChanged(nameof(ShowLoadOlderMessages));
      }
    }
  }

  public bool IsDeleted
  {
    get => isDeleted;
    private set
    {
      if (SetProperty(ref isDeleted, value))
      {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(CanDelete));
      }
    }
  }

  public bool IsStreaming
  {
    get => isStreaming;
    private set
    {
      if (SetProperty(ref isStreaming, value))
      {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(CanRename));
      }
    }
  }

  public bool CanSend => !IsStreaming && !IsDeleted && State != LoadState.Loading;

  public bool CanRename => !IsDeleted && !IsStreaming && ConversationId is not null;

  public bool CanDelete => !IsDeleted && ConversationId is not null;

}
