using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendRecommendationsViewModel
{
  private async Task LoadPageAsync(
      string? after,
      bool replace,
      CancellationToken cancellationToken)
  {
    var requestId = unchecked(++loadRequestId);
    Interlocked.Increment(ref activePageLoads);
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await service.FetchAsync(after, 25, cancellationToken).ConfigureAwait(true);
      if (requestId != loadRequestId) return;
      MergeUsers(response.Users);
      var visible = response.Results
          .Where(item => !tombstones.ContainsKey(item.Id));
      recommendations = Deduplicate(replace ? visible : [.. recommendations, .. visible]);
      cursor = response.PageInfo.EndCursor;
      HasMore = response.PageInfo.HasMore ?? response.PageInfo.HasNextPage;
      PublishRows();
      State = LoadState.Loaded;
      if (replace) ClearConfirmedTombstones();
    }
    catch (OperationCanceledException)
    {
      if (requestId != loadRequestId) return;
      State = recommendations.Count == 0 ? LoadState.Idle : LoadState.Loaded;
    }
    catch (Exception ex) when (
        ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (requestId != loadRequestId) return;
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
    finally
    {
      Interlocked.Decrement(ref activePageLoads);
    }
  }

  private void MergeUsers(IReadOnlyDictionary<string, User> nextUsers)
  {
    foreach (var (id, user) in nextUsers)
    {
      users[id] = user;
    }
  }

  private static FriendRecommendation[] Deduplicate(
      IEnumerable<FriendRecommendation> values)
  {
    var seen = new HashSet<string>(StringComparer.Ordinal);
    return values.Where(value => seen.Add(value.Id)).ToArray();
  }

  private void ClearConfirmedTombstones()
  {
    foreach (var id in tombstones
        .Where(entry => entry.Value.Succeeded)
        .Select(entry => entry.Key)
        .ToArray())
    {
      tombstones.Remove(id);
    }
  }
}
