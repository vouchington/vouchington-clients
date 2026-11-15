using System.Text.Json;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed record McpRepeatInfringerAccount(
    string? Username, bool IsOperative, bool TerminationInEffect, bool HasOpenReview, int IncidentCount);

/// <summary>One entry per account. Account, incident and review identifiers only group and count.</summary>
public sealed record McpRepeatInfringersFact(IReadOnlyList<McpRepeatInfringerAccount> Accounts) : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization)
  {
    var lines = new List<string> { localization.Localize(UiMessageKey.NativeMcpFactsRepeatInfringersTitle) };
    if (Accounts.Count == 0) lines.Add(localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults));
    foreach (var account in Accounts)
    {
      if (account.Username is not null)
        lines.Add(Row(localization, UiMessageKey.NativeMcpFactsRepeatInfringersAccountUsername,
            localization.Resolve(UiText.UserContent(account.Username))));
      lines.Add(Row(localization, UiMessageKey.NativeMcpFactsRepeatInfringersOperative, YesNo(localization, account.IsOperative)));
      lines.Add(Row(localization, UiMessageKey.NativeMcpFactsRepeatInfringersTerminationInEffect,
          YesNo(localization, account.TerminationInEffect)));
      lines.Add(Row(localization, UiMessageKey.NativeMcpFactsRepeatInfringersOpenReview,
          YesNo(localization, account.HasOpenReview)));
      lines.Add(Row(localization, account.IncidentCount == 1
          ? UiMessageKey.NativeMcpFactsRepeatInfringersIncident : UiMessageKey.NativeMcpFactsRepeatInfringersIncidents,
          localization.FormatNumber(account.IncidentCount)));
    }

    return lines;
  }

  private static string Row(IUiLocalization localization, UiMessageKey label, string value) =>
      $"{localization.Localize(label)}: {value}";

  private static string YesNo(IUiLocalization localization, bool value) => localization.Localize(
      value ? UiMessageKey.NativeSwiftRebasedRouteSurfacesYes : UiMessageKey.NativeSwiftRebasedRouteSurfacesNo);
}

internal static class McpRepeatInfringerFacts
{
  private const int MaximumRows = 500;

  internal static McpResultFact? Accounts(JsonElement content)
  {
    if (!McpFactJson.Array(content, "copyright_repeat_infringer_accounts", out var rows) ||
        rows.GetArrayLength() > MaximumRows) return null;
    var order = new List<string>();
    var grouped = new Dictionary<string, List<Row>>(StringComparer.Ordinal);
    foreach (var element in rows.EnumerateArray())
    {
      if (!Read(element, out var accountId, out var row)) return null;
      if (!grouped.TryGetValue(accountId, out var list)) grouped[accountId] = list = [];
      if (list.Count == 0) order.Add(accountId);
      list.Add(row);
    }

    return new McpRepeatInfringersFact(order.Select(id => Account(grouped[id])).ToArray());
  }

  private static McpRepeatInfringerAccount Account(List<Row> rows) => new(
      rows.Select(row => row.Username).FirstOrDefault(McpFactJson.Displayable),
      rows.Any(row => row.IsOperative), rows.Any(row => row.Termination), rows.Any(row => row.OpenReview),
      rows.Select(row => row.IncidentId).Distinct(StringComparer.Ordinal).Count());

  private static bool Read(JsonElement element, out string accountId, out Row row)
  {
    row = default;
    if (!McpFactJson.Text(element, "account_user_id", out accountId) || !Guid.TryParse(accountId, out _) ||
        !McpFactJson.Text(element, "incident_id", out var incident) || !Guid.TryParse(incident, out _) ||
        !McpFactJson.OptionalText(element, "account_username", out var username) ||
        !McpFactJson.OptionalText(element, "open_review_id", out var review) ||
        !McpFactJson.Bool(element, "is_operative", out var operative) ||
        !McpFactJson.Bool(element, "termination_in_effect", out var termination)) return false;
    row = new Row(username, incident, operative, termination, review is not null);
    return true;
  }

  private readonly record struct Row(
      string? Username, string IncidentId, bool IsOperative, bool Termination, bool OpenReview);
}
