using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.TopicRecommendations;

public sealed partial class TopHashtagsViewModel : ObservableObject
{
  private readonly ITopHashtagsService service;
  private IReadOnlyList<TopHashtag> items = [];
  private IReadOnlyDictionary<string, Topic> topics = new Dictionary<string, Topic>();
  private string query = "";
  private TopHashtagMapping mapping;
  private string? pageQuery;
  private TopHashtagMapping pageMapping;
  private string? endCursor;
  private bool hasMore;
  private bool isLoading;
  private string? errorMessage;
  private CancellationTokenSource? activeLoadCancellation;
  private int loadGeneration;

  public TopHashtagsViewModel(ITopHashtagsService service) =>
      this.service = service ?? throw new ArgumentNullException(nameof(service));

  public IReadOnlyList<TopHashtag> Items { get => items; private set => SetProperty(ref items, value); }
  public IReadOnlyDictionary<string, Topic> Topics { get => topics; private set => SetProperty(ref topics, value); }
  public string Query { get => query; set => SetProperty(ref query, value); }
  public TopHashtagMapping Mapping { get => mapping; set => SetProperty(ref mapping, value); }
  public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
  public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) OnPropertyChanged(nameof(CanMutate)); } }
  public bool CanMutate => !IsLoading;
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      LoadPageAsync(TrimmedOrNull(Query), Mapping, null, false, cancellationToken);
  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      !HasMore || IsLoading ? Task.CompletedTask : LoadPageAsync(pageQuery, pageMapping, endCursor, true, cancellationToken);

  public bool CanUnlink(TopHashtag hashtag)
  {
    ArgumentNullException.ThrowIfNull(hashtag);
    if (string.IsNullOrWhiteSpace(hashtag.TopicId)) return false;
    var normalized = TryNormalizeHashtagSlug(hashtag.Hashtag);
    return !Topics.TryGetValue(hashtag.TopicId, out var topic)
        || normalized is null
        || !string.Equals(normalized, topic.Slug, StringComparison.Ordinal);
  }

  public Task LinkAsync(TopHashtag hashtag, string topicId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(hashtag);
    return MutateAsync(() => service.LinkAsync(topicId, hashtag.TopicAliasId, cancellationToken), cancellationToken);
  }

  public Task UnlinkAsync(TopHashtag hashtag, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(hashtag);
    return string.IsNullOrWhiteSpace(hashtag.TopicId)
        ? Task.CompletedTask
        : MutateAsync(() => service.UnlinkAsync(hashtag.TopicId, hashtag.TopicAliasId, cancellationToken), cancellationToken);
  }

  public Task CreateTopicAsync(TopHashtag hashtag, string name, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(hashtag);
    return MutateAsync(
        () => service.CreateTopicAsync(
            new CreateTopicRequest(name.Trim(), NormalizeHashtagSlug(hashtag.Hashtag), "topic", SourceTopicAliasId: hashtag.TopicAliasId),
            cancellationToken),
        cancellationToken);
  }

  private async Task LoadPageAsync(
      string? requestedQuery,
      TopHashtagMapping requestedMapping,
      string? after,
      bool append,
      CancellationToken cancellationToken)
  {
    var requestGeneration = Interlocked.Increment(ref loadGeneration);
    var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var previousCancellation = Interlocked.Exchange(ref activeLoadCancellation, requestCancellation);
    if (previousCancellation is not null)
    {
      try { await previousCancellation.CancelAsync().ConfigureAwait(true); }
      catch (ObjectDisposedException) { }
    }
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      var response = await service.FetchAsync(requestedQuery, requestedMapping, after, cancellationToken: requestCancellation.Token).ConfigureAwait(true);
      if (!IsCurrent(requestGeneration, requestCancellation)) return;
      Items = append ? Items.Concat(response.Results.Where(row => Items.All(existing => existing.TopicAliasId != row.TopicAliasId))).ToArray() : response.Results;
      Topics = append ? MergeTopics(response.Topics) : response.Topics;
      pageQuery = requestedQuery;
      pageMapping = requestedMapping;
      endCursor = response.PageInfo.EndCursor;
      HasMore = response.PageInfo.HasNextPage;
    }
    catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested) { }
    catch (OperationCanceledException exception)
    {
      if (IsCurrent(requestGeneration, requestCancellation)) ErrorMessage = exception.Message;
    }
    catch (HttpRequestException exception) { if (IsCurrent(requestGeneration, requestCancellation)) ErrorMessage = exception.Message; }
    finally
    {
      if (IsCurrent(requestGeneration, requestCancellation)) IsLoading = false;
      Interlocked.CompareExchange(ref activeLoadCancellation, null, requestCancellation);
      requestCancellation.Dispose();
    }
  }

  private async Task MutateAsync(Func<Task> mutation, CancellationToken cancellationToken)
  {
    if (IsLoading) return;
    IsLoading = true;
    ErrorMessage = null;
    try { await mutation().ConfigureAwait(true); }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
    catch (OperationCanceledException exception) { ErrorMessage = exception.Message; return; }
    catch (HttpRequestException exception) { ErrorMessage = exception.Message; return; }
    finally { IsLoading = false; }
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  private static string? TrimmedOrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private static string NormalizeHashtagSlug(string hashtag)
  {
    return TryNormalizeHashtagSlug(hashtag)
        ?? throw new ArgumentException("A hashtag must contain up to 255 ASCII letters or digits.", nameof(hashtag));
  }

  private static string? TryNormalizeHashtagSlug(string hashtag)
  {
    var authored = hashtag.Trim();
    if (authored.Length is 0 or > 255) return null;
    var value = authored.StartsWith('#') ? authored[1..] : authored;
    var characters = new List<char>(value.Length);
    foreach (var character in value)
    {
      if (character is >= 'A' and <= 'Z') characters.Add((char)(character + ('a' - 'A')));
      else if (character is >= 'a' and <= 'z' or >= '0' and <= '9') characters.Add(character);
      else if (character is '-' or '.' or '_')
      {
        if (characters.Count == 0) return null;
        if (characters[^1] != '-') characters.Add('-');
      }
      else return null;
    }
    if (characters.Count is 0 or > 255 || characters[^1] == '-') return null;
    return new string(characters.ToArray());
  }

  private Dictionary<string, Topic> MergeTopics(IReadOnlyDictionary<string, Topic> additions)
  {
    var merged = new Dictionary<string, Topic>(Topics);
    foreach (var (id, topic) in additions) merged[id] = topic;
    return merged;
  }

  private bool IsCurrent(int requestGeneration, CancellationTokenSource requestCancellation) =>
      requestGeneration == Volatile.Read(ref loadGeneration) && ReferenceEquals(activeLoadCancellation, requestCancellation);
}
