using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  private LandingPageMetadataBaseline persistedMetadataBaseline = LandingPageMetadataBaseline.Empty;
  private IReadOnlyList<LandingPageItemInput> persistedItemInputBaseline = [];
  private string title = "";
  private string subtitle = "";
  private string slug = "";

  public string Title
  {
    get => title;
    set
    {
      if (SetProperty(ref title, value)) OnPropertyChanged(nameof(HasUnsavedMetadata));
    }
  }

  public string Subtitle
  {
    get => subtitle;
    set
    {
      if (SetProperty(ref subtitle, value)) OnPropertyChanged(nameof(HasUnsavedMetadata));
    }
  }

  public string Slug
  {
    get => slug;
    set
    {
      if (SetProperty(ref slug, value)) OnPropertyChanged(nameof(HasUnsavedMetadata));
    }
  }

  public bool HasUnsavedMetadata =>
      new LandingPageMetadataBaseline(Title, Subtitle, Slug) != persistedMetadataBaseline;

  public bool HasUnsavedItems =>
      !InputsEqual(DraftItems.Select(item => item.ToInput()).ToArray(), persistedItemInputBaseline);

  private void AcceptBaselines(LandingPage page)
  {
    persistedMetadataBaseline = LandingPageMetadataBaseline.From(page);
    persistedItemInputBaseline = Inputs(page.Items);
    NotifyBaselineState();
  }

  private void AcceptMetadataBaseline(LandingPage page)
  {
    persistedMetadataBaseline = LandingPageMetadataBaseline.From(page);
    NotifyBaselineState();
  }

  private void AcceptItemBaseline(LandingPage page)
  {
    persistedItemInputBaseline = Inputs(page.Items);
    NotifyBaselineState();
  }

  private void ClearBaselines()
  {
    persistedMetadataBaseline = LandingPageMetadataBaseline.Empty;
    persistedItemInputBaseline = [];
    NotifyBaselineState();
  }

  private void NotifyBaselineState()
  {
    OnPropertyChanged(nameof(HasUnsavedMetadata));
    OnPropertyChanged(nameof(HasUnsavedItems));
  }

  private static LandingPageItemInput[] Inputs(IReadOnlyList<LandingPageItem>? items) =>
      items?.Select(item => item.ToInput()).ToArray() ?? [];

  private static bool InputsEqual(
      IReadOnlyList<LandingPageItemInput> left,
      IReadOnlyList<LandingPageItemInput> right)
  {
    if (left.Count != right.Count) return false;
    for (var index = 0; index < left.Count; index++)
    {
      if (!InputEqual(left[index], right[index])) return false;
    }
    return true;
  }

  private static bool InputEqual(LandingPageItemInput left, LandingPageItemInput right) =>
      string.Equals(left.Type, right.Type, StringComparison.Ordinal)
      && string.Equals(left.ProfileLinkId, right.ProfileLinkId, StringComparison.Ordinal)
      && string.Equals(left.ReviewId, right.ReviewId, StringComparison.Ordinal)
      && string.Equals(left.ReferralLinkId, right.ReferralLinkId, StringComparison.Ordinal)
      && string.Equals(left.TopicId, right.TopicId, StringComparison.Ordinal)
      && string.Equals(left.Label, right.Label, StringComparison.Ordinal)
      && string.Equals(left.Url?.OriginalString, right.Url?.OriginalString, StringComparison.Ordinal)
      && InputsEqual(left.Entries ?? [], right.Entries ?? []);

  private readonly record struct LandingPageMetadataBaseline(string Title, string Subtitle, string Slug)
  {
    public static LandingPageMetadataBaseline Empty { get; } = new("", "", "");
    public static LandingPageMetadataBaseline From(LandingPage page) =>
        new(page.Title, page.Subtitle ?? "", page.Slug);
  }
}
