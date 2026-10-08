using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Lists;

public sealed record ListSummaryRow(
    string Id,
    string Name,
    string? Description,
    string ProtocolVisibility,
    UiText VisibilityText,
    IUiLocalization Localization,
    PublicContentProvenance? Provenance = null)
{
  public string LocalizedVisibility => Localization.Resolve(VisibilityText);

  public string? LocalizedProvenanceLabel => PublicProvenanceLabels.Resolve(Provenance, Localization);

  public bool HasProvenance => LocalizedProvenanceLabel is not null;

  public static ListSummaryRow FromList(
      UserList list,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(list);
    var localizer = localization ?? UiLocalization.English;
    return new(
        list.Id,
        list.Name,
        list.Description,
        list.Visibility,
        UiTaxonomy.ListVisibility(list.Visibility),
        localizer,
        list.Provenance);
  }

  public ListSummaryRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}

public sealed record ListItemRow(
    string Id,
    string ProtocolItemType,
    UiText ItemTypeText,
    string EntityId,
    string ProtocolMediaType,
    UiText MediaTypeText,
    IUiLocalization Localization)
{
  public string LocalizedItemType => Localization.Resolve(ItemTypeText);

  public string LocalizedMediaType => Localization.Resolve(MediaTypeText);

  public static ListItemRow FromItem(
      ListItem item,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(item);
    var localizer = localization ?? UiLocalization.English;
    var mediaType = item.MediaType ?? "item";
    return new(
        item.Id,
        item.ItemType,
        UiTaxonomy.ListItemType(item.ItemType),
        item.EntityId,
        mediaType,
        UiTaxonomy.MediaType(mediaType),
        localizer);
  }

  public ListItemRow WithLocalization(IUiLocalization localization) =>
      this with { Localization = localization };
}

public enum ListItemFilter
{
  All,
  Reading,
  Watch,
  Listen,
}
