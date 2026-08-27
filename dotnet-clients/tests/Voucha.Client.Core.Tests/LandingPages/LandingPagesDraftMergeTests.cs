using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesDraftMergeTests
{
  [Fact]
  public async Task SamePageRefreshPreservesDirtySectionsAndAdvancesBothBaselines()
  {
    var service = new LandingPagesFeatureService();
    var viewModel = await LoadedAsync(service);
    viewModel.Title = "Draft title";
    Assert.True(viewModel.AddLink("Draft", new Uri("https://example.com/draft")));

    service.Page = service.Page with
    {
      Title = "Server title",
      Subtitle = "Server subtitle",
      Slug = "server-slug",
      Items = [new LandingPageLinkItem("server-item", "Server", new Uri("https://example.com/server"))],
    };
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Draft title", viewModel.Title);
    Assert.Equal("Draft", Assert.IsType<LandingPageLinkItem>(Assert.Single(viewModel.DraftItems)).Label);
    Assert.True(viewModel.HasUnsavedMetadata);
    Assert.True(viewModel.HasUnsavedItems);

    viewModel.Title = "Server title";
    viewModel.Subtitle = "Server subtitle";
    viewModel.Slug = "server-slug";
    viewModel.RemoveItem(Assert.Single(viewModel.DraftItems));
    Assert.True(viewModel.AddLink("Server", new Uri("https://example.com/server")));
    Assert.False(viewModel.HasUnsavedMetadata);
    Assert.False(viewModel.HasUnsavedItems);
  }

  [Fact]
  public async Task SamePageRefreshAcceptsEachCleanSectionIndependently()
  {
    var service = new LandingPagesFeatureService();
    var viewModel = await LoadedAsync(service);
    viewModel.Title = "Draft title";
    service.Page = service.Page with
    {
      Title = "Server title",
      Items = [new LandingPageLinkItem("server-item", "Fresh item", new Uri("https://example.com/fresh"))],
    };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Draft title", viewModel.Title);
    Assert.Equal("Fresh item", Assert.IsType<LandingPageLinkItem>(Assert.Single(viewModel.DraftItems)).Label);

    viewModel.Title = "Server title";
    Assert.True(viewModel.AddLink("Dirty item", new Uri("https://example.com/dirty")));
    service.Page = service.Page with { Title = "Newest title", Items = [] };
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Newest title", viewModel.Title);
    Assert.Equal(2, viewModel.DraftItems.Count);
  }

  [Fact]
  public async Task SamePageRefreshPreservesManualLinkWithWireDistinctCanonicalUrl()
  {
    var baselineUrl = new Uri("https://example.com/path");
    var wireDistinctUrl = new Uri("https://EXAMPLE.com/path");
    Assert.True(baselineUrl.Equals(wireDistinctUrl));
    var service = new LandingPagesFeatureService
    {
      Page = LandingPagesFeatureTestData.FixturePage(
          [new LandingPageLinkItem("server-item", "Docs", baselineUrl)]),
    };
    var viewModel = await LoadedAsync(service);
    viewModel.RemoveItem(Assert.Single(viewModel.DraftItems));
    Assert.True(viewModel.AddLink("Docs", wireDistinctUrl));
    Assert.True(viewModel.HasUnsavedItems);
    service.Page = service.Page with
    {
      Items = [new LandingPageLinkItem("replacement", "Replacement", new Uri("https://example.com/new"))],
    };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var preserved = Assert.IsType<LandingPageLinkItem>(Assert.Single(viewModel.DraftItems));
    Assert.Equal("https://EXAMPLE.com/path", preserved.Url.OriginalString);
    Assert.True(viewModel.HasUnsavedItems);
  }

  [Fact]
  public async Task FailedRefreshPreservesDraftsBaselinesCandidatesAndPickerState()
  {
    var service = new LandingPagesFeatureService();
    var viewModel = await LoadedAsync(service);
    viewModel.Title = "Draft title";
    SelectType(viewModel, LandingPageAddType.Review);
    viewModel.SelectedCandidateOption = viewModel.CandidateOptions[0];
    var candidateIds = viewModel.Candidates.Reviews.Select(review => review.Id).ToArray();
    service.Page = service.Page with { Title = "Should not commit" };
    service.Candidates = service.Candidates with { Reviews = [] };
    service.DetailFailure = new HttpRequestException("offline");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Equal("Draft title", viewModel.Title);
    Assert.Equal(candidateIds, viewModel.Candidates.Reviews.Select(review => review.Id));
    Assert.Equal("review-1", viewModel.SelectedCandidateOption?.Id);
    Assert.True(viewModel.HasUnsavedMetadata);
  }

  [Fact]
  public async Task CandidateRefreshPreservesDraftsRemovesStaleSelectionAndAddsNoDuplicates()
  {
    var service = new LandingPagesFeatureService();
    var viewModel = await LoadedAsync(service);
    SelectType(viewModel, LandingPageAddType.ProfileLink);
    viewModel.SelectedCandidateOption = Assert.Single(viewModel.CandidateOptions);
    Assert.True(viewModel.AddSelectedItem());
    var draft = Assert.Single(viewModel.DraftItems);

    SelectType(viewModel, LandingPageAddType.Review);
    viewModel.SelectedCandidateOption = viewModel.CandidateOptions[0];
    var existingReviews = service.Candidates.Reviews;
    service.Candidates = service.Candidates with
    {
      Reviews =
      [
        existingReviews[1],
        existingReviews[1] with { Id = "review-new", Title = "New travel review" },
      ],
    };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Same(draft, Assert.Single(viewModel.DraftItems));
    Assert.Null(viewModel.SelectedCandidateOption);
    Assert.Equal(["review-2", "review-new"], viewModel.CandidateOptions.Select(option => option.Id));
    Assert.Equal(2, viewModel.CandidateOptions.Select(option => option.Id).Distinct().Count());
    SelectType(viewModel, LandingPageAddType.ProfileLink);
    Assert.Empty(viewModel.CandidateOptions);
  }

  [Fact]
  public async Task MissingPageClearsStateAndExplicitSwitchDiscardsDrafts()
  {
    var service = new LandingPagesFeatureService();
    var viewModel = await LoadedAsync(service);
    viewModel.Title = "Draft title";
    Assert.True(viewModel.AddLink("Draft", new Uri("https://example.com/draft")));

    service.Page = service.Page with { Id = "landing-page-2", Title = "Other page", Items = [] };
    await viewModel.SelectPageAsync("landing-page-2", TestContext.Current.CancellationToken);

    Assert.Equal("Other page", viewModel.Title);
    Assert.Empty(viewModel.DraftItems);
    Assert.False(viewModel.HasUnsavedMetadata);
    Assert.False(viewModel.HasUnsavedItems);

    service.Page = service.Page with { Id = "landing-page-3" };
    viewModel.SetInitialSlug("missing");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Null(viewModel.SelectedPage);
    Assert.Empty(viewModel.DraftItems);
  }

  [Fact]
  public async Task SectionSavesAdvanceOnlyTheirBaselineAndFailuresPreserveInputs()
  {
    var service = new LandingPagesFeatureService();
    var viewModel = await LoadedAsync(service);
    viewModel.Title = "Draft title";
    Assert.True(viewModel.AddLink("Draft", new Uri("https://example.com/draft")));
    service.Page = service.Page with
    {
      Items = [new LandingPageLinkItem("saved-item", "Draft", new Uri("https://example.com/draft"))],
    };

    Assert.True(await viewModel.SaveContentAsync(TestContext.Current.CancellationToken));
    Assert.True(viewModel.HasUnsavedMetadata);
    Assert.False(viewModel.HasUnsavedItems);

    service.Page = service.Page with { Title = "Draft title" };
    Assert.True(await viewModel.SaveDetailsAsync(TestContext.Current.CancellationToken));
    Assert.False(viewModel.HasUnsavedMetadata);
    Assert.False(viewModel.HasUnsavedItems);

    viewModel.Title = "Failed metadata";
    Assert.True(viewModel.AddLink("Failed item", new Uri("https://example.com/failure")));
    service.ReplaceFailure = new HttpRequestException("save failed");
    Assert.False(await viewModel.SaveContentAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Failed metadata", viewModel.Title);
    Assert.Equal("Failed item", Assert.IsType<LandingPageLinkItem>(viewModel.DraftItems.Last()).Label);

    var cancelledDraft = viewModel.DraftItems.ToArray();
    viewModel.LinkLabel = "Unsubmitted label";
    viewModel.LinkAddressText = "https://example.com/unsubmitted";
    service.ReplaceFailure = new OperationCanceledException();
    Assert.False(await viewModel.SaveContentAsync(TestContext.Current.CancellationToken));
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.Equal(cancelledDraft, viewModel.DraftItems);
    Assert.Equal("Unsubmitted label", viewModel.LinkLabel);
    Assert.Equal("https://example.com/unsubmitted", viewModel.LinkAddressText);
    Assert.True(viewModel.HasUnsavedMetadata);
    Assert.True(viewModel.HasUnsavedItems);
  }

  private static async Task<LandingPagesViewModel> LoadedAsync(LandingPagesFeatureService service)
  {
    var viewModel = new LandingPagesViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static void SelectType(LandingPagesViewModel viewModel, LandingPageAddType type) =>
      viewModel.SelectedItemTypeOption = viewModel.ItemTypeOptions.Single(option => option.Type == type);
}
