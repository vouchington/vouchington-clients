using System.Text.Json;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

/// <summary>A validated referral-link fact. It is not a hydrated referral card.</summary>
public sealed record McpFeedReferralLink(string Id, string ProgramName, Uri Url);

public sealed record McpFeedReferralLinkReceipt(
    string AccountId, string CallId, IReadOnlyList<McpFeedReferralLink> Links)
{
  internal static McpFeedReferralLinkReceipt? FromValidatedResult(
      string tool, JsonElement arguments, JsonElement structuredContent,
      string accountId, string callId)
  {
    if (tool != "read_feed" || string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(callId) ||
        arguments.ValueKind != JsonValueKind.Object ||
        !arguments.TryGetProperty("option", out var option) || option.GetString() != "referral_links" ||
        structuredContent.ValueKind != JsonValueKind.Object ||
        !structuredContent.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
        !structuredContent.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array ||
        results.GetArrayLength() > 100) return null;
    var links = new List<McpFeedReferralLink>();
    foreach (var row in results.EnumerateArray())
    {
      if (Link(row) is not { } link || links.Exists(existing => existing.Id == link.Id)) return null;
      links.Add(link);
    }
    return new McpFeedReferralLinkReceipt(accountId, callId, links);
  }

  internal static string? Display(
      IReadOnlyList<McpFeedReferralLinkReceipt>? receipts, IUiLocalization? localization)
  {
    if (receipts is not { Count: > 0 }) return null;
    var culture = localization ?? UiLocalization.English;
    var lines = new List<string> { culture.Localize(UiMessageKey.NativeSwiftLandingPagesReferralLinks) };
    foreach (var receipt in receipts)
    {
      if (receipt.Links.Count == 0)
      {
        lines.Add(culture.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults));
        continue;
      }
      foreach (var link in receipt.Links)
      {
        lines.Add(culture.Localize(UiMessageKey.NativeDotnetCsharpDialogsReferralProgram));
        lines.Add(culture.Resolve(UiText.UserContent(link.ProgramName)));
        lines.Add(culture.Localize(UiMessageKey.NativeDotnetCsharpDialogsReferralUrl));
        lines.Add(culture.Resolve(UiText.ProtocolValue(link.Url.OriginalString)));
      }
    }
    return string.Join("\n", lines);
  }

  private static McpFeedReferralLink? Link(JsonElement row)
  {
    if (row.ValueKind != JsonValueKind.Object || !Text(row, "id", out var id) ||
        !Text(row, "user_id", out _) || !Text(row, "referral_program_id", out _) ||
        !Text(row, "referral_program_slug", out _) || !Text(row, "referral_program_name", out var name) ||
        !Text(row, "url", out var url) || !Label(row) || !TrySafeUrl(url, out var uri)) return null;
    var programName = name.Trim();
    return programName.Length == 0 ? null : new McpFeedReferralLink(id, programName, uri);
  }

  private static bool Label(JsonElement row) =>
      row.TryGetProperty("label", out var label) &&
      label.ValueKind is JsonValueKind.Null or JsonValueKind.String;

  private static bool Text(JsonElement row, string name, out string value)
  {
    value = "";
    if (!row.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String ||
        property.GetString() is not { Length: > 0 } text) return false;
    value = text;
    return true;
  }

  private static bool TrySafeUrl(string value, out Uri uri) =>
      Uri.TryCreate(value, UriKind.Absolute, out uri!) && SafeUri(uri);

  internal static bool SafeUri(Uri uri) =>
      uri.IsAbsoluteUri &&
      (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
      !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
}
