using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public async Task LoadModmailThreadSurfaceAsync(
      string idOrSlug,
      string threadId,
      CancellationToken cancellationToken = default)
  {
    InvalidateCommunityListRequests();
    SelectedSection = CommunityDetailSurfaceSection.Modmail;
    await LoadAsync(idOrSlug, cancellationToken).ConfigureAwait(true);
    if (State != LoadState.Loaded)
    {
      return;
    }

    try
    {
      var response = await service.FetchModmailMessagesAsync(idOrSlug, threadId, cancellationToken).ConfigureAwait(true);
      modmailThreadSurfaceId = threadId;
      communityListEndCursor = response.PageInfo.EndCursor;
      Moderation = response.Results
          .Select(message => Summary(
              message.Id,
              string.IsNullOrWhiteSpace(message.SenderUsername)
                  ? UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesMessage)
                  : UiText.Verbatim($"@{message.SenderUsername}"),
              UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesModmail),
              UiText.Verbatim(message.BodyText)))
          .ToArray();
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
    }
  }

  private async Task<bool> TryLoadRouteSpecificModerationSurfaceAsync(CancellationToken cancellationToken)
  {
    switch (SelectedSection)
    {
      case CommunityDetailSurfaceSection.Modlog:
        {
          var response = await service.FetchModlogAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true);
          SetCommunityContinuation(response.PageInfo.EndCursor);
          Moderation = response.Results
              .Select(reference => reference.Id)
              .Where(id => id is not null && response.ModeratorActions.ContainsKey(id))
              .Select(id =>
              {
                var action = response.ModeratorActions[id!];
                var actor = action.ActorId is not null && response.Users is not null && response.Users.TryGetValue(action.ActorId, out var user)
                    ? UiText.Verbatim(user.Username ?? user.Name ?? action.ActorId)
                    : action.ActorId is { } actorId
                        ? UiText.Verbatim(actorId)
                        : UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesModeratorAction);
                var target = action.TargetUserId ?? action.PostId ?? action.ReportId ?? action.CommunityApplicationId ?? action.Id;
                return Summary(
                    action.Id,
                    UiText.Verbatim(action.ActionType),
                    actor,
                    UiText.Verbatim(target));
              })
              .ToArray();
          return true;
        }
      case CommunityDetailSurfaceSection.ModerationAnalytics:
        return await TryLoadModerationAnalyticsSurfaceAsync(cancellationToken).ConfigureAwait(true);
      case CommunityDetailSurfaceSection.Modmail:
        {
          var response = await service.FetchModmailAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true);
          modmailThreadSurfaceId = null;
          communityListEndCursor = response.PageInfo.EndCursor;
          Moderation = response.Results
              .Select(thread =>
              {
                var status = thread.ResolvedAt is null ? "open" : "resolved";
                var detail = thread.SubjectUserId ?? thread.AssignedModId;
                return Summary(
                    thread.Id,
                    UiText.Localized(
                        UiMessageKey.NativeDotnetCsharpCommunitiesThread,
                        ("id", thread.Id)),
                    UiText.Verbatim(status),
                    detail is null
                        ? UiText.Verbatim(localization.FormatDateTime(thread.UpdatedAt, TimeZoneInfo.Local))
                        : UiText.Verbatim(detail));
              })
              .ToArray();
          return true;
        }
      default:
        return false;
    }
  }

}
