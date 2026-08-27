using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class LandingPagesPageTests
{
  [Fact]
  public async Task RendersFiveTypePickerContextualControlsAndHumanLabels()
  {
    var service = new Service();
    using var localeController = new UiLocaleController(new EnglishLanguages());
    var localization = new UiLocalization(localeController);
    using var viewModel = new LandingPagesViewModel(service, localization, localeController);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel, localeController, localization);

    var typePicker = Find<Picker>(page, "landing-pages-item-type-picker");
    Assert.Equal(
        ["Link", "Profile link", "Review", "Referral link", "Topic group"],
        typePicker.ItemsSource.Cast<LandingPageItemTypeOption>().Select(option => option.LocalizedLabel));
    Assert.NotNull(Find<Entry>(page, "landing-pages-link-label"));
    Assert.NotNull(Find<Entry>(page, "landing-pages-link-url"));
    Assert.NotNull(Find<Picker>(page, "landing-pages-candidate-picker"));
    Assert.NotNull(Find<Picker>(page, "landing-pages-topic-picker"));
    Assert.NotNull(Find<CollectionView>(page, "landing-pages-group-reviews"));
    Assert.NotNull(Find<CollectionView>(page, "landing-pages-group-referral-links"));
    Assert.NotNull(Find<Button>(page, "landing-pages-add-item"));
    Assert.NotNull(Find<Button>(page, "landing-pages-save-content"));
    Assert.Empty(Descendants<WebView>(page));

    viewModel.SelectedItemTypeOption = viewModel.ItemTypeOptions.Single(option => option.Type == LandingPageAddType.Review);
    var candidatePicker = Find<Picker>(page, "landing-pages-candidate-picker");
    Assert.True(candidatePicker.IsVisible);
    Assert.Equal(["Best Travel Card", "Travel Card Benefits"],
        candidatePicker.ItemsSource.Cast<LandingPagePickerOption>().Select(option => option.LocalizedLabel));
    Assert.DoesNotContain(candidatePicker.Items, label => label.Contains("review-", StringComparison.Ordinal));

    viewModel.SelectedItemTypeOption = viewModel.ItemTypeOptions.Single(option => option.Type == LandingPageAddType.TopicGroup);
    viewModel.SelectedTopicOption = Assert.Single(viewModel.TopicOptions);
    var groupReviews = Find<CollectionView>(page, "landing-pages-group-reviews");
    var reviewTemplate = Assert.IsAssignableFrom<Element>(groupReviews.ItemTemplate.CreateContent());
    reviewTemplate.BindingContext = viewModel.GroupReviewOptions[0];
    var reviewCheck = Find<CheckBox>(reviewTemplate, "landing-pages-group-review-member");
    reviewCheck.IsChecked = true;
    Assert.True(viewModel.GroupReviewOptions[0].IsSelected);

    var originalItemTypeOptions = typePicker.ItemsSource;
    var propertyChanges = new List<string?>();
    viewModel.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);
    localeController.ApplySavedLocale("es");
    Assert.NotSame(originalItemTypeOptions, typePicker.ItemsSource);
    Assert.Equal(LandingPageAddType.TopicGroup, viewModel.SelectedItemTypeOption.Type);
    Assert.Contains(nameof(LandingPagesViewModel.DraftItems), propertyChanges);
    Assert.Equal(
        ["Enlace", "Enlace de perfil", "Reseña", "Enlace de referido", "Grupo de temas"],
        typePicker.Items);
  }

  [Fact]
  public async Task AppearanceRefreshIsUserReachableAndPreservesSamePageDrafts()
  {
    var service = new Service();
    var viewModel = new LandingPagesViewModel(service);
    var page = CreatePage(viewModel);
    await page.LoadForAppearanceAsync(TestContext.Current.CancellationToken);
    viewModel.Title = "Draft title";
    viewModel.LinkLabel = "Docs";
    viewModel.LinkAddressText = "https://example.com/docs";

    service.Page = service.Page with { Title = "Server title" };
    await page.LoadForAppearanceAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.PageFetchCount);
    Assert.Equal(2, service.CandidateFetchCount);
    Assert.Equal(2, service.DetailFetchCount);
    Assert.Equal("Draft title", viewModel.Title);
    Assert.Equal("Docs", viewModel.LinkLabel);
    Assert.Equal("https://example.com/docs", viewModel.LinkAddressText);
  }

  [Fact]
  public async Task FreeMemberRendersAnalyticsUpgradeStateWithoutRequestingAnalytics()
  {
    var service = new Service();
    var viewModel = new LandingPagesViewModel(service, canViewAnalytics: false);
    var page = CreatePage(viewModel);

    await page.LoadForAppearanceAsync(TestContext.Current.CancellationToken);

    Assert.True(Find<Label>(page, "landing-pages-analytics-upgrade-required").IsVisible);
    Assert.Equal(0, service.AnalyticsFetchCount);
  }

  private static LandingPagesPage CreatePage(
      LandingPagesViewModel viewModel,
      UiLocaleController? controller = null,
      UiLocalization? localization = null)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    controller ??= new UiLocaleController(new EnglishLanguages());
    localization ??= new UiLocalization(controller);
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
        ["UiLocaleVersion"] = new UiLocaleVersion(controller),
        ["UiLocalizedValue"] = new UiLocalizedValueConverter(localization),
      },
    };
    return new LandingPagesPage(viewModel);
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private sealed class Service : ILandingPagesService
  {
    public LandingPage Page { get; set; } = CreatePageValue();
    public int PageFetchCount { get; private set; }
    public int CandidateFetchCount { get; private set; }
    public int DetailFetchCount { get; private set; }
    public int AnalyticsFetchCount { get; private set; }

    public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default)
    { PageFetchCount++; return Task.FromResult(new LandingPagesResponse([Page], new PageInfo(null, false, null))); }
    public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default)
    { CandidateFetchCount++; return Task.FromResult(new LandingPageCandidatesResponse(CreateCandidates())); }
    public Task<LandingPageDetailResponse> FetchDetailAsync(string pageId, CancellationToken cancellationToken = default)
    { DetailFetchCount++; return Task.FromResult(new LandingPageDetailResponse(Page)); }
    public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(string pageId, CancellationToken cancellationToken = default)
    { AnalyticsFetchCount++; return Task.FromResult(new LandingPageAnalyticsResponse(new LandingPageAnalytics(0, 0, 0, 0, [], [], [], new LandingPageConversionFunnel(0, 0, 0, 0)))); }
    public Task<LandingPageDetailResponse> CreateAsync(CreateLandingPageBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> UpdateAsync(string pageId, UpdateLandingPageBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPageDetailResponse> ReplaceItemsAsync(string pageId, ReplaceLandingPageItemsBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(string pageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    private static LandingPage CreatePageValue() => new(
        "landing-page-1", "user-1", "My Links", null, "links", true,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, []);

    private static LandingPageCandidates CreateCandidates()
    {
      var ratings = new[] { new LandingPageReviewTopicRating("topic-1", "Travel Cards", "travel-cards", 5, 0) };
      return new LandingPageCandidates(
          true,
          [new LandingPageProfileLink("profile-1", "user-1", "url", 0, null, new Uri("https://example.com"), null, "Website", null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)],
          [
            new LandingPageReview("review-1", "Best Travel Card", null, "Review", DateTimeOffset.UnixEpoch, ratings),
            new LandingPageReview("review-2", "Travel Card Benefits", null, "Review", DateTimeOffset.UnixEpoch, ratings),
          ],
          [new LandingPageReferralLink("referral-1", "topic-1", "Travel Cards", "travel-cards", "Apply", new Uri("https://example.com/apply"))]);
    }
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }

  private sealed class EnglishLanguages : IDeviceLanguageProvider
  { public IReadOnlyList<string> PreferredLanguages => ["en"]; }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
