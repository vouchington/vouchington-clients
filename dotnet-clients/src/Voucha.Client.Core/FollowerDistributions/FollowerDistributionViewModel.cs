using Voucha.Client.Core.Api;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.FollowerDistributions;

public enum FollowerDistributionTargetKind { Post, RssFeedItem }

public sealed class FollowerDistributionViewModel : ObservableObject, IDisposable
{
  private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(250);
  private readonly IFriendsService friends;
  private readonly VouchaApiClient client;
  private FollowerDistributionTargetKind targetKind;
  private string targetId;
  private CancellationTokenSource? searchCancellation;
  private IReadOnlyList<User> followers = [];
  private readonly HashSet<string> selectedIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, User> selectedRecipients = new(StringComparer.Ordinal);
  private int searchGeneration;
  private bool isSending;
  private bool isSharing;
  private bool isSearching;
  private bool hasMore;
  private string? nextCursor;
  private string currentUserId = string.Empty;
  private string? query;
  private Exception? error;

  public FollowerDistributionViewModel(
      IFriendsService friends,
      VouchaApiClient client,
      FollowerDistributionTargetKind targetKind,
      string targetId)
  {
    this.friends = friends ?? throw new ArgumentNullException(nameof(friends));
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.targetKind = targetKind;
    this.targetId = string.IsNullOrWhiteSpace(targetId) ? throw new ArgumentException("A target is required.", nameof(targetId)) : targetId;
  }

  public IReadOnlyList<User> Followers { get => followers; private set => SetProperty(ref followers, value); }
  public IReadOnlyCollection<string> SelectedIds => selectedIds;
  public IReadOnlyList<User> SelectedRecipients => selectedRecipients.Values.ToArray();
  public bool IsSending { get => isSending; private set => SetProperty(ref isSending, value); }
  public bool IsSharing { get => isSharing; private set => SetProperty(ref isSharing, value); }
  public bool IsSearching { get => isSearching; private set => SetProperty(ref isSearching, value); }
  public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
  public Exception? Error { get => error; private set => SetProperty(ref error, value); }
  public bool CanSubmit => !IsSending && (!IsSelectedAudience || selectedIds.Count > 0);
  public bool IsSelectedAudience { get; private set; }

  public void SetAudience(bool selected)
  {
    IsSelectedAudience = selected;
    OnPropertyChanged(nameof(IsSelectedAudience));
    OnPropertyChanged(nameof(CanSubmit));
  }

  public bool ToggleSelection(string userId)
  {
    if (!IsSelectedAudience) return false;
    if (selectedIds.Remove(userId)) { selectedRecipients.Remove(userId); ChangedSelection(); return true; }
    if (selectedIds.Count == FollowerDistributionBody.MaximumRecipients) return false;
    selectedIds.Add(userId);
    ChangedSelection();
    return true;
  }

  public bool ToggleSelection(User user)
  {
    ArgumentNullException.ThrowIfNull(user);
    if (!IsSelectedAudience || selectedIds.Contains(user.Id)) return ToggleSelection(user.Id);
    if (selectedIds.Count == FollowerDistributionBody.MaximumRecipients) return false;
    selectedIds.Add(user.Id);
    selectedRecipients[user.Id] = user;
    ChangedSelection();
    return true;
  }

  public async Task SearchFollowersAsync(string currentUserId, string query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);
    Error = null;
    if (searchCancellation is not null) await searchCancellation.CancelAsync().ConfigureAwait(false);
    searchCancellation?.Dispose();
    searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var generation = ++searchGeneration;
    this.currentUserId = currentUserId;
    var normalizedQuery = query.Trim();
    this.query = normalizedQuery.Length == 0 ? null : normalizedQuery;
    Followers = [];
    nextCursor = null;
    HasMore = false;
    IsSearching = false;
    await LoadPageAsync(null, generation, searchCancellation.Token).ConfigureAwait(false);
  }

  public Task LoadMoreFollowersAsync(CancellationToken cancellationToken = default) =>
      HasMore && nextCursor is not null && !IsSearching
          ? LoadPageAsync(nextCursor, searchGeneration, cancellationToken) : Task.CompletedTask;

  public async Task<bool> SubmitAsync(CancellationToken cancellationToken = default)
  {
    if (IsSending || (IsSelectedAudience && selectedIds.Count == 0)) return false;
    IsSending = true; Error = null;
    try
    {
      var body = IsSelectedAudience ? FollowerDistributionBody.Selected(selectedIds.ToArray()) : FollowerDistributionBody.AllFollowers();
      if (targetKind == FollowerDistributionTargetKind.Post)
        await client.SendPostToFollowersAsync(targetId, body, cancellationToken).ConfigureAwait(false);
      else
        await client.SendRssFeedItemToFollowersAsync(targetId, body, cancellationToken).ConfigureAwait(false);
      return true;
    }
    catch (HttpRequestException ex) { Error = ex; return false; }
    finally { IsSending = false; }
  }

  public async Task<bool> ShareAsync(CancellationToken cancellationToken = default)
  {
    if (IsSharing) return false;
    IsSharing = true; Error = null;
    try
    {
      if (targetKind == FollowerDistributionTargetKind.Post)
        await client.SharePostWithFollowersAsync(targetId, cancellationToken).ConfigureAwait(false);
      else await client.ShareRssFeedItemWithFollowersAsync(targetId, cancellationToken).ConfigureAwait(false);
      return true;
    }
    catch (HttpRequestException ex) { Error = ex; return false; }
    finally { IsSharing = false; }
  }

  public void Reset(string nextTargetId)
  {
    if (searchCancellation is not null) _ = searchCancellation.CancelAsync();
    searchGeneration++;
    targetId = nextTargetId;
    Followers = [];
    nextCursor = null;
    HasMore = false;
    selectedIds.Clear();
    selectedRecipients.Clear();
    IsSelectedAudience = false;
    OnPropertyChanged(nameof(SelectedIds));
    OnPropertyChanged(nameof(IsSelectedAudience));
    OnPropertyChanged(nameof(CanSubmit));
  }

  public void ReplaceContext(string nextUserId, FollowerDistributionTargetKind nextTargetKind, string nextTargetId)
  {
    if (currentUserId == nextUserId && targetKind == nextTargetKind && targetId == nextTargetId) return;
    currentUserId = nextUserId; targetKind = nextTargetKind; Reset(nextTargetId); Error = null; query = null;
  }

  private async Task LoadPageAsync(string? after, int generation, CancellationToken cancellationToken)
  {
    IsSearching = true;
    Error = null;
    try
    {
      if (after is null) await Task.Delay(SearchDebounce, cancellationToken).ConfigureAwait(false);
      var response = await friends.FetchFollowersAsync(currentUserId, query, after, cancellationToken).ConfigureAwait(false);
      if (generation != searchGeneration) return;
      Error = null;
      Followers = Followers.Concat(response.Results.Where(user => Followers.All(x => x.Id != user.Id))).ToArray();
      nextCursor = response.PageInfo.EndCursor;
      HasMore = response.PageInfo.HasNextPage && nextCursor is not null;
    }
    catch (HttpRequestException ex) when (generation == searchGeneration) { Error = ex; }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    finally { if (generation == searchGeneration) IsSearching = false; }
  }

  private void ChangedSelection() { OnPropertyChanged(nameof(SelectedIds)); OnPropertyChanged(nameof(SelectedRecipients)); OnPropertyChanged(nameof(CanSubmit)); }

  public void Dispose()
  {
    if (searchCancellation is not null) _ = searchCancellation.CancelAsync();
    searchCancellation?.Dispose();
  }
}
