using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private void ApplyInitialCommunityPagination(
      CommunityMembersResponse membersResponse,
      CommunityPostsResponse postsResponse)
  {
    communityListEndCursor = SelectedSection switch
    {
      CommunityDetailSurfaceSection.Members => membersResponse.PageInfo.EndCursor,
      CommunityDetailSurfaceSection.Posts => postsResponse.PageInfo.EndCursor,
      _ => communityListEndCursor,
    };
    OnPropertyChanged(nameof(CanLoadMoreCommunityList));
  }

  private async Task<bool> TryLoadForwardCommunityPageAsync(
      string after,
      int revision,
      int pageRequest,
      string community,
      CommunityDetailSurfaceSection section,
      string? threadId,
      CancellationToken cancellationToken)
  {
    switch (section)
    {
      case CommunityDetailSurfaceSection.Members:
        var members = await service.FetchMembersPageAsync(community, after, 20, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        ApplyMembers(members, append: true);
        communityListEndCursor = members.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.Posts:
        var posts = await service.FetchPostsPageAsync(community, after, 20, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        ApplyPosts(posts, append: true);
        communityListEndCursor = posts.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.News:
        var news = await service.FetchNewsPageAsync(community, after, 25, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        var rows = news.Results
            .Select(reference => NewsRow(reference, news.RssFeedItems, news.RssFeedItemEmbeds))
            .Where(row => row is not null)
            .Select(row => row!);
        News = AppendUnique(News, rows, row => row.Id);
        communityListEndCursor = news.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.Applications:
        var applications = await service.FetchApplicationsPageAsync(community, after, 20, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        var applicationRows = applications.Results
            .Select(reference => reference.Id)
            .Where(id => id is not null && applications.CommunityApplications.ContainsKey(id))
            .Select(id => CommunityApplicationRow.FromApplication(applications.CommunityApplications[id!]));
        ApplicationRows = AppendUnique(ApplicationRows, applicationRows, row => row.Id);
        Applications = AppendUnique(Applications, ApplicationRows.Select(ApplicationSummary), row => row.Id);
        communityListEndCursor = applications.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.Invites:
        var invites = await service.FetchInvitesPageAsync(community, after, 20, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        var inviteRows = invites.Results
            .Select(reference => reference.Id)
            .Where(id => id is not null && invites.CommunityInvites.ContainsKey(id))
            .Select(id => CommunityInviteRow.FromInvite(invites.CommunityInvites[id!], localization));
        InviteRows = AppendUnique(InviteRows, inviteRows, row => row.Id);
        Invites = AppendUnique(Invites, InviteRows.Select(InviteSummary), row => row.Id);
        communityListEndCursor = invites.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.Bans:
        var bans = await service.FetchBansPageAsync(community, after, 20, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        var banRows = bans.Results
            .Select(reference => reference.Id)
            .Where(id => id is not null && bans.CommunityBans.ContainsKey(id))
            .Select(id => CommunityBanRow.FromBan(bans.CommunityBans[id!]));
        BanRows = AppendUnique(BanRows, banRows, row => row.Id);
        Moderation = AppendUnique(Moderation, BanRows.Select(BanSummary), row => row.Id);
        communityListEndCursor = bans.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.Restrictions:
        var restrictions = await service.FetchRestrictionsPageAsync(community, after, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        var restrictionRows = restrictions.Results
            .Select(reference => reference.Id)
            .Where(id => id is not null && restrictions.CommunityRestrictions.ContainsKey(id))
            .Select(id => CommunityRestrictionRow.FromRestriction(restrictions.CommunityRestrictions[id!]));
        RestrictionRows = AppendUnique(RestrictionRows, restrictionRows, row => row.Id);
        Moderation = AppendUnique(Moderation, RestrictionRows.Select(RestrictionSummary), row => row.Id);
        communityListEndCursor = restrictions.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.Modlog:
        var modlog = await service.FetchModlogPageAsync(community, after, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        var modlogRows = modlog.Results
            .Select(reference => reference.Id)
            .Where(id => id is not null && modlog.ModeratorActions.ContainsKey(id))
            .Select(id =>
            {
              var action = modlog.ModeratorActions[id!];
              var actor = action.ActorId is not null && modlog.Users.TryGetValue(action.ActorId, out var user)
                  ? UiText.Verbatim(user.Username ?? user.Name ?? action.ActorId)
                  : action.ActorId is { } actorId
                      ? UiText.Verbatim(actorId)
                      : UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesModeratorAction);
              return Summary(
                  action.Id,
                  UiText.Verbatim(action.ActionType),
                  actor,
                  UiText.Verbatim(action.TargetUserId ?? action.PostId ?? action.ReportId ?? action.CommunityApplicationId ?? action.Id));
            });
        Moderation = AppendUnique(Moderation, modlogRows, row => row.Id);
        communityListEndCursor = modlog.PageInfo.EndCursor;
        return true;
      case CommunityDetailSurfaceSection.Moderation:
        var queue = await service.FetchModerationQueuePageAsync(community, after, 20, cancellationToken).ConfigureAwait(true);
        if (!IsCurrentCommunityPage(revision, pageRequest, community, section, threadId)) return true;
        ModerationRows = AppendUnique(
            ModerationRows,
            queue.Entries.Select(CommunityModerationRow.FromQueueEntry),
            row => row.Id);
        Moderation = AppendUnique(Moderation, ModerationRows.Select(ModerationSummary), row => row.Id);
        communityListEndCursor = queue.PageInfo.EndCursor;
        return true;
      default:
        return false;
    }
  }

  private CommunitySummaryRow ApplicationSummary(CommunityApplicationRow row) =>
      Summary(row.Id, UiText.Verbatim(row.UserId), UiText.Verbatim(row.Status), Verbatim(row.Message));

  private CommunitySummaryRow InviteSummary(CommunityInviteRow row) =>
      Summary(row.Id, row.RecipientText, UiText.Verbatim(row.Status), UiText.Verbatim(row.Code));

  private CommunitySummaryRow BanSummary(CommunityBanRow row) =>
      Summary(row.Id, UiText.Verbatim(row.UserId), UiText.Verbatim(row.Status), Verbatim(row.Reason));

  private CommunitySummaryRow RestrictionSummary(CommunityRestrictionRow row) =>
      Summary(row.Id, UiText.Verbatim(row.RestrictionType), UiText.Verbatim(row.Status), Verbatim(row.Reason));

  private CommunitySummaryRow ModerationSummary(CommunityModerationRow row) =>
      Summary(
          row.Id,
          UiText.Verbatim(row.Target),
          UiText.Verbatim(row.Status),
          Verbatim(row.Reason),
          authoredContent: row.TargetContent);

  private void SetCommunityContinuation(string? endCursor)
  {
    communityListEndCursor = endCursor;
    OnPropertyChanged(nameof(CanLoadMoreCommunityList));
    OnPropertyChanged(nameof(HasMoreCommunityList));
  }
}
