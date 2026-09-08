namespace Voucha.Client.Core.Pagination;

public sealed record CursorPageRequest
{
  internal CursorPageRequest(string? cursor, int generation, int sequence) =>
      (Cursor, Generation, Sequence) = (cursor, generation, sequence);

  public string? Cursor { get; }
  internal int Generation { get; }
  internal int Sequence { get; }
}

public sealed class CursorPaginationState<T, TId> where TId : notnull
{
  private readonly Func<T, TId> idSelector;
  private int generation;
  private int sequence;
  private CursorPageRequest? inFlightRequest;
  private bool loadedPage;

  public CursorPaginationState(Func<T, TId> idSelector, IEnumerable<T>? items = null)
  {
    this.idSelector = idSelector ?? throw new ArgumentNullException(nameof(idSelector));
    Items = Unique(items ?? []);
    loadedPage = Items.Count > 0;
  }

  public IReadOnlyList<T> Items { get; private set; }
  public bool IsLoading => inFlightRequest is not null;
  public bool HasLoadedPage => loadedPage;
  public bool HasMore { get; private set; } = true;
  public string? EndCursor { get; private set; }
  public string? LastError { get; private set; }
  public bool CanAutomaticallyLoad => HasMore && !IsLoading && LastError is null;

  public CursorPageRequest? BeginInitialPageIfNeeded() =>
      loadedPage || LastError is not null ? null : BeginNextPage();

  public CursorPageRequest? BeginNextPage()
  {
    if (inFlightRequest is not null || !HasMore) return null;
    var request = new CursorPageRequest(EndCursor, generation, unchecked(++sequence));
    inFlightRequest = request;
    LastError = null;
    return request;
  }

  public bool IsCurrent(CursorPageRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);
    return ReferenceEquals(inFlightRequest, request) && request.Generation == generation;
  }

  public bool Complete(
      CursorPageRequest request,
      IEnumerable<T> items,
      string? endCursor,
      bool hasNextPage)
  {
    if (!IsCurrent(request)) return false;
    var existingIds = Items.Select(idSelector).ToHashSet();
    Items = [.. Items, .. Unique(items).Where(item => existingIds.Add(idSelector(item)))];
    EndCursor = endCursor;
    HasMore = hasNextPage;
    loadedPage = true;
    LastError = null;
    inFlightRequest = null;
    return true;
  }

  public bool CompletePrepending(
      CursorPageRequest request,
      IEnumerable<T> items,
      string? endCursor,
      bool hasNextPage)
  {
    if (!IsCurrent(request)) return false;
    var incoming = Unique(items);
    var incomingIds = incoming.Select(idSelector).ToHashSet();
    Items = [.. incoming, .. Items.Where(item => !incomingIds.Contains(idSelector(item)))];
    EndCursor = endCursor;
    HasMore = hasNextPage;
    loadedPage = true;
    LastError = null;
    inFlightRequest = null;
    return true;
  }

  public bool CompleteReplacing(
      CursorPageRequest request,
      IEnumerable<T> items,
      string? endCursor,
      bool hasNextPage)
  {
    if (!IsCurrent(request)) return false;
    Items = Unique(items);
    EndCursor = endCursor;
    HasMore = hasNextPage;
    loadedPage = true;
    LastError = null;
    inFlightRequest = null;
    return true;
  }

  public bool Fail(CursorPageRequest request, string message)
  {
    if (!IsCurrent(request)) return false;
    LastError = message;
    inFlightRequest = null;
    return true;
  }

  public bool Cancel(CursorPageRequest request)
  {
    if (!IsCurrent(request)) return false;
    inFlightRequest = null;
    return true;
  }

  public void Reset(IEnumerable<T>? items = null)
  {
    generation = unchecked(generation + 1);
    Items = Unique(items ?? []);
    loadedPage = Items.Count > 0;
    HasMore = true;
    EndCursor = null;
    LastError = null;
    inFlightRequest = null;
  }

  public void InvalidateRequestsPreservingPage()
  {
    generation = unchecked(generation + 1);
    inFlightRequest = null;
    LastError = null;
  }

  public void Remove(Func<T, bool> predicate) => Items = Items.Where(item => !predicate(item)).ToArray();

  public void ReplaceItems(IEnumerable<T> items) => Items = Unique(items);

  public void RestoreContinuation(string? endCursor, bool hasMore)
  {
    EndCursor = endCursor;
    HasMore = hasMore;
    loadedPage = true;
  }

  private T[] Unique(IEnumerable<T> items)
  {
    var seen = new HashSet<TId>();
    return items.Where(item => seen.Add(idSelector(item))).ToArray();
  }
}
