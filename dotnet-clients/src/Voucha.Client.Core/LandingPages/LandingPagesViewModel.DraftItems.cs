using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  private const int MaxLinkLabelLength = 100;
  private const int MaxLinkUrlLength = 2048;

  public bool AddSelectedItem()
  {
    if (!CanAddItem) return false;
    if (addType == LandingPageAddType.Link)
    {
      if (!Uri.TryCreate(LinkAddressText.Trim(), UriKind.Absolute, out var parsedUrl)) return false;
      var added = AddLink(LinkLabel, parsedUrl);
      if (added)
      {
        LinkLabel = "";
        LinkAddressText = "";
      }
      return added;
    }

    var item = SelectedDraftItem();
    if (item is null) return false;
    DraftItems = DraftItems.Append(item).ToArray();
    ClearCandidateAndTopicSelection();
    NotifyPickerState();
    return true;
  }

  public bool AddLink(string label, string url)
  {
    ArgumentNullException.ThrowIfNull(label);
    ArgumentNullException.ThrowIfNull(url);
    if (!IsContentEditable) return false;
    var trimmedLabel = label.Trim();
    var trimmedUrl = url.Trim();
    if (!IsValidLinkLabel(trimmedLabel) || trimmedUrl.Length > MaxLinkUrlLength) return false;
    if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var parsedUrl)) return false;
    return AddLink(trimmedLabel, parsedUrl);
  }

  public bool AddLink(string label, Uri url)
  {
    ArgumentNullException.ThrowIfNull(label);
    ArgumentNullException.ThrowIfNull(url);
    var trimmedLabel = label.Trim();
    if (!IsContentEditable || !IsValidLinkLabel(trimmedLabel) || !IsValidLinkUrl(url)) return false;
    DraftItems = DraftItems.Append(new LandingPageLinkItem($"draft-link-{Guid.NewGuid()}", trimmedLabel, url)).ToArray();
    return true;
  }

  public void RemoveItem(LandingPageItem item)
  {
    if (!IsContentEditable) return;
    DraftItems = DraftItems.Where(candidate => candidate.Id != item.Id).ToArray();
  }

  public void MoveItem(LandingPageItem item, int direction)
  {
    ArgumentNullException.ThrowIfNull(item);
    if (!IsContentEditable) return;

    var index = -1;
    for (var candidateIndex = 0; candidateIndex < DraftItems.Count; candidateIndex++)
    {
      if (DraftItems[candidateIndex].Id == item.Id)
      {
        index = candidateIndex;
        break;
      }
    }

    var target = index + direction;
    if (index < 0 || target < 0 || target >= DraftItems.Count)
    {
      return;
    }

    var reordered = DraftItems.ToArray();
    (reordered[index], reordered[target]) = (reordered[target], reordered[index]);
    DraftItems = reordered;
  }

  private static bool IsHttpUrlWithoutFragment(Uri url) =>
      url.IsAbsoluteUri
      && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
      && !string.IsNullOrWhiteSpace(url.Host)
      && string.IsNullOrEmpty(url.Fragment);

  private static bool IsValidLinkLabel(string label) =>
      label.Length > 0 && label.Length <= MaxLinkLabelLength;

  private static bool IsValidLinkUrl(Uri url) =>
      url.OriginalString.Trim().Length <= MaxLinkUrlLength && IsHttpUrlWithoutFragment(url);

  private static bool CanAddLinkInput(string label, string url)
  {
    var trimmedLabel = label.Trim();
    var trimmedUrl = url.Trim();
    return IsValidLinkLabel(trimmedLabel)
        && trimmedUrl.Length <= MaxLinkUrlLength
        && Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var parsed)
        && IsValidLinkUrl(parsed);
  }

  private LandingPageItem? SelectedDraftItem() => addType switch
  {
    LandingPageAddType.ProfileLink => AvailableProfileLinks()
        .FirstOrDefault(link => link.Id == selectedCandidateId) is { } profile
        ? new LandingPageProfileLinkItem(DraftId("profile-link"), profile) : null,
    LandingPageAddType.Review => AvailableReviews()
        .FirstOrDefault(review => review.Id == selectedCandidateId) is { } review
        ? new LandingPageReviewItem(DraftId("review"), review) : null,
    LandingPageAddType.ReferralLink => AvailableReferralLinks()
        .FirstOrDefault(link => link.Id == selectedCandidateId) is { } referral
        ? new LandingPageReferralLinkItem(DraftId("referral-link"), referral) : null,
    LandingPageAddType.TopicGroup => SelectedTopicGroupItem(),
    _ => null,
  };

  private LandingPageTopicGroupItem? SelectedTopicGroupItem()
  {
    var topic = AvailableTopics().FirstOrDefault(value => value.Id == selectedTopicId);
    if (topic is null) return null;
    var entries = AvailableGroupReviews()
        .Where(review => selectedGroupReviewIds.Contains(review.Id))
        .Select<LandingPageReview, LandingPageItem>(review =>
            new LandingPageReviewItem(DraftId("group-review"), review))
        .Concat(AvailableGroupReferralLinks()
            .Where(link => selectedGroupReferralLinkIds.Contains(link.Id))
            .Select(link => (LandingPageItem)new LandingPageReferralLinkItem(
                DraftId("group-referral-link"), link)))
        .ToArray();
    return entries.Length == 0
        ? null
        : new LandingPageTopicGroupItem(DraftId("topic-group"), topic, entries);
  }

  private static string DraftId(string type) => $"draft-{type}-{Guid.NewGuid()}";
}
