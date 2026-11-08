using System.Reflection;
using System.Net;
using Voucha.Client.Core.Lists;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityApplicantRenderingTests
{
  [Theory]
  [InlineData(false, "testuser")]
  [InlineData(true, "Deleted user")]
  public async Task ApplicationsSelectFixtureApplicantWithoutUuidEntry(bool deleted, string expected)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0, ["Headline"] = new Style(typeof(Label)), ["Metadata"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)), ["Eyebrow"] = new Style(typeof(Label)) } };
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationsProxy>();
    ((ApplicationsProxy)service).Deleted = deleted;
    var session = new SessionStore();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityApplicationsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    var picker = Assert.Single(VisualTreeElements.Descendants<Picker>(page), item => item.AutomationId == "community-application-picker");
    var row = Assert.IsType<CommunitySummaryRow>(Assert.Single(picker.ItemsSource.Cast<object>()));
    Assert.Equal(expected, row.Title);
    Assert.Equal("application-1", row.Id);
    Assert.Equal(2, VisualTreeElements.Descendants<Entry>(page).Count());
    var review = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.AutomationId == "community-application-review");
    Assert.False(review.IsEnabled);
    review.SendClicked();
    Assert.Null(((ApplicationsProxy)service).ReviewedId);
    picker.SelectedIndex = 0;
    Assert.Same(row, picker.SelectedItem);
    Assert.True(review.IsEnabled);
    review.SendClicked();
    Assert.Equal("application-1", ((ApplicationsProxy)service).ReviewedId);
    ((ApplicationsProxy)service).Empty = true;
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    review.SendClicked();
    Assert.Equal(1, ((ApplicationsProxy)service).ReviewCount);
  }

  [Theory]
  [InlineData("approved")]
  [InlineData("rejected")]
  public async Task ReviewedApplicantReloadClearsSelectionAndCannotReviewAgain(string reviewedStatus)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationsProxy>();
    var proxy = (ApplicationsProxy)service;
    proxy.StatusAfterReview = reviewedStatus;
    var session = new SessionStore();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityApplicationsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    var picker = Assert.Single(VisualTreeElements.Descendants<Picker>(page));
    var review = Assert.Single(VisualTreeElements.Descendants<Button>(page), button => button.AutomationId == "community-application-review");
    picker.SelectedIndex = 0;
    review.SendClicked();
    Assert.Equal(1, proxy.ReviewCount);
    Assert.Equal(reviewedStatus, Assert.Single(model.ApplicationRows).Status);
    Assert.Equal(-1, picker.SelectedIndex);
    Assert.Null(picker.SelectedItem);
    Assert.False(review.IsEnabled);
    picker.SelectedIndex = 0;
    Assert.False(review.IsEnabled);
    review.SendClicked();
    Assert.Equal(1, proxy.ReviewCount);
  }

  [Fact]
  public async Task SavedListTemplateRendersFixtureLabelsAndUntitledPost()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0, ["Headline"] = new Style(typeof(Label)), ["Metadata"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)), ["Eyebrow"] = new Style(typeof(Label)) } };
    using var http = new HttpClient(new ListHandler()) { BaseAddress = new Uri("https://api.test") };
    using var model = new ListsViewModel(new VouchaApiClient(http));
    var page = new ListsPage(model, new SessionStore());
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<ListItemRow>);
    foreach (var row in model.Items)
    {
      var content = Assert.IsAssignableFrom<View>(collection.ItemTemplate.CreateContent());
      content.BindingContext = row;
      var expected = row.ProtocolItemType == "post" ? "Untitled post" : "Saved article";
      Assert.Contains(VisualTreeElements.Descendants<Label>(content), label => label.Text == expected);
      Assert.DoesNotContain(VisualTreeElements.Descendants<Label>(content), label => label.Text == row.EntityId);
    }
  }

  private sealed class ListHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var fixture = request.RequestUri!.AbsolutePath.EndsWith("/items", StringComparison.Ordinal)
          ? "native.list-items.default" : "native.lists.default";
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(File.ReadAllText(FilamentsContractPaths.ApiFixture($"responses/{fixture}.json"))),
      });
    }
  }

  public class ApplicationsProxy : CommunityModerationPageAutomodNavigationTests.CommunityServiceProxy
  {
    public bool Deleted { get; set; }
    public bool Empty { get; set; }
    public string? StatusAfterReview { get; set; }
    public int ReviewCount { get; private set; }
    public string? ReviewedId { get; private set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
      if (method?.Name == nameof(ICommunitiesService.ReviewApplicationAsync))
      {
        ReviewCount++;
        ReviewedId = (string)args![1]!;
        return Task.CompletedTask;
      }
      if (method?.Name != nameof(ICommunitiesService.FetchApplicationsPageAsync)) return base.Invoke(method, args);
      var response = JsonSerializer.Deserialize<CommunityApplicationsResponse>(File.ReadAllText(
          FilamentsContractPaths.ApiFixture("responses/web.communities.applications.default.json")), VouchaApiJson.Options)!;
      if (ReviewCount > 0 && StatusAfterReview is { } status)
        response = response with { CommunityApplicationSummaries = response.CommunityApplicationSummaries.ToDictionary(
            pair => pair.Key, pair => pair.Value with { Status = status }) };
      if (Empty) response = response with { Results = [], CommunityApplicationSummaries = new Dictionary<string, CommunityApplicationSummary>() };
      return Task.FromResult(Deleted ? response with { Users = new Dictionary<string, PublicUser>() } : response);
    }
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("moderator-1", "moderator", Roles: [], EmailAddress: "mod@example.com", MembershipPlan: "free"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
