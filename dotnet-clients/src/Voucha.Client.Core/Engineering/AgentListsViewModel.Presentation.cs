using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class AgentListsViewModel
{
  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(PaginationActionTitle));
    OnPropertyChanged(nameof(FilterLabels));
    OnPropertyChanged(nameof(FilterKindAccessibilityLabel));
    OnPropertyChanged(nameof(FilterValueAccessibilityLabel));
    OnPropertyChanged(nameof(AgentCreatedAt));
    OnPropertyChanged(nameof(AgentStatusTitle));
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(PaginationErrorMessage));
    Rows = PresentRows(Rows);
  }

  private static string FirstText(params string?[] values) =>
      values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

  private AgentListRow[] PresentRows(IReadOnlyList<AgentListRow> source) => source.Select(row =>
      row.IsAgentDirectoryRow
          ? row with
          {
            Title = RowAgentDisplayName(agentDirectoryUsers.GetValueOrDefault(row.UserId ?? string.Empty), row.UserId),
            Detail = AgentDirectoryDetail(row),
          }
          : row.CreatedAt is { } createdAt
              ? row with
              {
                Title = row.UsesLocalizedUntitledConversationTitle
                    ? localization.Localize(UiMessageKey.NativeSwiftChatConversationTitle)
                    : row.Title,
                Detail = ConversationDetail(agentConversationUsers.GetValueOrDefault(row.UserId ?? string.Empty), row.UserId, createdAt),
              }
              : row).ToArray();

  private string AgentDirectoryDetail(AgentListRow row) =>
      row.CreatedAt is { } createdAt && row.IsActive is { } active
          ? $"{row.AgentType ?? row.Detail} · {localization.Localize(active ? UiMessageKey.NativeSwiftRouteSurfaceAgentStatusActive : UiMessageKey.NativeSwiftRouteSurfaceAgentStatusInactive)} · {TimeZoneInfo.ConvertTime(createdAt, TimeZoneInfo.Local).ToString("d", localization.Culture)}"
          : row.Detail;

  private static string RowAgentDisplayName(PublicUser? user, string? userId) =>
      FirstText(user?.DisplayAccount?.Name, user?.Username, IdPrefix(userId ?? string.Empty));

  private string ConversationDetail(PublicUser? user, string? userId, DateTimeOffset createdAt) =>
      $"{(userId is null ? localization.Localize(UiMessageKey.NativeSwiftPresentationValuesDeleted) : RowAgentDisplayName(user, userId))} · {TimeZoneInfo.ConvertTime(createdAt, TimeZoneInfo.Local).ToString("d", localization.Culture)}";

  private static string IdPrefix(string id) => id[..Math.Min(8, id.Length)];
}
