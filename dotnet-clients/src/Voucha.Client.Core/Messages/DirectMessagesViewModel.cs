using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IDirectMessagesService messagesService;
  private readonly string? currentUserId;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<DirectConversationRow> conversations = [];
  private IReadOnlyList<DirectMessageRow> messages = [];
  private IReadOnlyList<DirectMessageParticipantRow> participants = [];
  private IReadOnlyList<DirectMessageUserRow> userResults = [];
  private IReadOnlyList<DirectMessageUserRow> participantUserResults = [];
  private LoadState state = LoadState.Idle;
  private LoadState threadState = LoadState.Idle;
  private string? errorMessage;
  private string? threadErrorMessage;
  private string? messageCursor;
  private string? selectedConversationId;
  private string participantAddPolicy = "owner_only";
  private bool hasMoreMessages = true;
  private bool olderMessagesLoading;
  private string? olderMessagesLoadingConversationId;
  private long olderMessagesLoadingGeneration;
  private string? olderMessagesLoadingCursor;
  private bool createConversationLoading;
  private long selectedConversationLoadGeneration;
  private long participantSearchGeneration;
  private readonly HashSet<string> pendingSendConversationIds = [];
  private string latestUserSearchQuery = string.Empty;
  private string latestParticipantUserSearchQuery = string.Empty;

  public DirectMessagesViewModel(
      IDirectMessagesService messagesService,
      string? currentUserId = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.messagesService = messagesService ?? throw new ArgumentNullException(nameof(messagesService));
    this.currentUserId = currentUserId;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ComposerUserResults));
    OnPropertyChanged(nameof(ParticipantUserResults));
    Messages = Messages.ToArray();
    Participants = Participants.ToArray();
  }

  public IReadOnlyList<DirectConversationRow> Conversations
  {
    get => conversations;
    private set => SetProperty(ref conversations, value);
  }

  public IReadOnlyList<DirectMessageRow> Messages
  {
    get => messages;
    private set => SetProperty(ref messages, value);
  }

  public IReadOnlyList<DirectMessageParticipantRow> Participants
  {
    get => participants;
    private set
    {
      if (SetProperty(ref participants, value))
      {
        OnPropertyChanged(nameof(IsOwner));
        OnPropertyChanged(nameof(CanAddParticipants));
      }
    }
  }

  public IReadOnlyList<DirectMessageUserRow> ComposerUserResults
  {
    get => userResults;
    private set => SetProperty(ref userResults, value);
  }

  public IReadOnlyList<DirectMessageUserRow> ParticipantUserResults
  {
    get => participantUserResults;
    private set => SetProperty(ref participantUserResults, value);
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value)) OnPropertyChanged(nameof(IsLoading));
    }
  }

  public LoadState ThreadState
  {
    get => threadState;
    private set
    {
      if (SetProperty(ref threadState, value)) OnPropertyChanged(nameof(IsThreadLoading));
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError));
    }
  }

  public string? ThreadErrorMessage
  {
    get => threadErrorMessage;
    private set
    {
      if (SetProperty(ref threadErrorMessage, value)) OnPropertyChanged(nameof(HasThreadError));
    }
  }

  public string? SelectedConversationId
  {
    get => selectedConversationId;
    private set => SetProperty(ref selectedConversationId, value);
  }

  public string ParticipantAddPolicy
  {
    get => participantAddPolicy;
    private set
    {
      if (SetProperty(ref participantAddPolicy, value)) OnPropertyChanged(nameof(CanAddParticipants));
    }
  }

  public bool HasMoreConversations
  {
    get => hasMoreConversations;
    private set => SetProperty(ref hasMoreConversations, value);
  }

  public bool HasMoreMessages
  {
    get => hasMoreMessages;
    private set => SetProperty(ref hasMoreMessages, value);
  }

  public bool IsLoading => State == LoadState.Loading;

  public bool IsThreadLoading => ThreadState == LoadState.Loading;

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public bool HasThreadError => !string.IsNullOrWhiteSpace(ThreadErrorMessage);
  public bool IsOwner => Participants.Any(row =>
      string.Equals(row.UserId, currentUserId, StringComparison.Ordinal) &&
      string.Equals(row.RoleSource, "owner", StringComparison.Ordinal));

  public bool CanAddParticipants => IsOwner ||
      string.Equals(ParticipantAddPolicy, "all_members", StringComparison.Ordinal);

}
