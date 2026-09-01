using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private async Task<bool> TryLoadAdministrativeSurfaceAsync(
      CancellationToken cancellationToken)
  {
    switch (SelectedSection)
    {
      case CommunityDetailSurfaceSection.Bans:
        await LoadBansAsync(cancellationToken).ConfigureAwait(true);
        Moderation = BanRows
            .Select(row => Summary(
                row.Id,
                UiText.Verbatim(row.UserId),
                UiText.Verbatim(row.Status),
                Verbatim(row.Reason)))
            .ToArray();
        return true;
      case CommunityDetailSurfaceSection.Restrictions:
        await LoadRestrictionsAsync(cancellationToken).ConfigureAwait(true);
        Moderation = RestrictionRows
            .Select(row => Summary(
                row.Id,
                UiText.Verbatim(row.RestrictionType),
                UiText.Verbatim(row.Status),
                Verbatim(row.Reason)))
            .ToArray();
        return true;
      case CommunityDetailSurfaceSection.ModeratorVacation:
        await LoadModeratorVacationAsync(cancellationToken).ConfigureAwait(true);
        Moderation = ModeratorVacation is null
            ? []
            : [Summary(
                ModeratorVacation.UserId,
                UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesModeratorVacation),
                UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesActive),
                ModeratorVacation.EndsAt is { } endsAt
                    ? UiText.Verbatim(localization.FormatDateTime(endsAt, TimeZoneInfo.Local))
                    : null)];
        return true;
      case CommunityDetailSurfaceSection.AiAgents:
        await LoadAiAgentsAsync(cancellationToken).ConfigureAwait(true);
        Moderation = AiAgents
            .Select(agent => Summary(
                agent.AgentId,
                UiText.Verbatim(agent.Slug),
                UiText.Localized(agent.Enabled
                    ? UiMessageKey.NativeDotnetCsharpCommunitiesEnabled
                    : UiMessageKey.NativeDotnetCsharpCommunitiesDisabled),
                Verbatim(agent.OnFlagAction)))
            .ToArray();
        return true;
      case CommunityDetailSurfaceSection.AgentPrompts:
        await LoadAgentPromptsAsync(cancellationToken).ConfigureAwait(true);
        Moderation = AgentPrompts
            .Select(prompt => Summary(
                prompt.Id,
                UiText.Verbatim(prompt.AgentId),
                UiText.Localized(prompt.SlotAllocated
                    ? UiMessageKey.NativeDotnetCsharpCommunitiesAllocated
                    : UiMessageKey.NativeDotnetCsharpCommunitiesUnallocated),
                Verbatim(prompt.OnFlagAction)))
            .ToArray();
        return true;
      case CommunityDetailSurfaceSection.Moderation:
        var statsTask = service.FetchModeratorStatsAsync(
            communityIdOrSlug,
            cancellationToken: cancellationToken);
        await Task.WhenAll(
            statsTask,
            LoadModerationQueueAsync(cancellationToken),
            LoadPendingReportsAsync(cancellationToken)).ConfigureAwait(true);
        Moderation = [
          .. (await statsTask.ConfigureAwait(true)).Stats.Select(stat => Summary(
              stat.ActorId,
              UiText.Verbatim(stat.ActorId),
              UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesModeratorStats),
              UiText.Verbatim(localization.FormatNumber(stat.Total)))),
          .. ModerationRows.Select(row => Summary(
              row.Id,
              UiText.Verbatim(row.Target),
              UiText.Verbatim(row.Status),
              Verbatim(row.Reason))),
        ];
        return true;
      default:
        return false;
    }
  }

  private CommunitySummaryRow? NewsRow(
      EntityReference reference,
      IReadOnlyDictionary<string, RssFeedItem> items,
      IReadOnlyDictionary<string, UrlEmbed>? embeds = null)
  {
    var itemId = reference.EntityId ?? reference.Id;
    if (itemId is null || !items.TryGetValue(itemId, out var item))
    {
      return null;
    }

    UrlEmbed? embed = null;
    embeds?.TryGetValue(item.Id, out embed);
    return Summary(
        item.Id,
        (item.Data?.Title ?? item.Title) is { } title
            ? UiText.Verbatim(title)
            : UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesNewsItem),
        UiText.Localized(UiMessageKey.NativeDotnetResidualNews),
        Verbatim(item.RssFeed?.Title ?? item.Data?.ContentSnippet),
        UrlEmbedPreviews.From(embed));
  }

  private CommunitySummaryRow Summary(
      string id,
      UiText title,
      UiText subtitle,
      UiText? detail = null,
      UrlEmbedPreview? embedPreview = null) =>
      new(id, title, subtitle, detail, localization, embedPreview);

  private static UiText? Verbatim(string? value) =>
      value is null ? null : UiText.Verbatim(value);
}
