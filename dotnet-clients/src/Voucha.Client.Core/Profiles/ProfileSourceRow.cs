using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed record ProfileSourceRow(RssFeedSource Source, IUiLocalization Localization, bool IsStaffViewer)
{
  public string Id => Source.Id;
  public string Title => Source.Title;
  public string? LocalizedProvenanceLabel => PublicProvenanceLabels.Resolve(Source.Provenance, Localization);
  public bool HasProvenance => LocalizedProvenanceLabel is not null;
  public string? StaffChannelLabel => StaffProvenanceLabels.Channel(Source.StaffProvenance, IsStaffViewer, Localization);
  public bool HasStaffChannel => StaffChannelLabel is not null;
}
