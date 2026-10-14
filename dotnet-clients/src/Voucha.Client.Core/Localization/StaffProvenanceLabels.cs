using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Localization;

/// <summary>Formats server-supplied raw creation facts only for the current staff viewer.</summary>
public static class StaffProvenanceLabels
{
  public static string? Channel(StaffContentProvenance? provenance, bool isStaffViewer, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    return isStaffViewer && provenance is not null
        ? localization.Format(UiMessageKey.SharedProvenanceChannel, ("channel", UiText.ProtocolValue(provenance.CreatedVia)))
        : null;
  }

  public static string? Client(StaffContentProvenance? provenance, bool isStaffViewer,
      bool showClient, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    var client = isStaffViewer && showClient ? provenance?.OAuthClient : null;
    return client is null ? null : localization.Format(UiMessageKey.SharedProvenanceClient,
        ("name", UiText.UserContent(client.ClientName)), ("clientId", UiText.ProtocolValue(client.ClientId)));
  }

  public static string? Verification(StaffContentProvenance? provenance, bool isStaffViewer,
      bool showClient, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(localization);
    var client = isStaffViewer && showClient ? provenance?.OAuthClient : null;
    return client is null ? null : localization.Localize(client.Verified
        ? UiMessageKey.SharedProvenanceVerified : UiMessageKey.SharedProvenanceUnverified);
  }
}
