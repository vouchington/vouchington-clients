using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationViewModel
{
  private async Task<IReadOnlyList<ModerationRow>> LoadPersonalCasesAsync(
      CancellationToken cancellationToken)
  {
    var apiPath = Context!.ApiPath ??
        throw new InvalidOperationException("Moderation personal case route requires an API path.");
    var response = await service.FetchPersonalCasesAsync(
        apiPath, cancellationToken: cancellationToken).ConfigureAwait(true);
    return [Row(
        Context.Path,
        UiText.Localized(Context.TitleKey),
        UiText.Localized(
            UiMessageKey.SharedCountLabelFormat,
            ("unit", "item"),
            ("count", response.Results.Count)),
        "exclamation-triangle")];
  }

  private async Task<ModerationCursorPage> LoadPersonalCasesPageAsync(
      string? after,
      CancellationToken cancellationToken) => Context!.ApiPath switch
      {
        "/api/v1/my/bans" => PersonalBanPage(
            await service.FetchPersonalCommunityBansAsync(
                after, cancellationToken: cancellationToken).ConfigureAwait(true)),
        "/api/v1/my/removed-posts" => PersonalRemovedPostPage(
            await service.FetchPersonalRemovedPostsAsync(
                after, cancellationToken: cancellationToken).ConfigureAwait(true)),
        _ => throw new InvalidOperationException("Unsupported paged personal moderation route."),
      };

  private ModerationCursorPage PersonalBanPage(PersonalCommunityBansResponse response) => new(
      response.Bans.Select(ban => Row(
          ban.Id,
          UiText.UserContent(ban.CommunitySlug ?? ban.CommunityId),
          UiText.UserContent(ban.Reason ?? localization.Localize(UiMessageKey.NativeDotnetModerationNoReason)),
          "ban")).ToArray(),
      response.PageInfo);

  private ModerationCursorPage PersonalRemovedPostPage(PersonalRemovedPostsResponse response) => new(
      response.RemovedPosts.Select(post => Row(
          $"{post.PostRemovalKind ?? "community"}:{post.PostId}",
          UiText.UserContent(post.PostTitle ?? post.PostId),
          UiText.UserContent(post.PostRemovalKind ?? post.CommunitySlug ?? post.CommunityId ?? post.PostId),
          "file-circle-xmark")).ToArray(),
      response.PageInfo);
}
