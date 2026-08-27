using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  private void ApplySelected(LandingPage? page)
  {
    var selectedPageId = SelectedPage?.Id;
    var pageId = page?.Id;
    if (!string.Equals(selectedPageId, pageId, StringComparison.Ordinal))
    {
      ClearSelectedPageAnalytics();
    }
    SelectedPage = page;
    Title = page?.Title ?? "";
    Subtitle = page?.Subtitle ?? "";
    Slug = page?.Slug ?? "";
    DraftItems = page?.Items ?? [];
    if (page is null) ClearBaselines(); else AcceptBaselines(page);
    ResetPickerInputs();
  }

  private void ApplySelectedMetadata(LandingPage page)
  {
    SelectedPage = page with { Items = DraftItems };
    Title = page.Title;
    Subtitle = page.Subtitle ?? "";
    Slug = page.Slug;
    AcceptMetadataBaseline(page);
  }

  private void CommitLoad(
      IReadOnlyList<LandingPage> stagedPages,
      LandingPageCandidates stagedCandidates,
      LandingPage? stagedPage)
  {
    var samePage = stagedPage is not null && stagedPage.Id == SelectedPage?.Id;
    var preserveMetadata = samePage && HasUnsavedMetadata;
    var preserveItems = samePage && HasUnsavedItems;
    var draftTitle = Title;
    var draftSubtitle = Subtitle;
    var draftSlug = Slug;
    var draftContent = DraftItems;
    var previousId = SelectedPage?.Id;

    Pages = stagedPages.Select(LandingPageRow.FromPage).ToArray();
    Candidates = stagedCandidates;
    if (stagedPage is null)
    {
      ApplySelected(null);
      return;
    }
    if (!string.Equals(previousId, stagedPage.Id, StringComparison.Ordinal)) ClearSelectedPageAnalytics();
    persistedMetadataBaseline = LandingPageMetadataBaseline.From(stagedPage);
    persistedItemInputBaseline = Inputs(stagedPage.Items);
    Title = preserveMetadata ? draftTitle : stagedPage.Title;
    Subtitle = preserveMetadata ? draftSubtitle : stagedPage.Subtitle ?? "";
    Slug = preserveMetadata ? draftSlug : stagedPage.Slug;
    DraftItems = preserveItems ? draftContent : stagedPage.Items ?? [];
    SelectedPage = stagedPage with
    {
      Title = Title,
      Subtitle = Subtitle,
      Slug = Slug,
      Items = DraftItems,
    };
    NotifyBaselineState();
    if (samePage)
    {
      ReconcilePickerSelections();
      NotifyPickerState();
    }
    else
    {
      ResetPickerInputs();
    }
  }

  private void ResetPickerInputs()
  {
    addType = LandingPageAddType.Link;
    ClearCandidateAndTopicSelection();
    LinkLabel = "";
    LinkAddressText = "";
    NotifyPickerState();
  }
}
