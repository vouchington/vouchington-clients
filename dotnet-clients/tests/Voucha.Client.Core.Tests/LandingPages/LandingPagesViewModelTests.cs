using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesViewModelTests
{
  [Fact]
  public async Task LoadAsyncLoadsPagesCandidatesAndSelectedDetail()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = MakePagesResponse(),
      CandidatesResponse = MakeCandidatesResponse(),
      DetailResponse = MakeDetailResponse(),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(1, service.FetchPagesCalls);
    Assert.Equal(1, service.FetchCandidatesCalls);
    Assert.Equal(1, service.FetchDetailCalls);
    Assert.Equal("landing-page-1", viewModel.SelectedPage?.Id);
    Assert.True(viewModel.CanCreatePage);
    Assert.Equal("My Links", viewModel.Title);
    Assert.Equal("Cards and reviews I recommend", viewModel.Subtitle);
    Assert.Equal("links", viewModel.Slug);
    Assert.Equal(5, viewModel.DraftItems.Count);
    Assert.IsType<LandingPageProfileLinkItem>(viewModel.DraftItems[0]);
    Assert.IsType<LandingPageReviewItem>(viewModel.DraftItems[1]);
    Assert.IsType<LandingPageReferralLinkItem>(viewModel.DraftItems[2]);
    Assert.IsType<LandingPageTopicGroupItem>(viewModel.DraftItems[3]);
    Assert.IsType<LandingPageLinkItem>(viewModel.DraftItems[4]);
  }

  [Fact]
  public async Task SelectPageAsyncLoadsDetailAndReconcilesSelection()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = MakePagesResponse(),
      CandidatesResponse = MakeCandidatesResponse(),
      DetailResponse = MakeDetailResponse("landing-page-2", "Travel Stack", null, "travel"),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectPageAsync("landing-page-2", TestContext.Current.CancellationToken);

    Assert.Equal("landing-page-2", service.LastDetailPageId);
    Assert.Equal("landing-page-2", viewModel.SelectedPage?.Id);
    Assert.Equal("Travel Stack", viewModel.Title);
    Assert.Equal("", viewModel.Subtitle);
    Assert.Equal("travel", viewModel.Slug);
  }

  [Fact]
  public async Task LoadAsyncHandlesCancellationAndErrors()
  {
    var service = new RecordingLandingPagesService { ThrowOnFetchPages = true };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Contains("boom", viewModel.ErrorMessage, StringComparison.Ordinal);

    service.ThrowOnFetchPages = false;
    service.CandidatesResponse = MakeCandidatesResponse();
    service.CancelFetchPages = true;
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Idle, viewModel.State);
  }

  [Fact]
  public void ConstructorAndMappingsValidateInputs()
  {
    Assert.Throws<ArgumentNullException>(() => new LandingPagesViewModel(null!));
    Assert.Throws<ArgumentNullException>(() => LandingPageRow.FromPage(null!));
    Assert.Throws<ArgumentNullException>(() => LandingPageItemMappings.ToInput(null!));
    Assert.Throws<ArgumentNullException>(() => LandingPageItemMappings.Describe((LandingPageItem)null!));
    Assert.Throws<ArgumentNullException>(() => LandingPageItemMappings.Describe((LandingPageItemInput)null!));
  }

  [Fact]
  public void LandingPageRowsCopyPageFields()
  {
    var page = MakePage("landing-page-9", "Desk Setup", null, "desk", false);

    var row = LandingPageRow.FromPage(page);

    Assert.Equal(page.Id, row.Id);
    Assert.Equal(page.Title, row.Title);
    Assert.Null(row.Subtitle);
    Assert.Equal(page.Slug, row.Slug);
    Assert.False(row.IsDefault);
    Assert.Equal(page.CreatedAt, row.CreatedAt);
    Assert.Equal(page.UpdatedAt, row.UpdatedAt);
  }

  [Fact]
  public void LandingPageItemMappingsConvertAndDescribeEveryItemType()
  {
    var detail = MakeDetailResponse().LandingPage;
    var items = detail.Items ?? throw new InvalidOperationException("Missing fixture items.");

    var inputs = items.Select(item => item.ToInput()).ToArray();

    Assert.IsType<LandingPageProfileLinkItemInput>(inputs[0]);
    Assert.Equal("profile-link-1", inputs[0].ProfileLinkId);
    Assert.IsType<LandingPageReviewItemInput>(inputs[1]);
    Assert.Equal("review-1", inputs[1].ReviewId);
    Assert.IsType<LandingPageReferralLinkItemInput>(inputs[2]);
    Assert.Equal("referral-link-1", inputs[2].ReferralLinkId);
    var topicGroup = Assert.IsType<LandingPageTopicGroupItemInput>(inputs[3]);
    Assert.Equal("topic-1", topicGroup.TopicId);
    Assert.Collection(
        topicGroup.Entries ?? [],
        entry => Assert.IsType<LandingPageReviewItemInput>(entry),
        entry => Assert.IsType<LandingPageReferralLinkItemInput>(entry));
    Assert.IsType<LandingPageLinkItemInput>(inputs[4]);
    Assert.Equal("Newsletter", inputs[4].Label);

    Assert.Equal("Website", items[0].Describe());
    Assert.Equal("Best Travel Card", items[1].Describe());
    Assert.Equal("Apply", items[2].Describe());
    Assert.Equal("Topic group: Travel Cards", items[3].Describe());
    Assert.Equal("Newsletter", items[4].Describe());

    Assert.Equal("profile-link-1", inputs[0].Describe());
    Assert.Equal("review-1", inputs[1].Describe());
    Assert.Equal("referral-link-1", inputs[2].Describe());
    Assert.Equal("Topic group: topic-1", inputs[3].Describe());
    Assert.Equal("Newsletter", inputs[4].Describe());
  }

  [Fact]
  public void LandingPageItemDescriptionsUseFallbacks()
  {
    var profileByHandle = new LandingPageProfileLinkItem(
        "item-profile-handle",
        new LandingPageProfileLink(
            "profile-link-handle",
            "user-abc",
            "social",
            1,
            "url-profile-handle",
            new Uri("https://example.com/handle"),
            "@voucha",
            null,
            null,
            DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
            DateTimeOffset.Parse("2026-06-28T10:00:00Z")));
    var profileByUrl = new LandingPageProfileLinkItem(
        "item-profile-url",
        profileByHandle.ProfileLink with { Handle = null });
    var profileWithoutUrl = new LandingPageProfileLinkItem(
        "item-profile-without-url",
        profileByHandle.ProfileLink with { Handle = null, Url = null });
    var referralByProgram = new LandingPageReferralLinkItem(
        "item-referral-program",
        new LandingPageReferralLink(
            "referral-link-program",
            "topic-1",
            "Travel Cards",
            "travel-cards",
            null,
            new Uri("https://example.com/apply")));

    Assert.Equal("@voucha", profileByHandle.Describe());
    Assert.Equal("https://example.com/handle", profileByUrl.Describe());
    Assert.Equal("Profile link", profileWithoutUrl.Describe());
    Assert.Equal("Travel Cards", referralByProgram.Describe());
    Assert.Equal("Profile link", new LandingPageItemInput("profile_link").Describe());
    Assert.Equal("Review", new LandingPageItemInput("review").Describe());
    Assert.Equal("Referral link", new LandingPageItemInput("referral_link").Describe());
    Assert.Equal("Topic group: Topic", new LandingPageItemInput("topic_group").Describe());
    Assert.Equal("https://example.com/custom", new LandingPageItemInput("link", Url: new Uri("https://example.com/custom")).Describe());
    Assert.Equal("custom", new LandingPageItemInput("custom").Describe());
  }

  [Fact]
  public void UnsupportedLandingPageItemMappingThrows()
  {
    var item = new UnsupportedLandingPageItem("unsupported-1");

    var ex = Assert.Throws<InvalidOperationException>(() => item.ToInput());

    Assert.Contains("unsupported", ex.Message, StringComparison.Ordinal);
    Assert.Equal("unsupported", item.Describe());
  }

  [Fact]
  public async Task MutationsNoOpWithoutSelectedPage()
  {
    var service = new RecordingLandingPagesService();
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.SaveDetailsAsync(TestContext.Current.CancellationToken);
    await viewModel.SetDefaultAsync(TestContext.Current.CancellationToken);
    await viewModel.DeleteSelectedAsync(TestContext.Current.CancellationToken);
    await viewModel.SaveContentAsync(TestContext.Current.CancellationToken);

    Assert.Null(service.LastDetailPageId);
    Assert.Null(service.LastUpdateBody);
    Assert.Null(service.LastReplaceItemsBody);
    Assert.Empty(viewModel.DraftItems);
  }

  [Fact]
  public async Task CreateSaveDefaultAddRemoveAndSaveContentMutateState()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = MakePagesResponse(),
      CreateResponse = MakeDetailResponse("landing-page-3", "New Page", null, "new-page"),
      UpdateResponse = MakeDetailResponse("landing-page-3", "Updated Page", "", "updated-page"),
      DefaultResponse = MakeDetailResponse("landing-page-3", "Updated Page", "", "updated-page"),
      ReplaceItemsResponse = MakeDetailResponse("landing-page-3", "Updated Page", "", "updated-page"),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.CreateAsync("New Page", "  ", "new-page", TestContext.Current.CancellationToken);
    viewModel.Title = "Updated Page";
    viewModel.Subtitle = "";
    viewModel.Slug = "updated-page";
    await viewModel.SaveDetailsAsync(TestContext.Current.CancellationToken);
    await viewModel.SetDefaultAsync(TestContext.Current.CancellationToken);
    viewModel.AddLink("Docs", "https://example.com/docs");
    viewModel.AddLink("Support", new Uri("https://example.com/support"));
    var removed = viewModel.DraftItems.Last();
    viewModel.RemoveItem(removed);
    await viewModel.SaveContentAsync(TestContext.Current.CancellationToken);

    Assert.Equal(JsonNullableString.Null, service.LastCreateBody?.Subtitle);
    Assert.Equal("landing-page-3", service.LastDetailPageId);
    Assert.Equal(JsonNullableString.Null, service.LastUpdateBody?.Subtitle);
    Assert.Contains(viewModel.Pages, page => page.Id == "landing-page-1" && !page.IsDefault);
    Assert.Contains(viewModel.Pages, page => page.Id == "landing-page-2" && !page.IsDefault);
    Assert.DoesNotContain(viewModel.DraftItems, item => item.Id == removed.Id);
    Assert.NotNull(service.LastReplaceItemsBody);
    Assert.Contains(service.LastReplaceItemsBody.Items, item => item.Type == "link" && item.Label == "Docs");
  }

  [Fact]
  public async Task DeleteSelectedClearsSelectionWhenNoPagesRemain()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = MakePagesResponse(),
      CandidatesResponse = MakeCandidatesResponse(),
      DetailResponse = MakeDetailResponse(),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.PagesResponse = new LandingPagesResponse([], new PageInfo(null, false, null));

    await viewModel.DeleteSelectedAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.SelectedPage);
    Assert.Equal("", viewModel.Title);
    Assert.Equal("", viewModel.Subtitle);
    Assert.Equal("", viewModel.Slug);
    Assert.Empty(viewModel.DraftItems);
  }

  private static LandingPagesResponse MakePagesResponse() =>
      new(
          [
            MakePage("landing-page-1", "My Links", "Cards and reviews I recommend", "links", true),
            MakePage("landing-page-2", "Travel Stack", null, "travel", false),
          ],
          new PageInfo(null, false, null));

  private static LandingPageCandidatesResponse MakeCandidatesResponse(bool canCreate = true) =>
      new(
          new LandingPageCandidates(
              canCreate,
              [new LandingPageProfileLink("profile-link-1", "user-abc", "website", 1, "url-profile-1", new Uri("https://example.com"), null, "Website", null, DateTimeOffset.Parse("2026-06-28T10:00:00Z"), DateTimeOffset.Parse("2026-06-28T10:00:00Z"))],
              [new LandingPageReview(
                  "review-1",
                  "Best Travel Card",
                  "best-travel-card",
                  "Useful review",
                  DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
                  [new LandingPageReviewTopicRating("topic-1", "Travel Cards", "travel-cards", 5, 0)])],
              [new LandingPageReferralLink(
                  "referral-link-1",
                  "topic-1",
                  "Travel Cards",
                  "travel-cards",
                  "Apply",
                  new Uri("https://example.com/apply"))]));

  private static LandingPageDetailResponse MakeDetailResponse(
      string id = "landing-page-1",
      string title = "My Links",
      string? subtitle = "Cards and reviews I recommend",
      string slug = "links") =>
      new(
          MakePage(
              id,
              title,
              subtitle,
              slug,
              true,
              [
                new LandingPageProfileLinkItem(
                    "item-profile-1",
                    new LandingPageProfileLink(
                        "profile-link-1",
                        "user-abc",
                        "website",
                        1,
                        "url-profile-1",
                        new Uri("https://example.com"),
                        null,
                        "Website",
                        null,
                        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
                        DateTimeOffset.Parse("2026-06-28T10:00:00Z"))),
                new LandingPageReviewItem(
                    "item-review-1",
                    new LandingPageReview(
                        "review-1",
                        "Best Travel Card",
                        "best-travel-card",
                        "Useful review",
                        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
                        [new LandingPageReviewTopicRating("topic-1", "Travel Cards", "travel-cards", 5, 0)])),
                new LandingPageReferralLinkItem(
                    "item-referral-1",
                    new LandingPageReferralLink(
                        "referral-link-1",
                        "topic-1",
                        "Travel Cards",
                        "travel-cards",
                        "Apply",
                        new Uri("https://example.com/apply"))),
                new LandingPageTopicGroupItem(
                    "item-topic-group-1",
                    new LandingPageTopic(
                        "topic-1",
                        "Travel Cards",
                        "travel-cards",
                        "referral_program"),
                    [
                      new LandingPageReviewItem(
                          "group-entry-review-1",
                          new LandingPageReview(
                              "review-1",
                              "Best Travel Card",
                              "best-travel-card",
                              "Useful review",
                              DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
                              [new LandingPageReviewTopicRating("topic-1", "Travel Cards", "travel-cards", 5, 0)])),
                      new LandingPageReferralLinkItem(
                          "group-entry-referral-1",
                          new LandingPageReferralLink(
                              "referral-link-1",
                              "topic-1",
                              "Travel Cards",
                              "travel-cards",
                              "Apply",
                              new Uri("https://example.com/apply"))),
                    ]),
                new LandingPageLinkItem("item-link-1", "Newsletter", new Uri("https://example.com/newsletter")),
              ]));

  private static LandingPage MakePage(
      string id,
      string title,
      string? subtitle,
      string slug,
      bool isDefault,
      IReadOnlyList<LandingPageItem>? items = null) =>
      new(
          id,
          "user-abc",
          title,
          subtitle,
          slug,
          isDefault,
          DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
          DateTimeOffset.Parse("2026-06-29T10:00:00Z"),
          items);

  private sealed class RecordingLandingPagesService : ILandingPagesService
  {
    public LandingPagesResponse? PagesResponse { get; set; }

    public LandingPageCandidatesResponse? CandidatesResponse { get; set; }

    public LandingPageDetailResponse? DetailResponse { get; set; }

    public LandingPageDetailResponse? CreateResponse { get; set; }

    public LandingPageDetailResponse? UpdateResponse { get; set; }

    public LandingPageDetailResponse? DefaultResponse { get; set; }

    public LandingPageDetailResponse? ReplaceItemsResponse { get; set; }

    public bool ThrowOnFetchPages { get; set; }

    public bool CancelFetchPages { get; set; }

    public int FetchPagesCalls { get; private set; }

    public int FetchCandidatesCalls { get; private set; }

    public int FetchDetailCalls { get; private set; }

    public string? LastDetailPageId { get; private set; }

    public CreateLandingPageBody? LastCreateBody { get; private set; }

    public UpdateLandingPageBody? LastUpdateBody { get; private set; }

    public ReplaceLandingPageItemsBody? LastReplaceItemsBody { get; private set; }

    public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default)
    {
      FetchPagesCalls++;
      if (ThrowOnFetchPages)
      {
        throw new InvalidOperationException("boom");
      }

      return CancelFetchPages
          ? Task.FromCanceled<LandingPagesResponse>(new CancellationToken(canceled: true))
          : Task.FromResult(PagesResponse ?? throw new InvalidOperationException("Missing pages response."));
    }

    public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default)
    {
      FetchCandidatesCalls++;
      return Task.FromResult(CandidatesResponse ?? throw new InvalidOperationException("Missing candidates response."));
    }

    public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> FetchDetailAsync(string pageId, CancellationToken cancellationToken = default)
    {
      FetchDetailCalls++;
      LastDetailPageId = pageId;
      return Task.FromResult(DetailResponse ?? throw new InvalidOperationException("Missing detail response."));
    }

    public Task<LandingPageDetailResponse> CreateAsync(CreateLandingPageBody body, CancellationToken cancellationToken = default)
    {
      LastCreateBody = body;
      return Task.FromResult(CreateResponse ?? throw new InvalidOperationException("Missing create response."));
    }

    public Task<LandingPageDetailResponse> UpdateAsync(string pageId, UpdateLandingPageBody body, CancellationToken cancellationToken = default)
    {
      LastDetailPageId = pageId;
      LastUpdateBody = body;
      return Task.FromResult(UpdateResponse ?? throw new InvalidOperationException("Missing update response."));
    }

    public Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default)
    {
      LastDetailPageId = pageId;
      return Task.FromResult(DefaultResponse ?? throw new InvalidOperationException("Missing default response."));
    }

    public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default)
    {
      LastDetailPageId = pageId;
      return Task.CompletedTask;
    }

    public Task<LandingPageDetailResponse> ReplaceItemsAsync(
        string pageId,
        ReplaceLandingPageItemsBody body,
        CancellationToken cancellationToken = default)
    {
      LastDetailPageId = pageId;
      LastReplaceItemsBody = body;
      return Task.FromResult(ReplaceItemsResponse ?? throw new InvalidOperationException("Missing replace response."));
    }
  }

  private sealed record UnsupportedLandingPageItem(string Id) : LandingPageItem(Id)
  {
    public override string Type => "unsupported";
  }
}
