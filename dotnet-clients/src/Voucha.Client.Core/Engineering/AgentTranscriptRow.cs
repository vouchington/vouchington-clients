using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed record AgentTranscriptRow(string Id, string Role, string Body, string Timestamp)
{
  public static AgentTranscriptRow From(
      AgentConversationMessage message,
      string selectedAgentSystemUserId,
      IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(message);
    ArgumentException.ThrowIfNullOrWhiteSpace(selectedAgentSystemUserId);
    ArgumentNullException.ThrowIfNull(localization);
    return new(
      message.Id,
      message.CreatedById is null
          ? localization.Localize(UiMessageKey.NativeSwiftPresentationValuesDeleted)
          : message.CreatedById == selectedAgentSystemUserId
              ? localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceAgent)
              : localization.Localize(UiMessageKey.NativeSwiftHouseholdsBookmarksUser),
      FirstText(message.Content?.Content, message.Content?.Error) ??
          localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults),
      localization.FormatDateTime(message.CreatedAt, TimeZoneInfo.Local));
  }

  private static string? FirstText(params string?[] values) =>
      values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
