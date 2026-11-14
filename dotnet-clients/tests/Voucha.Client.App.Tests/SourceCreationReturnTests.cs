using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.SourceCreation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SourceCreationReturnTests
{
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task ReturningFromCreationReloadsSourcesThroughOwnerAppearance(bool complete)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" }) app.Resources[key] = new Style(typeof(Label));
    using var handler = new CreationAndListHandler();
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var client = new VouchaApiClient(http);
    using var provider = new ServiceCollection().AddSingleton(http).AddSingleton(client)
        .AddSingleton<IUiLocalization>(UiLocalization.English).AddSingleton(new AppShell()).BuildServiceProvider();
    using var model = new NewsFeedsViewModel(new ApiNewsFeedService(client), NewsFeedScope.AllSources);
    var page = new AppearingPage(model, new Session(), provider);
    var navigation = new NavigationPage(page);
    await AppearAsync(page, model);
    Assert.Empty(model.Items);
    Assert.Equal(1, handler.ListReads);

    await page.AddSourceAsync();
    using var creation = Assert.IsType<SourceCreationPage>(navigation.Navigation.NavigationStack.Last());
    if (complete)
    {
      creation.FindByName<Entry>("UrlEntry").Text = "https://example.test/feed.xml";
      await creation.CreateAsync();
      Assert.True(Assert.IsType<SourceCreationViewModel>(creation.BindingContext).CanOpen);
    }
    Assert.Equal(1, handler.ListReads);
    await navigation.PopAsync();
    Assert.Null(creation.BindingContext);
    await AppearAsync(page, model);

    Assert.Equal(2, handler.ListReads);
    Assert.Equal(complete ? 1 : 0, handler.Posts);
    Assert.Null(model.ErrorMessage);
    var rendered = Assert.IsAssignableFrom<IEnumerable<NewsFeedItem>>(page.FindByName<CollectionView>("FeedItemsView").ItemsSource);
    if (complete) Assert.Equal("created-feed", Assert.Single(rendered).Id);
    else Assert.Empty(rendered);
  }

  private static async Task AppearAsync(AppearingPage page, NewsFeedsViewModel model)
  {
    var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void Changed(object? sender, PropertyChangedEventArgs args)
    {
      if (args.PropertyName == nameof(model.IsLoading) && !model.IsLoading) loaded.TrySetResult();
    }
    model.PropertyChanged += Changed;
    try
    {
      page.Appear();
      await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
    finally { model.PropertyChanged -= Changed; }
  }

  private sealed class AppearingPage(NewsFeedsViewModel model, ISessionStore session, IServiceProvider services)
      : NewsFeedsPage(model, session, services, null!)
  {
    public void Appear() => OnAppearing();
  }

  private sealed class CreationAndListHandler : HttpMessageHandler
  {
    public int ListReads { get; private set; }
    public int Posts { get; private set; }
    private bool created;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      if (request.Method == HttpMethod.Get && path == "/api/v1/rss-feeds")
      {
        ListReads++;
        var feeds = new RssFeedsResponse(created
            ? [new("created-feed", "Created source", NewsFeedSourceType.Article, null, null, null)] : [], new(null, false, null));
        return Respond(HttpStatusCode.OK, feeds);
      }
      if (request.Method == HttpMethod.Post && path == "/api/v1/rss-feeds")
      {
        Posts++;
        return Respond(HttpStatusCode.Accepted, new RssFeedImportSubmission(
            new("import", 1, 0, 0, 1, null, DateTimeOffset.UnixEpoch), new("/api/v1/my/import/rss-feeds/import", UriKind.Relative)));
      }
      if (request.Method == HttpMethod.Get && path == "/api/v1/my/import/rss-feeds/import")
      {
        created = true;
        return Respond(HttpStatusCode.OK, new RssFeedImportStatus(
            new("import", 1, 1, 0, 0, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
            [new("row", "https://example.test/feed.xml", ImportResultStatus.SourceCreated, null, "created-feed", null, TopicSlug: "created-topic")]));
      }
      throw new InvalidOperationException("Unexpected request: " + path);
    }

    private static Task<HttpResponseMessage> Respond<T>(HttpStatusCode code, T value) =>
        Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(JsonSerializer.Serialize(value, VouchaApiJson.Options)) });
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("member", "member"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
