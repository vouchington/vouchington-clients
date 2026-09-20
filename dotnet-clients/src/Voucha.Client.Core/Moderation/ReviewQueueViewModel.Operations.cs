using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ReviewQueueViewModel
{
  private async Task ReplaceAsync(CancellationToken cancellationToken, bool reconcilesMutations = false)
  {
    var generation = BeginListOperation();
    if (!HasItems) State = LoadState.Loading;
    try
    {
      var response = await service.FetchReviewQueueAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
      if (!Accepts(generation, cancellationToken)) return;
      Interlocked.Increment(ref revealContextVersion);
      Items = SupportedRows(response.Results);
      ApplyPageInfo(response);
      State = LoadState.Loaded;
      if (reconcilesMutations) ClearReconciliationRequired();
      await RefreshExposureAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (!Accepts(generation, cancellationToken)) return;
      SetError(UiMessageKey.NativeSwiftModerationReportsReviewQueueLoadFailed);
      if (!HasItems) State = LoadState.Error;
    }
    finally
    {
      if (generation == listGeneration)
      {
        SetListLoading(false);
        if (State == LoadState.Loading) State = HasItems ? LoadState.Loaded : LoadState.Idle;
      }
    }
  }

  private async Task AppendAsync(string cursor, CancellationToken cancellationToken)
  {
    var generation = BeginListOperation();
    try
    {
      var response = await service.FetchReviewQueueAsync(cursor, cancellationToken: cancellationToken).ConfigureAwait(true);
      if (!Accepts(generation, cancellationToken)) return;
      var existingIds = Items.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
      Items = Items.Concat(SupportedRows(response.Results).Where(row => existingIds.Add(row.Id))).ToArray();
      ApplyPageInfo(response);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (!Accepts(generation, cancellationToken)) return;
      SetError(UiMessageKey.NativeSwiftModerationReportsReviewQueueLoadMoreFailed);
    }
    finally
    {
      if (generation == listGeneration) SetListLoading(false);
    }
  }

  private async Task PerformCoreAsync(string postId, PostClearanceAction action, CancellationToken cancellationToken)
  {
    ReplaceRow(postId, row => row with { IsMutating = true });
    SetError(null);
    try
    {
      var responseTask = service.UpdatePostClearanceAsync(postId, action, cancellationToken);
      using var cancellationRegistration = cancellationToken.Register(MarkReconciliationRequired);
      var response = await responseTask.ConfigureAwait(true);
      if (cancellationToken.IsCancellationRequested) return;
      if (response.ClearanceStatus == AdminReviewQueueClearanceStatus.Approved)
      {
        Items = Items.Where(row => row.Id != postId).ToArray();
      }
      else if (response.ClearanceStatus is AdminReviewQueueClearanceStatus.Rejected or AdminReviewQueueClearanceStatus.InReview)
      {
        ReplaceRow(postId, row => row with { ClearanceStatus = response.ClearanceStatus });
      }
      else
      {
        SetError(UiMessageKey.NativeSwiftModerationReportsReviewQueueUnsupportedStatus);
      }
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (cancellationToken.IsCancellationRequested) return;
      SetError(UiMessageKey.NativeSwiftModerationReportsReviewQueueUpdateFailed);
    }
    finally
    {
      ReplaceRow(postId, row => row with { IsMutating = false });
      EndMutation(postId);
    }
  }

  private void ReplaceRow(string postId, Func<ReviewQueueRow, ReviewQueueRow> update) =>
      Items = Items.Select(row => row.Id == postId ? update(row) : row).ToArray();

  private ReviewQueueRow[] SupportedRows(IEnumerable<AdminReviewQueuePost> posts) =>
      posts.Where(post => post.ClearanceStatus is AdminReviewQueueClearanceStatus.Rejected or AdminReviewQueueClearanceStatus.InReview)
          .Select(CreateRow)
          .ToArray();

  private ReviewQueueRow CreateRow(AdminReviewQueuePost post) =>
      new(
          post,
          post.ClearanceStatus,
          localization,
          MediaRows(post),
          isExposureStale,
          exposureState?.InCooldown == true,
          revealedPostIds.Contains(post.Id),
          revealInFlightPostId == post.Id,
          revealInFlightPostId is null && !isExposureRefreshInFlight);

  private ReviewQueueMediaRow[] MediaRows(AdminReviewQueuePost post) =>
      post.MediaReveal.Images
          .OrderBy(image => image.OrderIndex)
          .Select(image => new ReviewQueueMediaRow(
              appConfig.ImageUrlForPlacement(image.PlacementId, image.PlacementRevision, image.ImageId, 960)!,
              UiText.UserContent(image.Caption),
              image.OrderIndex,
              localization))
          .ToArray();

  private void ApplyPageInfo(AdminReviewQueueResponse response)
  {
    endCursor = response.PageInfo.EndCursor;
    HasMore = response.PageInfo.HasNextPage;
  }
}
