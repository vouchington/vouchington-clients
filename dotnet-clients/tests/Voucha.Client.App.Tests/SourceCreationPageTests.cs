using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App;
using Voucha.Client.Core.NewsFeeds;
using System.Net;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SourceCreation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SourceCreationPageTests
{
  [Fact]
  public async Task RenderedControlsShowPendingRetryAndFinalNavigation()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var service = new Service();
    using var model = new SourceCreationViewModel(service, new Session(), UiLocalization.English,
        (_, _) => Task.CompletedTask);
    string? opened = null;
    using var page = new LifecyclePage(model, slug => { opened = slug; return Task.CompletedTask; });
    page.FindByName<Entry>("UrlEntry").Text = "bad";
    page.FindByName<Button>("CreateButton").SendClicked();
    Assert.NotEmpty(page.FindByName<Label>("ErrorLabel").Text);
    page.FindByName<Entry>("UrlEntry").Text = "https://example.test/feed.xml";
    await page.OpenAsync();
    Assert.Null(opened);
    var operation = page.CreateAsync();
    page.FindByName<Button>("CreateButton").SendClicked();
    Assert.True(page.FindByName<ActivityIndicator>("ProgressIndicator").IsRunning);
    Assert.False(page.FindByName<Button>("CreateButton").IsEnabled);
    service.Pending.SetException(new HttpRequestException());
    await operation;

    Assert.NotEmpty(page.FindByName<Label>("StatusLabel").Text);
    Assert.NotEmpty(page.FindByName<Label>("ErrorLabel").Text);
    Assert.True(page.FindByName<Button>("RetryButton").IsVisible);
    page.FindByName<Button>("RetryButton").SendClicked();
    await page.RetryAsync();
    Assert.True(page.FindByName<Button>("OpenButton").IsVisible);
    Assert.False(page.FindByName<Button>("RetryButton").IsVisible);
    Assert.Null(page.FindByName<Label>("ErrorLabel").Text);
    Assert.Equal("Source created", page.FindByName<Label>("StatusLabel").Text);
    page.FindByName<Button>("OpenButton").SendClicked();
    Assert.Equal("source-topic", opened);
    Assert.Equal(1, service.Posts);
    page.Disappear();
    Assert.False(model.IsBusy);
  }

  [Fact]
  public async Task NewsFeedsEntryCreatesNativePageAndOpensResolvedSource()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" }) app.Resources[key] = new Style(typeof(Label));
    using var http = new HttpClient(new CreateHandler()) { BaseAddress = new("https://api.test") };
    var client = new VouchaApiClient(http);
    var shell = new AppShell();
    using var provider = new ServiceCollection().AddSingleton(http).AddSingleton(client)
        .AddSingleton<IUiLocalization>(UiLocalization.English).AddSingleton(shell).BuildServiceProvider();
    using var model = new NewsFeedsViewModel(new ApiNewsFeedService(client));
    var page = new NewsFeedsPage(model, new Session(), provider, null!);
    var navigation = new NavigationPage(page);
    page.FindByName<Button>("AddSourceButton").SendClicked();
    using var creation = Assert.IsType<SourceCreationPage>(navigation.Navigation.NavigationStack.Last());
    creation.FindByName<Entry>("UrlEntry").Text = "https://example.test/feed.xml";
    await creation.CreateAsync();
    await creation.OpenAsync();
    Assert.Equal("/source/source-topic", shell.LastOpenedPath);
    await navigation.PopAsync();
    Assert.Null(creation.BindingContext);
  }

  private sealed class CreateHandler : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var body = request.Method == HttpMethod.Post
          ? "{\"import\":{\"id\":\"import\",\"total_rows\":1,\"pending_rows\":1},\"status_url\":\"/api/v1/my/import/rss-feeds/import\"}"
          : "{\"import\":{\"id\":\"import\",\"total_rows\":1,\"completed_rows\":1,\"pending_rows\":0},\"rows\":[{\"id\":\"row\",\"input\":\"https://example.test/feed.xml\",\"status\":\"source_created\",\"entity_id\":\"feed\",\"topic_slug\":\"source-topic\"}]}";
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
  }

  private sealed class LifecyclePage(SourceCreationViewModel model, Func<string, Task> open)
      : SourceCreationPage(model, open)
  {
    public void Disappear() => OnDisappearing();
  }

  private sealed class Service : ISourceCreationService
  {
    public Uri ApiOrigin { get; } = new("https://api.test");
    public TaskCompletionSource<RssFeedImportStatus> Pending { get; } = new();
    public int Posts { get; private set; }
    private int reads;
    public Task<RssFeedImportSubmission> CreateAsync(Uri url, CancellationToken token)
    {
      Posts++;
      return Task.FromResult(new RssFeedImportSubmission(new("import", 1, 0, 0, 1, null, DateTimeOffset.UtcNow),
          new Uri("/api/v1/my/import/rss-feeds/import", UriKind.Relative)));
    }
    public Task<RssFeedImportStatus> ReadAsync(string importId, CancellationToken token) => ++reads == 1 ? Pending.Task :
        Task.FromResult(new RssFeedImportStatus(new("import", 1, 1, 0, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
          [new("row", "https://example.test/feed.xml", ImportResultStatus.SourceCreated, null, "feed", null, TopicSlug: "source-topic")]));
    public Task<CreatedSourceDetail> ResolveAsync(string feedId, CancellationToken token) => throw new InvalidOperationException();
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("member", "member"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
