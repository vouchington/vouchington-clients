using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed record IntegrityEntityTarget(UiText Label, string Id, string? Route);

public sealed record ReportIntegrityRow(
    ReportIntegrityFlag Flag,
    IntegrityEntityTarget Entity,
    UiText ReporterCountText,
    double NewAccountReporterPct,
    string Evidence);

public sealed record VoteIntegrityRow(
    VoteIntegrityFlag Flag,
    IntegrityEntityTarget Entity,
    string Evidence);

internal static class IntegrityFlagPresentation
{
  public static ReportIntegrityRow ReportRow(
      ReportIntegrityFlag flag, IUiLocalization localization) =>
      new(flag, ReportEntity(flag), UiText.Localized(
          UiMessageKey.NativeDotnetResidualReporterCount,
          ("count", flag.ReporterCount)), flag.NewAccountReporterPct,
          Evidence(flag.Details, localization));

  public static VoteIntegrityRow VoteRow(
      VoteIntegrityFlag flag, IUiLocalization localization) =>
      new(flag, VoteEntity(flag), Evidence(flag.Details, localization));

  private static IntegrityEntityTarget ReportEntity(ReportIntegrityFlag flag)
  {
    if (flag.ReportedUserId is { } userId)
      return Target(UiMessageKey.NativeSwiftIntegrityUserId, userId, $"/user/{userId}");
    if (flag.HostnameId is { } hostnameId)
      return Target(UiMessageKey.NativeSwiftIntegrityDomainId, hostnameId, $"/domain/{hostnameId}");
    if (flag.PostId is { } postId) return Target(UiMessageKey.NativeSwiftIntegrityPostId, postId);
    if (flag.RssFeedItemId is { } itemId)
      return Target(UiMessageKey.NativeSwiftIntegrityRssItemId, itemId);
    return Target(UiMessageKey.NativeSwiftIntegrityFlagId, flag.Id);
  }

  private static IntegrityEntityTarget VoteEntity(VoteIntegrityFlag flag)
  {
    if (flag.TopicId is { } topicId)
      return Target(UiMessageKey.NativeSwiftIntegrityTopicId, topicId, $"/topic/{topicId}");
    if (flag.HostnameId is { } hostnameId)
      return Target(UiMessageKey.NativeSwiftIntegrityDomainId, hostnameId, $"/domain/{hostnameId}");
    if (flag.PostId is { } postId) return Target(UiMessageKey.NativeSwiftIntegrityPostId, postId);
    if (flag.RssFeedItemId is { } itemId)
      return Target(UiMessageKey.NativeSwiftIntegrityRssItemId, itemId);
    if (flag.EntityRelationId is { } relationId)
      return Target(UiMessageKey.NativeSwiftIntegrityEntityRelationId, relationId);
    if (flag.AgentModerationId is { } moderationId)
      return Target(UiMessageKey.NativeSwiftIntegrityAgentModerationId, moderationId);
    return Target(UiMessageKey.NativeSwiftIntegrityFlagId, flag.Id);
  }

  private static IntegrityEntityTarget Target(UiMessageKey key, string id, string? route = null) =>
      new(UiText.Localized(key, ("id", id)), id, route);

  private static string Evidence(
      IReadOnlyDictionary<string, JsonElement> details, IUiLocalization localization) =>
      string.Join(
          " · ",
          details.OrderBy(item => item.Key, StringComparer.Ordinal)
              .Select(item => $"{item.Key}: {EvidenceValue(item.Value, localization)}"));

  private static string EvidenceValue(JsonElement value, IUiLocalization localization) =>
      value.ValueKind switch
      {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number when value.TryGetDecimal(out var number) =>
            localization.FormatNumber(number),
        JsonValueKind.Array => $"[{string.Join(", ", value.EnumerateArray()
            .Select(item => EvidenceValue(item, localization)))}]",
        JsonValueKind.Object => $"{{{string.Join(", ", value.EnumerateObject()
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => $"{item.Name}: {EvidenceValue(item.Value, localization)}"))}}}",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => value.GetRawText(),
      };

}
