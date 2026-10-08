using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Localization;

public static class PublicProvenanceLabels
{
  public static string? Resolve(PublicContentProvenance? provenance, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    if (provenance is null) return null;
    var channel = localization.Localize(provenance.Via == "mcp"
        ? UiMessageKey.SharedProvenanceViaMcp
        : UiMessageKey.SharedProvenanceViaApi);
    var appName = provenance.App?.Kind switch
    {
      "hostname" => provenance.App.Hostname,
      "verified" => provenance.App.ClientName,
      _ => null, // The reviewed known-app catalog currently has no entries.
    };
    return string.IsNullOrWhiteSpace(appName)
        ? channel
        : localization.Format(UiMessageKey.SharedProvenanceViaApp, ("app", appName));
  }
}
