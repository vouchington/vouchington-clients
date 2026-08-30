using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.TopicRecommendations;

public sealed partial class TopicRecommendationsListViewModel : ObservableObject
{
  private readonly VouchaApiClient client;
  private readonly object loadGate = new();
  private IReadOnlyList<Post> items = [];
  private Task? activeLoad;
  private string? endCursor;
  private bool hasMore;
  private bool isLoading;
  private string? errorMessage;

  public TopicRecommendationsListViewModel(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public IReadOnlyList<Post> Items { get => items; private set => SetProperty(ref items, value); }
  public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

  public Task LoadAsync(CancellationToken cancellationToken = default) => LoadPageAsync(null, false, cancellationToken);
  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      !HasMore || IsLoading ? Task.CompletedTask : LoadPageAsync(endCursor, true, cancellationToken);

  private Task LoadPageAsync(string? after, bool append, CancellationToken cancellationToken)
  {
    Task load;
    lock (loadGate)
    {
      if (activeLoad is not null) load = activeLoad;
      else
      {
        IsLoading = true;
        ErrorMessage = null;
        load = FetchPageAsync(after, append, cancellationToken);
        activeLoad = load;
      }
    }
    return AwaitActiveLoadAsync(load);
  }

  private async Task AwaitActiveLoadAsync(Task load)
  {
    try
    {
      await load.ConfigureAwait(true);
    }
    finally
    {
      lock (loadGate)
      {
        if (ReferenceEquals(activeLoad, load))
        {
          activeLoad = null;
          IsLoading = false;
        }
      }
    }
  }

  private async Task FetchPageAsync(string? after, bool append, CancellationToken cancellationToken)
  {
    try
    {
      var response = await client.FetchTopicRecommendationsAsync(after, cancellationToken: cancellationToken).ConfigureAwait(true);
      var rows = response.Results
          .Select(reference => reference.EntityId ?? reference.Id)
          .Where(id => id is not null && response.Posts.TryGetValue(id, out _))
          .Select(id => response.Posts[id!]);
      Items = append ? Items.Concat(rows.Where(row => Items.All(existing => existing.Id != row.Id))).ToArray() : rows.ToArray();
      endCursor = response.PageInfo.EndCursor;
      HasMore = response.PageInfo.HasNextPage;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (OperationCanceledException exception) { ErrorMessage = exception.Message; }
    catch (HttpRequestException exception) { ErrorMessage = exception.Message; }
  }
}
