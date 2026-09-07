using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private async Task LoadSavedRepliesCoreAsync(CancellationToken cancellationToken)
  {
    var response = await service.FetchSavedRepliesAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true);
    modmailThreadSurfaceId = null;
    communityListEndCursor = response.PageInfo.EndCursor;
    Moderation = response.Results
        .Select(reply => Summary(
            reply.Id,
            UiText.Verbatim(reply.Title),
            UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesSavedReply),
            UiText.Verbatim(reply.Body)))
        .ToArray();
    OnPropertyChanged(nameof(Moderation));
    OnPropertyChanged(nameof(CanLoadMoreCommunityList));
  }

  private async Task LoadAutomodRecentActionsCoreAsync(
      string? after,
      int? limit,
      string? window,
      string? source,
      CancellationToken cancellationToken)
  {
    var response = await service.FetchAutomodRecentActionsAsync(
        communityIdOrSlug,
        after,
        limit,
        window,
        source,
        cancellationToken).ConfigureAwait(true);
    Moderation = response.AutomodActions
        .Select(action => Summary(
            action.SourceKey,
            UiText.Verbatim(action.AuthoredTitle ?? action.Title),
            UiText.Verbatim(action.CurrentState),
            Verbatim(action.Reason ?? action.PostType)))
        .ToArray();
    OnPropertyChanged(nameof(Moderation));
  }
}
