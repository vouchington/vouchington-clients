using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesPaginationTests
{
  [Fact]
  public async Task AppendsOpaquePagesInServerOrderAndStopsWhenExhausted()
  {
    var service = new LandingPagesFeatureService
    {
      CandidatePageInfo = new("opaque/+==", true, null),
    };
    using var model = new LandingPagesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var first = model.Candidates.Reviews.ToArray();
    service.Candidates = service.Candidates with
    {
      Reviews = [first[1], first[0] with { Id = "later-review", Title = "Later" }],
    };
    service.CandidatePageInfo = new(null, false, null);

    await model.LoadMoreCandidatesAsync(TestContext.Current.CancellationToken);

    Assert.Equal("opaque/+==", service.LastCandidateCursor);
    Assert.Equal(first.Select(review => review.Id).Append("later-review"), model.Candidates.Reviews.Select(review => review.Id));
    Assert.False(model.HasMoreCandidates);
    await model.LoadMoreCandidatesAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, service.CandidateFetchCount);
  }

  [Fact]
  public async Task CapBlocksAdditionButAllowsRemovalAndUsesLatestMutationLimit()
  {
    var service = new LandingPagesFeatureService { ItemLimit = 1 };
    using var model = new LandingPagesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.AddLink("One", "https://example.com/1"));
    Assert.False(model.AddLink("Two", "https://example.com/2"));
    model.RemoveItem(Assert.Single(model.DraftItems));
    Assert.True(model.AddLink("Two", "https://example.com/2"));
    service.ItemLimit = 2;
    Assert.True(await model.SaveDetailsAsync(TestContext.Current.CancellationToken));
    Assert.Equal(2, model.ItemLimit);
    Assert.True(model.AddLink("Three", "https://example.com/3"));
  }

  [Fact]
  public async Task HydratedItemsDoNotBecomeEligibleCandidatesAfterRemoval()
  {
    var service = new LandingPagesFeatureService();
    var review = service.Candidates.Reviews[0];
    service.Page = service.Page with { Items = [new LandingPageReviewItem("existing", review)] };
    service.Candidates = service.Candidates with { Reviews = [] };
    using var model = new LandingPagesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.RemoveItem(Assert.Single(model.DraftItems));
    model.SelectedItemTypeOption = model.ItemTypeOptions.Single(option => option.Type == LandingPageAddType.Review);
    Assert.Empty(model.CandidateOptions);
    Assert.False(model.CanAddItem);
  }

  [Fact]
  public async Task LimitRefusalRetainsDraftAndDisplaysServerMessage()
  {
    var service = new LandingPagesFeatureService
    {
      ReplaceFailure = new VouchaApiException(HttpStatusCode.Conflict,
          "{\"code\":\"LANDING_PAGE_ITEM_LIMIT_REACHED\",\"message\":\"Landing page item limit reached\"}"),
    };
    using var model = new LandingPagesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.AddLink("One", "https://example.com"));
    Assert.False(await model.SaveContentAsync(TestContext.Current.CancellationToken));
    Assert.True(model.IsItemLimitReached);
    Assert.Equal("Landing page item limit reached", model.ErrorMessage);
    Assert.Single(model.DraftItems);
  }

  [Fact]
  public async Task TopicGroupRequiresRoomForContainerAndEveryEntry()
  {
    var service = new LandingPagesFeatureService { ItemLimit = 2 };
    using var model = new LandingPagesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.SelectedItemTypeOption = model.ItemTypeOptions.Single(option => option.Type == LandingPageAddType.TopicGroup);
    model.SelectedTopicOption = model.TopicOptions[0];
    model.SetGroupMemberSelected(model.GroupReviewOptions[0], true);
    Assert.True(model.CanAddItem);
    model.SetGroupMemberSelected(model.GroupReviewOptions[1], true);
    Assert.False(model.CanAddItem);
    Assert.False(model.AddSelectedItem());
    model.SetGroupMemberSelected(model.GroupReviewOptions[1], false);
    Assert.True(model.AddSelectedItem());
    Assert.False(model.AddLink("Over cap", "https://example.com"));
  }

  [Fact]
  public async Task FailedDetailReloadPreservesCandidatesCursorAndLimit()
  {
    var service = new LandingPagesFeatureService
    {
      ItemLimit = 3,
      CandidatePageInfo = new("original-cursor", true, null),
    };
    using var model = new LandingPagesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var candidates = model.Candidates;
    service.ItemLimit = 9;
    service.CandidatePageInfo = new("replacement-cursor", true, null);
    service.Candidates = service.Candidates with { Reviews = [] };
    service.DetailFailure = new HttpRequestException("Detail unavailable");

    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Same(candidates, model.Candidates);
    Assert.Equal(3, model.ItemLimit);
    Assert.True(model.HasMoreCandidates);
    service.CandidatePageInfo = new(null, false, null);
    await model.LoadMoreCandidatesAsync(TestContext.Current.CancellationToken);
    Assert.Equal("original-cursor", service.LastCandidateCursor);
    Assert.Equal(candidates.Reviews, model.Candidates.Reviews);
  }

  [Fact]
  public void DetailRequiresItemLimit() =>
      Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LandingPageDetailResponse>(
          "{\"landing_page\":null}", VouchaApiJson.Options));

  [Theory]
  [InlineData("{\"candidates\":{}}")]
  [InlineData("{\"candidates\":{},\"page_info\":{}}")]
  [InlineData("{\"candidates\":{},\"item_limit\":100}")]
  public void MissingPaginationMetadataIsRejected(string json) =>
      Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LandingPageCandidatesResponse>(json, VouchaApiJson.Options));
}
