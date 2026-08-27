using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Fediverse;

public sealed record FediverseInstanceRow(
    string Id,
    string Slug,
    string Name,
    string Software,
    string LocalizedSoftwareLabel,
    bool IsClassified,
    FediverseTrustTier TrustTier,
    string LocalizedTrustLabel)
{
  internal static FediverseInstanceRow From(FediverseInstanceListItem item, IUiLocalization localization)
  {
    var software = string.Join(
        " ",
        new[] { item.Instance?.Software, item.Instance?.SoftwareVersion }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    var trustTier = FediverseTrust.FromElection(item.HostnameElection);
    return new(
        item.Topic.Id,
        item.Topic.Slug,
        item.Topic.Name,
        software,
        software.Length > 0
            ? software
            : localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceUnclassified),
        software.Length > 0,
        trustTier,
        localization.Localize(FediverseTrust.MessageKey(trustTier)));
  }
}
