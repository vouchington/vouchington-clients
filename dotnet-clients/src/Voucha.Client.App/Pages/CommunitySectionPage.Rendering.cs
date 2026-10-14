using Voucha.Client.Core.Communities;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private static bool AffectsRenderedSurface(string? propertyName) =>
      propertyName is null or nameof(CommunityDetailViewModel.Community) or nameof(CommunityDetailViewModel.State) or
          nameof(CommunityDetailViewModel.ErrorMessage) or nameof(CommunityDetailViewModel.IsArchived) or
          nameof(CommunityDetailViewModel.CanJoin) or nameof(CommunityDetailViewModel.CanLeave) or
          nameof(CommunityDetailViewModel.CanArchive) or nameof(CommunityDetailViewModel.CanUnarchive) or
          nameof(CommunityDetailViewModel.CanModerateCommunity) or nameof(CommunityDetailViewModel.CanManageCommunity) or
          nameof(CommunityDetailViewModel.CanManageMembers) or
          nameof(CommunityDetailViewModel.CanUseModmail) or
          nameof(CommunityDetailViewModel.CanViewRawModerationAnalytics) or
          nameof(CommunityDetailViewModel.CanLoadMoreCommunityList) or
          nameof(CommunityDetailViewModel.CommunityListPaginationErrorMessage) or
          nameof(CommunityDetailViewModel.TransparencyPaginationError) or
          nameof(CommunityDetailViewModel.Members) or
          nameof(CommunityDetailViewModel.Posts) or nameof(CommunityDetailViewModel.ListItemCounts) or
          nameof(CommunityDetailViewModel.News) or nameof(CommunityDetailViewModel.PinnedPosts) or
          nameof(CommunityDetailViewModel.Applications) or nameof(CommunityDetailViewModel.Invites) or
          nameof(CommunityDetailViewModel.Moderation);

}
