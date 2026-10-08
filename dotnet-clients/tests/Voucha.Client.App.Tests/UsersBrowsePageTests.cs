using System.Collections;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Users;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class UsersBrowsePageTests
{
  [Fact]
  public void PaginationControlIsHiddenBeforeAnyResultsAreLoaded()
  {
    var (page, _) = CreatePageWithRow(administrator: true);

    var action = Find<Button>(page, "pagination-users-browse-action");
    Assert.False(action.IsVisible);

    page.Dispose();
  }

  [Fact]
  public async Task LoadMoreRequestsTheNextPageAndAppendsResultsToTheCollection()
  {
    var service = new PagedService(
        new UsersSearchResponse([new UserSearchResult("user-1", "alice")], new PageInfo("cursor-1", true, null)),
        new UsersSearchResponse([new UserSearchResult("user-2", "bob")], new PageInfo(null, false, null)));
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var controller = new UiLocaleController(new Languages());
    var localization = new UiLocalization(controller);
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, ["administrator"]));
    var model = new UsersBrowseViewModel(service, viewerProvider, localization) { Query = "alice" };
    var page = new UsersBrowsePage(model, localization);
    await model.SearchAsync(TestContext.Current.CancellationToken);

    var action = Find<Button>(page, "pagination-users-browse-action");
    Assert.True(action.IsVisible);

    action.SendClicked();

    Assert.Equal("cursor-1", Assert.Single(service.Requests, request => request.After is not null).After);
    Assert.Equal(
        ["user-1", "user-2"],
        Assert.IsAssignableFrom<IEnumerable>(Find<CollectionView>(page, "users-browse-results").ItemsSource)
            .Cast<UserSearchResult>()
            .Select(user => user.Id));
    Assert.False(Find<Button>(page, "pagination-users-browse-action").IsVisible);

    page.Dispose();
  }

  [Fact]
  public void RendersSearchControlsAndShowsManageLinkForAdministrators()
  {
    var (page, row) = CreatePageWithRow(administrator: true);

    Assert.Single(Descendants<Entry>(page), value => value.AutomationId == "users-browse-query");
    Assert.Single(Descendants<Button>(page), value => value.AutomationId == "users-browse-search");
    Assert.Single(Descendants<CollectionView>(page), value => value.AutomationId == "users-browse-results");
    Assert.Single(Descendants<Label>(page), value => value.AutomationId == "users-browse-empty");
    Assert.Single(Descendants<Label>(page), value => value.AutomationId == "users-browse-error");
    Assert.Empty(Descendants<WebView>(page));

    var manage = Find<Button>(row, "user-card-manage-link");
    Assert.True(manage.IsVisible);
    Assert.True(Find<Label>(row, "user-card-status").IsVisible);
    Assert.Equal("Active", Find<Label>(row, "user-card-status").Text);
    Assert.False(Find<Label>(row, "user-card-email").IsVisible);

    page.Dispose();
  }

  [Fact]
  public void ShowsEmailAndSuspendedStatusForAdministrators()
  {
    var (page, row) = CreatePageWithRow(
        administrator: true,
        new UserSearchResult(
            "user-1",
            "alice",
            EmailAddress: "alice@example.com",
            SuspendedAt: DateTimeOffset.UnixEpoch));

    Assert.Equal("Suspended", Find<Label>(row, "user-card-status").Text);
    Assert.True(Find<Label>(row, "user-card-email").IsVisible);
    Assert.Equal("alice@example.com", Find<Label>(row, "user-card-email").Text);

    page.Dispose();
  }

  [Fact]
  public void HidesManageLinkForNonAdministrators()
  {
    var (page, row) = CreatePageWithRow(
        administrator: false,
        new UserSearchResult(
            "user-1",
            "alice",
            EmailAddress: "alice@example.com",
            SuspendedAt: DateTimeOffset.UnixEpoch));

    var manage = Find<Button>(row, "user-card-manage-link");
    Assert.False(manage.IsVisible);
    Assert.False(Find<Label>(row, "user-card-status").IsVisible);
    Assert.False(Find<Label>(row, "user-card-email").IsVisible);

    page.Dispose();
  }

  [Theory]
  [InlineData(AccountType.Official, "Official", "shared.accountType.official")]
  [InlineData(AccountType.System, "System", "shared.accountType.system")]
  [InlineData(AccountType.AiAgent, "AI Agent", "shared.accountType.aiAgent")]
  [InlineData(null, null, null)]
  public void ShowsAccountClassificationAndClearsItWhenRowIsReused(
      AccountType? accountType, string? expected, string? messageKey)
  {
    var (page, row) = CreatePageWithRow(
        administrator: false,
        new UserSearchResult("user-1", "alice", AccountType: accountType));

    var badge = Find<Label>(row, "user-card-account-type");
    Assert.Equal(expected, badge.Text);
    Assert.Equal(expected is not null, badge.IsVisible);

    if (messageKey is not null)
    {
      row.Resources[messageKey] = "Translated classification";
      Assert.Equal("Translated classification", badge.Text);
    }

    row.BindingContext = new UserSearchResult("user-2", "bob");
    Assert.Null(badge.Text);
    Assert.False(badge.IsVisible);
    if (accountType is not null)
    {
      row.BindingContext = new UserSearchResult("user-3", "charlie", AccountType: accountType);
      Assert.Equal("Translated classification", badge.Text);
      Assert.True(badge.IsVisible);
    }
    page.Dispose();
  }

  [Fact]
  public void HidesAdminFieldsWhenViewerLosesAdministratorRole()
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    var (page, row) = CreatePageWithRow(
        viewerProvider,
        administrator: true,
        new UserSearchResult(
            "user-1",
            "alice",
            EmailAddress: "alice@example.com",
            SuspendedAt: DateTimeOffset.UnixEpoch));

    viewerProvider.SetViewer(new NavigationViewer(true, ["member"]));

    Assert.False(Find<Button>(row, "user-card-manage-link").IsVisible);
    Assert.False(Find<Label>(row, "user-card-status").IsVisible);
    Assert.Equal(string.Empty, Find<Label>(row, "user-card-status").Text);
    Assert.False(Find<Label>(row, "user-card-email").IsVisible);
    Assert.Equal(string.Empty, Find<Label>(row, "user-card-email").Text);
    page.Dispose();
  }

  [Fact]
  public void ManageLinkNavigatesToUserAdminRouteOnTap()
  {
    var (page, row) = CreatePageWithRow(administrator: true);
    NativeRoutePath? captured = null;
    page.SetNavigator(path => { captured = path; return Task.CompletedTask; });

    Find<Button>(row, "user-card-manage-link").SendClicked();

    Assert.Equal("/user/alice/admin", captured!.Value.Value);
    page.Dispose();
  }

  private static (UsersBrowsePage Page, View Row) CreatePageWithRow(
      bool administrator,
      UserSearchResult? user = null) =>
      CreatePageWithRow(new MutableNavigationViewerProvider(), administrator, user);

  private static (UsersBrowsePage Page, View Row) CreatePageWithRow(
      MutableNavigationViewerProvider viewerProvider,
      bool administrator,
      UserSearchResult? user = null)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var controller = new UiLocaleController(new Languages());
    var localization = new UiLocalization(controller);
    viewerProvider.SetViewer(administrator
        ? new NavigationViewer(true, ["administrator"])
        : new NavigationViewer(true, ["member"]));
    var model = new UsersBrowseViewModel(new Service(), viewerProvider, localization);
    var page = new UsersBrowsePage(model, localization);

    var content = Assert.IsAssignableFrom<View>(
        Assert.Single(Descendants<CollectionView>(page)).ItemTemplate.CreateContent());
    foreach (var type in new[] { AccountType.Official, AccountType.System, AccountType.AiAgent })
    {
      var key = AccountTypeLabels.MessageKey(type)!.Value;
      content.Resources[key.Value] = localization.Localize(key);
    }
    content.BindingContext = user ?? new UserSearchResult("user-1", "alice");
    return (page, content);
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Descendants<T>(root).Single(value => value.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private sealed class Languages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class Service : IUsersSearchService
  {
    public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new UsersSearchResponse([new UserSearchResult("user-1", "alice")], new PageInfo(null, false, null)));
  }

  private sealed class PagedService(params UsersSearchResponse[] pages) : IUsersSearchService
  {
    private int index;
    public List<SearchUsersRequest> Requests { get; } = [];

    public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default)
    {
      Requests.Add(request);
      return Task.FromResult(pages[index++]);
    }
  }
}
