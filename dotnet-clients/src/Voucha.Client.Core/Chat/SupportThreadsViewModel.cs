using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class SupportThreadsViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly IChatService chatService;
  private readonly IUiLocalization localization;
  private readonly List<SupportThreadRow> threads = [];
  private string? cursor;
  private bool hasMore = true;
  private bool inFlight;
  private int createInFlight;
  private bool hasLoaded;
  private int requestId;
  private string? errorMessage;
  private LoadState state = LoadState.Idle;
  private readonly IDisposable? localeSubscription;

  public SupportThreadsViewModel(
      IChatService chatService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<SupportThreadRow> Threads => threads;

  public bool HasMore
  {
    get => hasMore;
    private set => SetProperty(ref hasMore, value);
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

  public bool IsSubmitting => Volatile.Read(ref createInFlight) != 0;

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (hasLoaded)
    {
      await ReloadAsync(cancellationToken).ConfigureAwait(true);
      hasLoaded = State == LoadState.Loaded;
      return;
    }

    await LoadNextPageAsync(cancellationToken).ConfigureAwait(true);
    hasLoaded = State == LoadState.Loaded;
  }

  public async Task ReloadAsync(CancellationToken cancellationToken = default)
  {
    Reset();
    await LoadNextPageAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task<SupportThreadRow?> CreateThreadAsync(
      string subject,
      string? message = null,
      string? conversationId = null,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(subject);
    var trimmedSubject = subject.Trim();
    if (trimmedSubject.Length == 0) return null;

    if (Interlocked.CompareExchange(ref createInFlight, 1, 0) != 0) return null;
    OnPropertyChanged(nameof(IsSubmitting));
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await chatService.CreateSupportThreadAsync(
          new CreateSupportThreadBody(trimmedSubject, message, conversationId),
          cancellationToken).ConfigureAwait(true);

      var row = MapThread(response.Thread);
      UpsertThread(row, moveToFront: true);
      OnPropertyChanged(nameof(Threads));
      State = LoadState.Loaded;
      return row;
    }
    catch (OperationCanceledException)
    {
      State = threads.Count == 0 ? LoadState.Idle : LoadState.Loaded;
      throw;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
      return null;
    }
    finally
    {
      Interlocked.Exchange(ref createInFlight, 0);
      OnPropertyChanged(nameof(IsSubmitting));
    }
  }

  public void Reset()
  {
    _ = Interlocked.Increment(ref requestId);
    threads.Clear();
    cursor = null;
    HasMore = true;
    inFlight = false;
    ErrorMessage = null;
    State = LoadState.Idle;
    OnPropertyChanged(nameof(Threads));
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

  private void UpsertThread(SupportThreadRow row, bool moveToFront)
  {
    var index = threads.FindIndex(existing => existing.Id == row.Id);
    var targetIndex = index >= 0 ? index : threads.Count;
    if (index >= 0) threads.RemoveAt(index);
    threads.Insert(moveToFront ? 0 : targetIndex, row);
  }

  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(Threads));

  public void Dispose() => localeSubscription?.Dispose();
}
