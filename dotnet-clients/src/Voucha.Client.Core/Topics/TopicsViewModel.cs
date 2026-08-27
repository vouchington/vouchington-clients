using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Voting;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicsViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly ITopicsService topicsService;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<TopicRow> items = [];
  private TopicRow? selectedTopic;
  private LoadState state = LoadState.Idle;
  private string searchQuery = "";
  private string? errorMessage;
  private int requestId;
  private readonly HashSet<string> votingIds = new(StringComparer.Ordinal);

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public TopicsViewModel(
      ITopicsService topicsService,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.topicsService = topicsService ?? throw new ArgumentNullException(nameof(topicsService));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public string SearchQuery
  {
    get => searchQuery;
    set => SetProperty(ref searchQuery, value);
  }

  public IReadOnlyList<TopicRow> Items
  {
    get => items;
    private set
    {
      if (SetProperty(ref items, value))
      {
        OnPropertyChanged(nameof(HasItems));
      }
    }
  }

  public TopicRow? SelectedTopic
  {
    get => selectedTopic;
    private set => SetProperty(ref selectedTopic, value);
  }

  public bool HasItems => Items.Count > 0;

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

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

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

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      SearchAsync(SearchQuery, cancellationToken);

  public async Task SearchAsync(
      string query,
      CancellationToken cancellationToken = default)
  {
    var currentRequest = BeginLoad();
    try
    {
      var response = await topicsService
          .SearchAsync(query, cancellationToken)
          .ConfigureAwait(true);
      CompleteLoad(
          currentRequest,
          response.Results
              .Select(result => response.Topics.TryGetValue(result.EntityId ?? result.Id ?? "", out var topic)
                  ? RowFrom(
                      topic,
                      ElectionSidecars.TopicElectionFor(response.TopicElections, topic.Id),
                      ElectionSidecars.ElectionVoteFor(response.ElectionVotes, topic.Id))
                  : null)
              .Where(topic => topic is not null)
              .Select(topic => topic!)
              .ToArray());
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }

  public async Task LoadTopicAsync(
      string topicIdOrSlug,
      CancellationToken cancellationToken = default)
  {
    var currentRequest = BeginLoad();
    try
    {
      var response = await topicsService
          .FetchTopicAsync(topicIdOrSlug, cancellationToken)
          .ConfigureAwait(true);
      var row = RowFrom(response.Topic, response.TopicElection, response.ElectionVote);
      SelectedTopic = row;
      CompleteLoad(currentRequest, [row]);
    }
    catch (OperationCanceledException)
    {
      CompleteCanceled(currentRequest);
    }
    catch (VouchaApiException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteError(currentRequest, ex.Message);
    }
  }

  private int BeginLoad()
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    State = LoadState.Loading;
    ErrorMessage = null;
    return currentRequest;
  }

  private void CompleteLoad(int currentRequest, IReadOnlyList<TopicRow> rows)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    Items = rows;
    State = LoadState.Loaded;
  }

  private void CompleteError(int currentRequest, string message)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    Items = [];
    ErrorMessage = message;
    State = LoadState.Error;
  }

  private void CompleteCanceled(int currentRequest)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    State = LoadState.Idle;
  }

}
