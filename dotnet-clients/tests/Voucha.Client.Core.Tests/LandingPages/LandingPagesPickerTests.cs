using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesPickerTests
{
  [Fact]
  public async Task PickerAddsAllFiveTypesInStableWireOrder()
  {
    var viewModel = await LoadedAsync();

    SelectType(viewModel, LandingPageAddType.ProfileLink);
    viewModel.SelectedCandidateOption = Assert.Single(viewModel.CandidateOptions);
    Assert.True(viewModel.AddSelectedItem());

    SelectType(viewModel, LandingPageAddType.Review);
    viewModel.SelectedCandidateOption = viewModel.CandidateOptions[0];
    Assert.True(viewModel.AddSelectedItem());

    SelectType(viewModel, LandingPageAddType.ReferralLink);
    viewModel.SelectedCandidateOption = viewModel.CandidateOptions[0];
    Assert.True(viewModel.AddSelectedItem());

    SelectType(viewModel, LandingPageAddType.TopicGroup);
    viewModel.SelectedTopicOption = Assert.Single(viewModel.TopicOptions);
    viewModel.SetGroupMemberSelected(Assert.Single(viewModel.GroupReviewOptions), true);
    viewModel.SetGroupMemberSelected(Assert.Single(viewModel.GroupReferralLinkOptions), true);
    Assert.True(viewModel.AddSelectedItem());

    SelectType(viewModel, LandingPageAddType.Link);
    viewModel.LinkLabel = " Newsletter ";
    viewModel.LinkAddressText = " https://example.com/newsletter ";
    Assert.True(viewModel.AddSelectedItem());

    Assert.Equal(
        ["profile_link", "review", "referral_link", "topic_group", "link"],
        viewModel.DraftItems.Select(item => item.Type));
    var group = Assert.IsType<LandingPageTopicGroupItem>(viewModel.DraftItems[3]);
    Assert.Collection(
        group.Entries,
        item => Assert.IsType<LandingPageReviewItem>(item),
        item => Assert.IsType<LandingPageReferralLinkItem>(item));
    Assert.Equal("Newsletter", Assert.IsType<LandingPageLinkItem>(viewModel.DraftItems[4]).Label);
    Assert.True(viewModel.HasUnsavedItems);
  }

  [Fact]
  public async Task OptionsUseHumanLabelsAndExcludeTopLevelAndNestedCandidates()
  {
    var viewModel = await LoadedAsync();

    Assert.Equal(
        ["Link", "Profile link", "Review", "Referral link", "Topic group"],
        viewModel.ItemTypeOptions.Select(option => option.LocalizedLabel));
    Assert.DoesNotContain(
        viewModel.ItemTypeOptions.Select(option => option.LocalizedLabel),
        label => label.Contains("_", StringComparison.Ordinal));

    SelectType(viewModel, LandingPageAddType.TopicGroup);
    viewModel.SelectedTopicOption = Assert.Single(viewModel.TopicOptions);
    Assert.Equal(["Best Travel Card", "Travel Card Benefits"], viewModel.GroupReviewOptions.Select(option => option.LocalizedLabel));
    Assert.Equal(["Apply", "Learn more"], viewModel.GroupReferralLinkOptions.Select(option => option.LocalizedLabel));
    viewModel.SetGroupMemberSelected(viewModel.GroupReviewOptions[0], true);
    viewModel.SetGroupMemberSelected(viewModel.GroupReferralLinkOptions[0], true);
    Assert.True(viewModel.AddSelectedItem());

    SelectType(viewModel, LandingPageAddType.Review);
    Assert.Equal(["Travel Card Benefits"], viewModel.CandidateOptions.Select(option => option.LocalizedLabel));
    SelectType(viewModel, LandingPageAddType.ReferralLink);
    Assert.Equal(["Learn more"], viewModel.CandidateOptions.Select(option => option.LocalizedLabel));
  }

  [Fact]
  public async Task TopicOptionsDeduplicateSortAndFilterByMembership()
  {
    var service = new LandingPagesFeatureService();
    var baseCandidates = service.Candidates;
    service.Candidates = baseCandidates with
    {
      Reviews =
      [
        .. baseCandidates.Reviews,
        baseCandidates.Reviews[0] with
        {
          Id = "review-z",
          Title = "Zero fee card",
          ReviewTopicRatings =
          [
            new LandingPageReviewTopicRating("topic-z", "Zero Fees", "zero-fees", 5, 0),
            new LandingPageReviewTopicRating("topic-1", "Travel Cards", "travel-cards", 4, 1),
          ],
        },
      ],
    };
    var viewModel = new LandingPagesViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    SelectType(viewModel, LandingPageAddType.TopicGroup);
    Assert.Equal(["Travel Cards", "Zero Fees"], viewModel.TopicOptions.Select(option => option.LocalizedLabel));
    viewModel.SelectedTopicOption = viewModel.TopicOptions.Single(option => option.Id == "topic-z");
    Assert.Equal(["Zero fee card"], viewModel.GroupReviewOptions.Select(option => option.LocalizedLabel));
    Assert.Empty(viewModel.GroupReferralLinkOptions);
  }

  [Fact]
  public async Task AddRevalidatesUrlsCandidatesAndNonemptyGroups()
  {
    var viewModel = await LoadedAsync();
    SelectType(viewModel, LandingPageAddType.Link);
    viewModel.LinkLabel = "Docs";
    viewModel.LinkAddressText = "https://example.com/docs#install";
    Assert.False(viewModel.CanAddItem);
    Assert.False(viewModel.AddSelectedItem());

    SelectType(viewModel, LandingPageAddType.TopicGroup);
    viewModel.SelectedTopicOption = Assert.Single(viewModel.TopicOptions);
    Assert.False(viewModel.CanAddItem);
    Assert.False(viewModel.AddSelectedItem());

    SelectType(viewModel, LandingPageAddType.Review);
    viewModel.SelectedCandidateOption = viewModel.CandidateOptions[0];
    Assert.True(viewModel.CanAddItem);
    Assert.True(viewModel.AddSelectedItem());
    Assert.Null(viewModel.SelectedCandidateOption);
    Assert.False(viewModel.CanAddItem);
  }

  [Fact]
  public async Task GroupSelectionIsIgnoredWhileTheViewModelIsLoading()
  {
    var service = new LandingPagesFeatureService();
    var viewModel = new LandingPagesViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    SelectType(viewModel, LandingPageAddType.TopicGroup);
    viewModel.SelectedTopicOption = Assert.Single(viewModel.TopicOptions);
    var option = viewModel.GroupReviewOptions[0];
    service.PagesGate = new TaskCompletionSource<LandingPagesResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

    var reload = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsLoading);
    viewModel.SetGroupMemberSelected(option, true);

    Assert.DoesNotContain(viewModel.GroupReviewOptions, candidate => candidate.IsSelected);
    service.PagesGate.SetResult(LandingPagesFeatureTestData.Pages(service.Page));
    await reload;
  }

  private static async Task<LandingPagesViewModel> LoadedAsync()
  {
    var viewModel = new LandingPagesViewModel(new LandingPagesFeatureService());
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static void SelectType(LandingPagesViewModel viewModel, LandingPageAddType type) =>
      viewModel.SelectedItemTypeOption = viewModel.ItemTypeOptions.Single(option => option.Type == type);
}
