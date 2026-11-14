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
          Evidence(flag.Details.Where(item => item.Key != "reporter_user_ids")
              .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), localization));

  public static ReportIntegrityRow ReportDetailRow(
      ReportIntegrityFlag flag, IUiLocalization localization) =>
      ReportRow(flag, localization);

  public static VoteIntegrityRow VoteRow(
      VoteIntegrityFlag flag, IUiLocalization localization) =>
      new(flag, VoteEntity(flag), Evidence(flag.Details, localization));

  private static IntegrityEntityTarget ReportEntity(ReportIntegrityFlag flag)
  {
    if (flag.ReportedUserId is { } userId)
      return Target(flag.EntityLabel, userId, $"/user/{userId}");
    if (flag.HostnameId is { } hostnameId)
      return Target(flag.EntityLabel, hostnameId, $"/domain/{hostnameId}");
    if (flag.PostId is { } postId) return Target(flag.EntityLabel, postId);
    if (flag.RssFeedItemId is { } itemId)
      return Target(flag.EntityLabel, itemId);
    return Target(flag.EntityLabel, flag.Id);
  }

  private static IntegrityEntityTarget VoteEntity(VoteIntegrityFlag flag)
  {
    if (flag.TopicId is { } topicId)
      return Target(flag.EntityLabel, topicId, $"/topic/{topicId}");
    if (flag.HostnameId is { } hostnameId)
      return Target(flag.EntityLabel, hostnameId, $"/domain/{hostnameId}");
    if (flag.PostId is { } postId) return Target(flag.EntityLabel, postId);
    if (flag.RssFeedItemId is { } itemId)
      return Target(flag.EntityLabel, itemId);
    if (flag.EntityRelationId is { } relationId)
      return Target(flag.EntityLabel, relationId);
    if (flag.AgentModerationId is { } moderationId)
      return Target(flag.EntityLabel, moderationId);
    return Target(flag.EntityLabel, flag.Id);
  }

  private static UiText EntityLabel(string? label) => string.IsNullOrWhiteSpace(label)
      ? UiText.Localized(UiMessageKey.NativeDotnetModerationRebasedTargetUnavailable)
      : UiText.UserContent(label);

  private static IntegrityEntityTarget Target(string? label, string id, string? route = null) =>
      new(EntityLabel(label), id, route);

  private static string Evidence(
      IReadOnlyDictionary<string, JsonElement> details, IUiLocalization localization) =>
      string.Join(
          " · ",
          details.Where(item => !item.Key.EndsWith("_id", StringComparison.Ordinal) && !item.Key.EndsWith("_ids", StringComparison.Ordinal)).OrderBy(item => item.Key, StringComparer.Ordinal)
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
