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
    picker.SelectedIndex = 0;
    Assert.Same(row, picker.SelectedItem);
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
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
      if (method?.Name != nameof(ICommunitiesService.FetchApplicationsPageAsync)) return base.Invoke(method, args);
      var response = JsonSerializer.Deserialize<CommunityApplicationsResponse>(File.ReadAllText(
          FilamentsContractPaths.ApiFixture("responses/web.communities.applications.default.json")), VouchaApiJson.Options)!;
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
