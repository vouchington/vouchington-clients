using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  private LandingPageProfileLink[] AvailableProfileLinks()
  {
    var used = UsedCandidateIds();
    return Candidates.ProfileLinks.Where(link => !used.ProfileLinks.Contains(link.Id)).ToArray();
  }

  private LandingPageReview[] AvailableReviews()
  {
    var used = UsedCandidateIds();
    return Candidates.Reviews.Where(review => !used.Reviews.Contains(review.Id)).ToArray();
  }

  private LandingPageReferralLink[] AvailableReferralLinks()
  {
    var used = UsedCandidateIds();
    return Candidates.ReferralLinks.Where(link => !used.ReferralLinks.Contains(link.Id)).ToArray();
  }

  private LandingPageTopic[] AvailableTopics()
  {
    var topics = new Dictionary<string, LandingPageTopic>(StringComparer.Ordinal);
    foreach (var review in Candidates.Reviews)
    {
      foreach (var rating in review.ReviewTopicRatings)
      {
        topics[rating.TopicId] = new LandingPageTopic(
            rating.TopicId, rating.TopicName, rating.TopicSlug, "topic");
      }
    }
    foreach (var link in Candidates.ReferralLinks)
    {
      topics[link.ReferralProgramId] = new LandingPageTopic(
          link.ReferralProgramId,
          link.ReferralProgramName,
          link.ReferralProgramSlug,
          "referral_program");
    }
    var used = UsedCandidateIds();
    return topics.Values
        .Where(topic => !used.Topics.Contains(topic.Id))
        .OrderBy(topic => topic.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(topic => topic.Id, StringComparer.Ordinal)
        .ToArray();
  }

  private LandingPageReview[] AvailableGroupReviews()
  {
    if (selectedTopicId is null) return [];
    var used = UsedCandidateIds();
    return Candidates.Reviews.Where(review =>
        !used.Reviews.Contains(review.Id)
        && review.ReviewTopicRatings.Any(rating => rating.TopicId == selectedTopicId)).ToArray();
  }

  private LandingPageReferralLink[] AvailableGroupReferralLinks()
  {
    if (selectedTopicId is null) return [];
    var used = UsedCandidateIds();
    return Candidates.ReferralLinks.Where(link =>
        !used.ReferralLinks.Contains(link.Id)
        && link.ReferralProgramId == selectedTopicId).ToArray();
  }

  private LandingPagePickerOption ProfileOption(LandingPageProfileLink link) =>
      new(link.Id, ProfileLabel(link), localization);

  private LandingPagePickerOption ReviewOption(LandingPageReview review) =>
      new(review.Id, ReviewLabel(review), localization);

  private LandingPagePickerOption ReferralOption(LandingPageReferralLink link) =>
      new(link.Id, ReferralLabel(link), localization);

  private static UiText ProfileLabel(LandingPageProfileLink link) =>
      FirstUserLabel(link.Name, link.Handle, link.Url?.ToString())
      ?? UiText.Localized(UiMessageKey.NativeSwiftLandingPagesProfileLink);

  private static UiText ReviewLabel(LandingPageReview review) =>
      FirstUserLabel(review.Title, review.Markdown.Length > 40 ? review.Markdown[..40] : review.Markdown)
      ?? UiText.Localized(UiMessageKey.NativeSwiftLandingPagesReview);

  private static UiText ReferralLabel(LandingPageReferralLink link) =>
      FirstUserLabel(link.Label, link.ReferralProgramName)
      ?? UiText.Localized(UiMessageKey.NativeSwiftLandingPagesReferralLink);

  private static UiText? FirstUserLabel(params string?[] values)
  {
    foreach (var value in values)
    {
      if (!string.IsNullOrWhiteSpace(value)) return UiText.UserContent(value.Trim());
    }
    return null;
  }

  private UsedLandingPageCandidates UsedCandidateIds()
  {
    var used = new UsedLandingPageCandidates();
    foreach (var item in DraftItems)
    {
      switch (item)
      {
        case LandingPageProfileLinkItem profile:
          used.ProfileLinks.Add(profile.ProfileLink.Id);
          break;
        case LandingPageReviewItem review:
          used.Reviews.Add(review.Review.Id);
          break;
        case LandingPageReferralLinkItem referral:
          used.ReferralLinks.Add(referral.ReferralLink.Id);
          break;
        case LandingPageTopicGroupItem group:
          used.Topics.Add(group.Topic.Id);
          foreach (var entry in group.Entries)
          {
            if (entry is LandingPageReviewItem groupReview) used.Reviews.Add(groupReview.Review.Id);
            if (entry is LandingPageReferralLinkItem groupReferral) used.ReferralLinks.Add(groupReferral.ReferralLink.Id);
          }
          break;
      }
    }
    return used;
  }

  private void ReconcilePickerSelections()
  {
    if (selectedCandidateId is not null
        && !CandidateOptions.Any(option => option.Id == selectedCandidateId))
      selectedCandidateId = null;
    if (selectedTopicId is null) return;
    if (!TopicOptions.Any(option => option.Id == selectedTopicId))
    {
      selectedTopicId = null;
      selectedGroupReviewIds.Clear();
      selectedGroupReferralLinkIds.Clear();
      return;
    }
    selectedGroupReviewIds.IntersectWith(AvailableGroupReviews().Select(review => review.Id));
    selectedGroupReferralLinkIds.IntersectWith(AvailableGroupReferralLinks().Select(link => link.Id));
  }

  private void ClearCandidateAndTopicSelection()
  {
    selectedCandidateId = null;
    selectedTopicId = null;
    selectedGroupReviewIds.Clear();
    selectedGroupReferralLinkIds.Clear();
  }

  private void NotifyCandidateState()
  {
    ReconcilePickerSelections();
    NotifyPickerState();
  }

  private void NotifyDraftItemState()
  {
    OnPropertyChanged(nameof(HasUnsavedItems));
    NotifyCandidateState();
  }

  private sealed class UsedLandingPageCandidates
  {
    public HashSet<string> ProfileLinks { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Reviews { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ReferralLinks { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Topics { get; } = new(StringComparer.Ordinal);
  }
}
