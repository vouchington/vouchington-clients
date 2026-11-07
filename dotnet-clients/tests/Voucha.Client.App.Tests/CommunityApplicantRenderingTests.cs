using System.Reflection;
using System.Net;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Localization;
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
  public async Task ApplicationRowsReviewTheFixtureApplicantWithoutUuidEntry(bool deleted, string expected)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var version = new UiLocaleVersion(locale);
    InstallApplicationResources(locale, version);
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationsProxy>();
    var proxy = (ApplicationsProxy)service;
    proxy.Deleted = deleted;
    var session = new SessionStore();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityApplicationsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    var content = ApplicationRow(page, Assert.Single(model.ApplicationRows));
    Assert.Contains(VisualTreeElements.Descendants<Label>(content), label => label.Text == expected);
    Assert.DoesNotContain(VisualTreeElements.Descendants<Entry>(content), entry => entry.Text == "application-1");
    Assert.Single(VisualTreeElements.Descendants<Entry>(content));
    var approve = FindApplicationButton(content, "approve");
    Assert.True(approve.IsVisible);
    Assert.True(approve.IsEnabled);
    approve.SendClicked();
    Assert.Equal("application-1", proxy.ReviewedId);
    proxy.Empty = true;
    await model.SelectSectionAsync(CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);

    approve.SendClicked();

    Assert.Equal(1, proxy.ReviewCount);
  }

  [Theory]
  [InlineData("approved")]
  [InlineData("rejected")]
  public async Task ReviewedApplicantReloadDisablesBothRowActionsAndRejectsStaleClicks(string reviewedStatus)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var version = new UiLocaleVersion(locale);
    InstallApplicationResources(locale, version);
    var service = DispatchProxy.Create<ICommunitiesService, ApplicationsProxy>();
    var proxy = (ApplicationsProxy)service;
    proxy.StatusAfterReview = reviewedStatus;
    var session = new SessionStore();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityApplicationsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);
    var content = ApplicationRow(page, Assert.Single(model.ApplicationRows));
    var approve = FindApplicationButton(content, "approve");
    var reject = FindApplicationButton(content, "reject");
    approve.SendClicked();
    Assert.Equal(1, proxy.ReviewCount);
    var reviewed = Assert.Single(model.ApplicationRows);
    Assert.Equal(reviewedStatus, reviewed.Status);

    approve.SendClicked();
    reject.SendClicked();
    Assert.Equal(1, proxy.ReviewCount);
    content.BindingContext = reviewed;
    Assert.False(approve.IsVisible);
    Assert.False(reject.IsVisible);
    Assert.False(approve.IsEnabled);
    Assert.False(reject.IsEnabled);
    approve.SendClicked();
    reject.SendClicked();
    Assert.Equal(1, proxy.ReviewCount);
  }

  private static View ApplicationRow(CommunityApplicationsPage page, CommunityApplicationRow row)
  {
    var collection = Assert.Single(VisualTreeElements.Descendants<CollectionView>(page), view =>
        view.ItemsSource is IReadOnlyList<CommunityApplicationRow>);
    var content = Assert.IsAssignableFrom<View>(collection.ItemTemplate.CreateContent());
    content.BindingContext = row;
    return content;
  }

  private static Button FindApplicationButton(View row, string action) =>
      Assert.Single(VisualTreeElements.Descendants<Button>(row), button => button.AutomationId == $"community-application-{action}");

  private static void InstallApplicationResources(UiLocaleController locale, UiLocaleVersion version) =>
      _ = new Application { Resources =
      {
        ["UiLocaleVersion"] = version,
        ["UiLocalizedValue"] = new UiLocalizedValueConverter(new UiLocalization(locale)),
      } };

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
