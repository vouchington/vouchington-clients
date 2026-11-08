using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ModerationQueueReappearanceTests
{
  [Fact]
  public async Task AppealsRetainPagesAndCursorAfterDetailResolutionAndReappearance()
  {
    Prepare();
    var service = new ModerationModalAppealsService();
    service.Current = service.Current with { Status = ModerationAppealStatus.Pending, SentAt = DateTimeOffset.UtcNow };
    var first = Summary<ModerationAppealSummary>(service.Current);
    service.Pages.Enqueue(new([first], new PageInfo("page-1", true, null)));
    service.Pages.Enqueue(new([first with { Id = "second" }], new PageInfo("page-2", true, null)));
    service.Pages.Enqueue(new([first with { Id = "third" }], new PageInfo(null, false, null)));
    using var model = new ModerationAppealsViewModel(service, new NavigationViewer(true, ["administrator"]));
    using var page = new ModerationAppealsPage(model);
    Lifecycle(page, "OnAppearing");
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal([first.Id, "second"], AppealRows(page));
    Lifecycle(page, "OnDisappearing");
    var detail = await model.FetchDetailAsync(first.Id, TestContext.Current.CancellationToken);
    service.Current = detail with { Status = ModerationAppealStatus.Resolved };
    Assert.True(model.CanResolve(detail, ModerationAppealAction.Deny));
    await model.ResolveAsync(detail, ModerationAppealAction.Deny, TestContext.Current.CancellationToken);

    Lifecycle(page, "OnAppearing");

    Assert.Equal(["second"], AppealRows(page));
    Assert.Equal(2, service.Cursors.Count);
    Assert.True(model.HasMore);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "page-1", "page-2"], service.Cursors);
    Assert.Equal(["second", "third"], AppealRows(page));
    service.Pages.Enqueue(new([first], new PageInfo(null, false, null)));
    await page.ReloadAsync();
    Assert.Null(service.Cursors.Last());
    Assert.Equal([first.Id], AppealRows(page));
  }

  [Fact]
  public async Task DisputesRetainPagesAndCursorAfterDetailResolutionAndReappearance()
  {
    Prepare();
    var service = new ModerationModalDisputesService();
    service.Current = service.Current with { Status = ModerationDisputeStatus.Pending, SentAt = DateTimeOffset.UtcNow };
    var first = Summary<ModerationDisputeSummary>(service.Current);
    service.Pages.Enqueue(new([first], new PageInfo("page-1", true, null)));
    service.Pages.Enqueue(new([first with { Id = "second" }], new PageInfo("page-2", true, null)));
    service.Pages.Enqueue(new([first with { Id = "third" }], new PageInfo(null, false, null)));
    using var model = new ModerationDisputesViewModel(service, new NavigationViewer(true, ["administrator"]));
    using var page = new ModerationDisputesPage(model);
    Lifecycle(page, "OnAppearing");
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal([first.Id, "second"], DisputeRows(page));
    Lifecycle(page, "OnDisappearing");
    var detail = await model.FetchDetailAsync(first.Id, TestContext.Current.CancellationToken);
    service.Current = detail with { Status = ModerationDisputeStatus.Resolved };
    Assert.True(model.CanResolve(detail, ModerationDisputeResolutionAction.Dismiss));
    await model.ResolveAsync(detail, ModerationDisputeResolutionAction.Dismiss, TestContext.Current.CancellationToken);

    Lifecycle(page, "OnAppearing");

    Assert.Equal(["second"], DisputeRows(page));
    Assert.Equal(2, service.Cursors.Count);
    Assert.True(model.HasMore);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "page-1", "page-2"], service.Cursors);
    Assert.Equal(["second", "third"], DisputeRows(page));
    service.Pages.Enqueue(new([first], new PageInfo(null, false, null)));
    await page.ReloadAsync();
    Assert.Null(service.Cursors.Last());
    Assert.Equal([first.Id], DisputeRows(page));
  }

  [Fact]
  public void AppealReappearanceReloadsAfterAccountInvalidationButNeverForSignedOutViewer()
  {
    Prepare();
    var provider = new MutableNavigationViewerProvider();
    var viewer = new NavigationViewer(true, ["administrator"], IdentityId: "staff-1");
    provider.SetViewer(viewer);
    var service = new ModerationModalAppealsService();
    var first = Summary<ModerationAppealSummary>(service.Current);
    service.Pages.Enqueue(new([first], new PageInfo("page-1", true, null)));
    using var model = new ModerationAppealsViewModel(service, viewer, viewerProvider: provider);
    using var page = new ModerationAppealsPage(model);
    Lifecycle(page, "OnAppearing");
    Lifecycle(page, "OnDisappearing");
    provider.SetViewer(new NavigationViewer(false, []));
    Lifecycle(page, "OnAppearing");
    Assert.Empty(model.Summaries);
    Assert.Single(service.Cursors);
    provider.SetViewer(new NavigationViewer(true, ["administrator"], IdentityId: "staff-2"));
    Lifecycle(page, "OnAppearing");
    Assert.Single(service.Cursors);
    provider.SetViewer(viewer);
    service.Pages.Enqueue(new([first with { Id = "refreshed-account" }], new PageInfo(null, false, null)));
    Lifecycle(page, "OnAppearing");
    Assert.Equal([null, null], service.Cursors);
    Assert.Equal(["refreshed-account"], AppealRows(page));
  }

  private static T Summary<T>(object detail) => JsonSerializer.SerializeToElement(detail, VouchaApiJson.Options)
      .Deserialize<T>(VouchaApiJson.Options)!;

  private static string[] AppealRows(Page page) => Assert.Single(VisualTreeElements.Descendants<CollectionView>(page))
      .ItemsSource.Cast<ModerationAppealSummary>().Select(row => row.Id).ToArray();

  private static string[] DisputeRows(Page page) => Assert.Single(VisualTreeElements.Descendants<CollectionView>(page))
      .ItemsSource.Cast<ModerationDisputeSummary>().Select(row => row.Id).ToArray();

  private static void Prepare()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    Application.Current ??= new Application();
  }

  private static void Lifecycle(Page page, string method) => page.GetType()
      .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, null);
}
