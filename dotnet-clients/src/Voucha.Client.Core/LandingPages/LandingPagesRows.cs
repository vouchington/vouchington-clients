using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public sealed record LandingPageRow(
    string Id,
    string Title,
    string? Subtitle,
    string Slug,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
  public static LandingPageRow FromPage(LandingPage page)
  {
    ArgumentNullException.ThrowIfNull(page);
    return new(page.Id, page.Title, page.Subtitle, page.Slug, page.IsDefault, page.CreatedAt, page.UpdatedAt);
  }
}

public sealed record LandingPageItemClickRow(
    string ProtocolItemId,
    string ProtocolItemType,
    UiText ItemTypeText,
    int ClickCount,
    IUiLocalization Localization)
{
  public string LocalizedItemType => Localization.Resolve(ItemTypeText);

  public static LandingPageItemClickRow From(
      LandingPageItemClickStats stats,
      IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(stats);
    ArgumentNullException.ThrowIfNull(localization);
    return new(
          stats.ItemId,
          stats.ItemType,
          UiTaxonomy.LandingPageItemType(stats.ItemType),
          stats.ClickCount,
          localization);
  }
}
