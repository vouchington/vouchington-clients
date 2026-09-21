using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Engineering;

public sealed partial class AgentConversationViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IAgentConversationsService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<AgentConversationMessage> messages = [];
  private AgentConversation? conversation;
  private bool isLoading;
  private bool isLoadingOlder;
  private bool hasMore;
  private string? errorMessage;
  private string? paginationErrorMessage;
  private UiText? localizedErrorText;
  private UiText? localizedPaginationErrorText;
  private string? agentIdOrSlug;
  private string? conversationId;
  private string? nextCursor;
  private string? agentSystemUserId;
  private int contextGeneration;
  private int pageRequestGeneration;

  public AgentConversationViewModel(
      IAgentConversationsService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public AgentConversation? Conversation
  {
    get => conversation;
    private set
    {
      if (SetProperty(ref conversation, value))
      {
        OnPropertyChanged(nameof(ConversationDisplayTitle));
        OnPropertyChanged(nameof(ConversationCreatedAt));
      }
    }
  }

  public string ConversationDisplayTitle => Conversation is { Title: var title } && !string.IsNullOrWhiteSpace(title)
      ? title
      : localization.Localize(UiMessageKey.NativeSwiftChatConversationTitle);
  public string? ConversationCreatedAt => Conversation is { } conversation
      ? localization.FormatDateTime(conversation.CreatedAt, TimeZoneInfo.Local)
      : null;

  public IReadOnlyList<AgentConversationMessage> Messages
  {
    get => messages;
    private set
    {
      if (SetProperty(ref messages, value)) { OnPropertyChanged(nameof(MessageRows)); OnPropertyChanged(nameof(IsEmpty)); }
    }
  }
  public IReadOnlyList<AgentTranscriptRow> MessageRows => agentSystemUserId is { } selectedAgentSystemUserId
      ? Messages.Select(message => AgentTranscriptRow.From(message, selectedAgentSystemUserId, localization)).ToArray()
      : [];

  public bool IsLoading
  {
    get => isLoading;
    private set
    {
      if (SetProperty(ref isLoading, value)) { OnPropertyChanged(nameof(CanLoadOlder)); OnPropertyChanged(nameof(IsEmpty)); }
    }
  }

  public bool IsLoadingOlder
  {
    get => isLoadingOlder;
    private set
    {
      if (SetProperty(ref isLoadingOlder, value)) OnPropertyChanged(nameof(CanLoadOlder));
    }
  }

  public bool HasMore
  {
    get => hasMore;
    private set
    {
      if (SetProperty(ref hasMore, value)) OnPropertyChanged(nameof(CanLoadOlder));
    }
  }

  public bool CanLoadOlder => HasMore && !IsLoadingOlder && !IsLoading;
  public bool IsEmpty => !IsLoading && ErrorMessage is null && Messages.Count == 0;

  public string? ErrorMessage
  {
    get => localizedErrorText is { } text ? localization.Resolve(text) : errorMessage;
    private set
    {
      var hadLocalizedText = localizedErrorText is not null;
      localizedErrorText = null;
      if (hadLocalizedText || SetProperty(ref errorMessage, value)) { OnPropertyChanged(nameof(ErrorMessage)); OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(IsEmpty)); }
    }
  }
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public string? PaginationErrorMessage
  {
    get => localizedPaginationErrorText is { } text ? localization.Resolve(text) : paginationErrorMessage;
    private set
    {
      var hadLocalizedText = localizedPaginationErrorText is not null;
      localizedPaginationErrorText = null;
      if (hadLocalizedText || SetProperty(ref paginationErrorMessage, value)) { OnPropertyChanged(nameof(PaginationErrorMessage)); OnPropertyChanged(nameof(PaginationActionTitle)); }
    }
  }

  public string PaginationActionTitle => localization.Localize(
      PaginationErrorMessage is null
          ? UiMessageKey.NativeSwiftDirectMessagesLoadOlderMessages
          : UiMessageKey.NativeDotnetEngineeringPaginationRetry);

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(PaginationActionTitle));
    OnPropertyChanged(nameof(MessageRows));
    OnPropertyChanged(nameof(ConversationDisplayTitle));
    OnPropertyChanged(nameof(ConversationCreatedAt));
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(PaginationErrorMessage));
  }

  private void SetLocalizedError(UiText? text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
    OnPropertyChanged(nameof(IsEmpty));
  }

  private void SetLocalizedPaginationError(UiText? text)
  {
    localizedPaginationErrorText = text;
    paginationErrorMessage = null;
    OnPropertyChanged(nameof(PaginationErrorMessage));
    OnPropertyChanged(nameof(PaginationActionTitle));
  }

  public void Dispose() => localeSubscription?.Dispose();
}
