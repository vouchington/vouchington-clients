using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendRecommendationsViewModel
{
  private async Task MutateAsync(
      FriendRecommendationRow row,
      FriendRecommendationMutation mutation,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(row);
    if (!mutationsInFlight.Add(row.Id)) return;

    var index = recommendations
        .Select((item, itemIndex) => (item, itemIndex))
        .FirstOrDefault(value => StringComparer.Ordinal.Equals(value.item.Id, row.Id))
        .itemIndex;
    var tombstone = new RecommendationTombstone(row.Recommendation, index, Succeeded: false);
    tombstones[row.Id] = tombstone;
    recommendations = recommendations
        .Where(item => !StringComparer.Ordinal.Equals(item.Id, row.Id))
        .ToArray();
    PublishRows();
    ErrorMessage = null;
    try
    {
      if (mutation == FriendRecommendationMutation.Follow)
      {
        await service.FollowAsync(row.Id, cancellationToken).ConfigureAwait(true);
      }
      else
      {
        await service.DismissAsync(row.Id, cancellationToken).ConfigureAwait(true);
      }
      tombstones[row.Id] = tombstone with { Succeeded = true };
      SetMutationCompletionState(LoadState.Loaded);
    }
    catch (OperationCanceledException)
    {
      Restore(tombstone);
    }
    catch (Exception ex) when (
        ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      Restore(tombstone);
      ErrorMessage = ex.Message;
      SetMutationCompletionState(LoadState.Error);
    }
    finally
    {
      mutationsInFlight.Remove(row.Id);
    }
  }

  private void Restore(RecommendationTombstone tombstone)
  {
    tombstones.Remove(tombstone.Recommendation.Id);
    if (recommendations.Any(item =>
        StringComparer.Ordinal.Equals(item.Id, tombstone.Recommendation.Id)))
    {
      return;
    }
    var restored = recommendations.ToList();
    restored.Insert(Math.Clamp(tombstone.Index, 0, restored.Count), tombstone.Recommendation);
    recommendations = restored;
    PublishRows();
    SetMutationCompletionState(LoadState.Loaded);
  }

  private void SetMutationCompletionState(LoadState nextState)
  {
    if (Volatile.Read(ref activePageLoads) == 0) State = nextState;
  }
}
