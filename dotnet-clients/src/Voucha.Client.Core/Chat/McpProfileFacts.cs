using System.Text.Json;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed record McpProfileBioFact(string Markdown) : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization) =>
      [localization.Localize(UiMessageKey.NativeSwiftProfileBio),
          localization.Resolve(UiText.UserContent(Markdown))];
}

public sealed record McpProfileLink(string LinkType, string Display);

public sealed record McpProfileLinksFact(IReadOnlyList<McpProfileLink> Links) : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization) =>
      [localization.Localize(UiMessageKey.NativeDotnetSettingsProfileLinks),
          .. Links.Select(link => $"{localization.Resolve(UiTaxonomy.ProfileLinkType(link.LinkType))}: " +
              localization.Resolve(UiText.UserContent(link.Display)))];
}

internal static class McpProfileFacts
{
  internal static McpResultFact? Bio(JsonElement content) =>
      McpFactJson.Object(content, "profile", out var profile) &&
      McpFactJson.Text(profile, "id", out var id) && id.Length > 0 &&
      McpFactJson.Text(profile, "markdown", out var markdown) && McpFactJson.Displayable(markdown)
          ? new McpProfileBioFact(markdown) : null;

  internal static McpResultFact? Links(JsonElement content)
  {
    if (!McpFactJson.Array(content, "results", out var rows)) return null;
    var links = new List<McpProfileLink>();
    foreach (var row in rows.EnumerateArray())
    {
      if (!McpFactJson.Text(row, "id", out var id) || id.Length == 0 ||
          !McpFactJson.Text(row, "link_type", out var type) ||
          !UiTaxonomy.ProfileLinkTypes.Any(option => option.ProtocolValue == type) ||
          !McpFactJson.OptionalText(row, "name", out var name) ||
          !McpFactJson.OptionalText(row, "handle", out var handle) ||
          !McpFactJson.OptionalText(row, "url", out var url)) return null;
      if (new[] { name, handle, url }.FirstOrDefault(McpFactJson.Displayable) is { } display)
        links.Add(new McpProfileLink(type, display));
    }

    return links.Count == 0 ? null : new McpProfileLinksFact(links);
  }
}
