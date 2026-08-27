using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class BookmarkCollectionPaginationFooterTests
{
  [Fact]
  public async Task RendersLoadMoreAndRetryControlsForPostBookmarks()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        [UiMessageKey.NativeSwiftCommonLoadMore.Value] = "Load more",
        [UiMessageKey.NativeSwiftCommonTryAgain.Value] = "Try again",
      },
    };
    var handler = new Handler(
        (HttpStatusCode.OK, Fixture("native.bookmarks.posts.saved.default.json")),
        (HttpStatusCode.ServiceUnavailable, "offline"),
        (HttpStatusCode.OK, Fixture("native.bookmarks.posts.saved.next-page.json")));
    var viewModel = new BookmarkCollectionViewModel(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        new SessionStore());
    viewModel.SetContext(new BookmarkCollectionRouteContext(
        "/my/posts/saved", UiText.Verbatim("Saved posts"), BookmarkCollectionKind.Posts, "saved"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var footer = new BookmarkCollectionPaginationFooter(viewModel);
    var loadMore = Find<Button>(footer, "bookmark-collection-load-more");
    var retry = Find<Button>(footer, "bookmark-collection-retry");

    Assert.True(loadMore.IsVisible);
    Assert.False(retry.IsVisible);
    Assert.Equal("Load more", loadMore.Text);
    loadMore.SendClicked();
    await WaitUntilAsync(() => viewModel.HasContinuationError);

    Assert.Equal(["post-1", "post-2"], viewModel.Rows.Select(row => row.Id));
    Assert.False(loadMore.IsVisible);
    Assert.True(retry.IsVisible);
    Assert.True(retry.IsEnabled);
    Assert.Equal("Try again", retry.Text);

    retry.SendClicked();
    await WaitUntilAsync(() => handler.RequestCount == 3 && !viewModel.IsLoadingMore);

    Assert.Equal(["post-1", "post-2", "post-3"], viewModel.Rows.Select(row => row.Id));
    Assert.False(retry.IsVisible);
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10);
    Assert.True(condition());
  }

  private static string Fixture(string file, [CallerFilePath] string sourceFile = "") =>
      File.ReadAllText(Path.Combine(RepoRoot(sourceFile), "api-fixtures", "v1", "responses", file));

  private static string RepoRoot(string sourceFile)
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (directory is not null)
    {
      if (Directory.Exists(Path.Combine(directory.FullName, "api-fixtures"))) return directory.FullName;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find repository root from test source path.");
  }

  private sealed class SessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User(
        "user-1", "alice", Roles: ["member"], EmailAddress: "alice@example.com", MembershipPlan: "membership"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class Handler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
  {
    private readonly Queue<(HttpStatusCode Status, string Body)> queue = new(responses);
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      RequestCount++;
      var response = queue.Dequeue();
      return Task.FromResult(new HttpResponseMessage(response.Status)
      {
        Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      });
    }
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => new Timer();
  }

  private sealed class Timer : IDispatcherTimer
  {
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler? Tick;
    public void Start() { IsRunning = true; Tick?.Invoke(this, EventArgs.Empty); IsRunning = false; }
    public void Stop() => IsRunning = false;
  }
}
