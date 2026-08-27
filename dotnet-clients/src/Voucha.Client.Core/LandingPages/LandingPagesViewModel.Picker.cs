using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  private LandingPageAddType addType = LandingPageAddType.Link;
  private string? selectedCandidateId;
  private string? selectedTopicId;
  private readonly HashSet<string> selectedGroupReviewIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> selectedGroupReferralLinkIds = new(StringComparer.Ordinal);
  private string linkLabel = "";
  private string linkAddressText = "";

  public IReadOnlyList<LandingPageItemTypeOption> ItemTypeOptions { get; private set; } = [];

  public LandingPageItemTypeOption SelectedItemTypeOption
  {
    get => ItemTypeOptions.Single(option => option.Type == addType);
    set
    {
      ArgumentNullException.ThrowIfNull(value);
      if (addType == value.Type) return;
      addType = value.Type;
      ClearCandidateAndTopicSelection();
      NotifyPickerState();
    }
  }

  public IReadOnlyList<LandingPagePickerOption> CandidateOptions => addType switch
  {
    LandingPageAddType.ProfileLink => AvailableProfileLinks().Select(ProfileOption).ToArray(),
    LandingPageAddType.Review => AvailableReviews().Select(ReviewOption).ToArray(),
    LandingPageAddType.ReferralLink => AvailableReferralLinks().Select(ReferralOption).ToArray(),
    _ => [],
  };

  public LandingPagePickerOption? SelectedCandidateOption
  {
    get => CandidateOptions.FirstOrDefault(option => option.Id == selectedCandidateId);
    set
    {
      var id = value?.Id;
      if (string.Equals(selectedCandidateId, id, StringComparison.Ordinal)) return;
      selectedCandidateId = id;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanAddItem));
    }
  }

  public IReadOnlyList<LandingPagePickerOption> TopicOptions =>
      AvailableTopics().Select(topic => new LandingPagePickerOption(topic.Id, UiText.UserContent(topic.Name), localization)).ToArray();

  public LandingPagePickerOption? SelectedTopicOption
  {
    get => TopicOptions.FirstOrDefault(option => option.Id == selectedTopicId);
    set
    {
      var id = value?.Id;
      if (string.Equals(selectedTopicId, id, StringComparison.Ordinal)) return;
      selectedTopicId = id;
      selectedGroupReviewIds.Clear();
      selectedGroupReferralLinkIds.Clear();
      NotifyGroupState();
    }
  }

  public IReadOnlyList<LandingPageGroupMemberOption> GroupReviewOptions =>
      AvailableGroupReviews()
          .Select(review => new LandingPageGroupMemberOption(
              review.Id, LandingPageGroupMemberType.Review, ReviewLabel(review),
              selectedGroupReviewIds.Contains(review.Id), localization))
          .ToArray();

  public IReadOnlyList<LandingPageGroupMemberOption> GroupReferralLinkOptions =>
      AvailableGroupReferralLinks()
          .Select(link => new LandingPageGroupMemberOption(
              link.Id, LandingPageGroupMemberType.ReferralLink, ReferralLabel(link),
              selectedGroupReferralLinkIds.Contains(link.Id), localization))
          .ToArray();

  public string LinkLabel
  {
    get => linkLabel;
    set
    {
      if (SetProperty(ref linkLabel, value)) OnPropertyChanged(nameof(CanAddItem));
    }
  }

  public string LinkAddressText
  {
    get => linkAddressText;
    set
    {
      if (SetProperty(ref linkAddressText, value)) OnPropertyChanged(nameof(CanAddItem));
    }
  }

  public bool IsLinkType => addType == LandingPageAddType.Link;
  public bool IsCandidateType => addType is LandingPageAddType.ProfileLink or LandingPageAddType.Review or LandingPageAddType.ReferralLink;
  public bool IsTopicGroupType => addType == LandingPageAddType.TopicGroup;
  public bool HasSelectedTopic => selectedTopicId is not null;
  public string LocalizedAddButtonText => localization.Localize(
      addType == LandingPageAddType.Link
          ? UiMessageKey.NativeSwiftLandingPagesAddLink
          : UiMessageKey.NativeSwiftLandingPagesAddItem);

  public bool CanAddItem
  {
    get
    {
      if (!IsContentEditable) return false;
      return addType switch
      {
        LandingPageAddType.Link => CanAddLinkInput(LinkLabel, LinkAddressText),
        LandingPageAddType.ProfileLink or LandingPageAddType.Review or LandingPageAddType.ReferralLink =>
            CandidateOptions.Any(option => option.Id == selectedCandidateId),
        LandingPageAddType.TopicGroup =>
            TopicOptions.Any(option => option.Id == selectedTopicId)
            && (GroupReviewOptions.Any(option => option.IsSelected)
                || GroupReferralLinkOptions.Any(option => option.IsSelected)),
        _ => false,
      };
    }
  }

  public void SetGroupMemberSelected(LandingPageGroupMemberOption option, bool isSelected)
  {
    ArgumentNullException.ThrowIfNull(option);
    if (!IsContentEditable) return;
    var ids = option.Type == LandingPageGroupMemberType.Review
        ? selectedGroupReviewIds : selectedGroupReferralLinkIds;
    if (isSelected) ids.Add(option.Id); else ids.Remove(option.Id);
    NotifyGroupState();
  }

  private void InitializePicker()
  {
    ItemTypeOptions =
    [
      TypeOption(LandingPageAddType.Link, UiMessageKey.NativeSwiftLandingPagesLink),
      TypeOption(LandingPageAddType.ProfileLink, UiMessageKey.NativeSwiftLandingPagesProfileLink),
      TypeOption(LandingPageAddType.Review, UiMessageKey.NativeSwiftLandingPagesReview),
      TypeOption(LandingPageAddType.ReferralLink, UiMessageKey.NativeSwiftLandingPagesReferralLink),
      TypeOption(LandingPageAddType.TopicGroup, UiMessageKey.NativeSwiftLandingPagesTopicGroup),
    ];
  }

  private LandingPageItemTypeOption TypeOption(LandingPageAddType type, UiMessageKey key) =>
      new(type, UiText.Localized(key), localization);

  private void NotifyPickerState()
  {
    OnPropertyChanged(nameof(SelectedItemTypeOption));
    OnPropertyChanged(nameof(CandidateOptions));
    OnPropertyChanged(nameof(SelectedCandidateOption));
    OnPropertyChanged(nameof(TopicOptions));
    OnPropertyChanged(nameof(SelectedTopicOption));
    OnPropertyChanged(nameof(IsLinkType));
    OnPropertyChanged(nameof(IsCandidateType));
    OnPropertyChanged(nameof(IsTopicGroupType));
    OnPropertyChanged(nameof(LocalizedAddButtonText));
    NotifyGroupState();
  }

  private void NotifyGroupState()
  {
    OnPropertyChanged(nameof(SelectedTopicOption));
    OnPropertyChanged(nameof(GroupReviewOptions));
    OnPropertyChanged(nameof(GroupReferralLinkOptions));
    OnPropertyChanged(nameof(HasSelectedTopic));
    OnPropertyChanged(nameof(CanAddItem));
  }
}
