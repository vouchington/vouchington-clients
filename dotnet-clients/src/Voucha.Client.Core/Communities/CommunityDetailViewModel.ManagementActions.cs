using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public Task<bool> AddListItemAsync(CommunityListItemRequest request, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.AddListItemAsync(communityIdOrSlug, request, cancellationToken), cancellationToken);

  public Task<bool> RemoveListItemAsync(string itemType, string itemId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.RemoveListItemAsync(communityIdOrSlug, itemType, itemId, cancellationToken), cancellationToken);

  public Task<bool> UpdateMemberRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.UpdateMemberRoleAsync(communityIdOrSlug, userId, role, cancellationToken), cancellationToken);

  public Task<bool> RemoveMemberAsync(string userId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.RemoveMemberAsync(communityIdOrSlug, userId, cancellationToken), cancellationToken);

  public Task<bool> TransferOwnershipAsync(string userId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.TransferOwnershipAsync(communityIdOrSlug, userId, cancellationToken), cancellationToken);

  public Task<bool> ReviewApplicationAsync(string applicationId, string status, string? reason = null, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.ReviewApplicationAsync(communityIdOrSlug, applicationId, new UpdateCommunityPostReviewRequest(status, reason), cancellationToken),
          cancellationToken);

  public Task<bool> RevokeInviteAsync(string inviteId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.RevokeInviteAsync(communityIdOrSlug, inviteId, cancellationToken), cancellationToken);

  public Task<bool> UpdatePinnedPostsAsync(IReadOnlyList<string> postIds, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.UpdatePinnedPostsAsync(communityIdOrSlug, new CommunityPinnedPostsUpdateRequest(postIds), cancellationToken), cancellationToken);

  public Task<bool> BanAsync(string userId, string? reason = null, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.BanAsync(communityIdOrSlug, new BanCommunityMemberRequest(userId, reason, expiresAt), cancellationToken), cancellationToken);

  public Task<bool> LiftBanAsync(string userId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.LiftBanAsync(communityIdOrSlug, userId, cancellationToken), cancellationToken);

  public Task<bool> ActivateRestrictionsAsync(
      IReadOnlyList<string> restrictionTypes,
      DateTimeOffset? expiresAt = null,
      string? reason = null,
      CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.ActivateRestrictionsAsync(communityIdOrSlug, new ActivateCommunityRestrictionsRequest(restrictionTypes, expiresAt, reason), cancellationToken),
          cancellationToken);

  public Task<bool> LiftRestrictionAsync(string restrictionId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.LiftRestrictionAsync(communityIdOrSlug, restrictionId, cancellationToken), cancellationToken);

  public Task<bool> SetModeratorVacationAsync(DateTimeOffset? endsAt = null, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.SetModeratorVacationAsync(communityIdOrSlug, endsAt, cancellationToken), cancellationToken);

  public Task<bool> SetSuppressCommunityDigestsWhileOnVacationAsync(bool suppress, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.SetSuppressCommunityDigestsWhileOnVacationAsync(communityIdOrSlug, suppress, cancellationToken),
          cancellationToken);

  public Task<bool> ClearModeratorVacationAsync(CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.ClearModeratorVacationAsync(communityIdOrSlug, cancellationToken), cancellationToken);

  public Task<bool> ClaimModerationReportAsync(string reportId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.ClaimModerationReportAsync(communityIdOrSlug, reportId, cancellationToken), cancellationToken);

  public Task<bool> ReleaseModerationReportAsync(string reportId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.ReleaseModerationReportAsync(communityIdOrSlug, reportId, cancellationToken), cancellationToken);

  public Task<bool> ReviewPostAsync(string postId, string status, string? reason = null, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.ReviewPostAsync(communityIdOrSlug, postId, new UpdateCommunityPostReviewRequest(status, reason), cancellationToken),
          cancellationToken);

  private async Task<bool> MutateAndReloadSectionAsync(
      Func<Task> mutation,
      CancellationToken cancellationToken,
      Func<CancellationToken, Task>? reload = null)
  {
    if (IsLoading || string.IsNullOrWhiteSpace(communityIdOrSlug))
    {
      return false;
    }

    BeginMutation();
    try
    {
      await mutation().ConfigureAwait(true);
      if (reload is null)
      {
        await LoadSelectedSectionAsync(cancellationToken).ConfigureAwait(true);
      }
      else
      {
        await reload(cancellationToken).ConfigureAwait(true);
      }
      CompleteLoad();
      return !HasError;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
      return false;
    }
  }
}
