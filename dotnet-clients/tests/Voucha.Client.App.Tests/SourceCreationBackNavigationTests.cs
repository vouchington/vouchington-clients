using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.SourceCreation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SourceCreationBackNavigationTests
{
  [Theory]
  [InlineData("unsubmitted")]
  [InlineData("failed")]
  [InlineData("working")]
  public async Task NativeBackRemainsAvailableAndPoppingDisposesCreation(string state)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in new[] { "Body", "Eyebrow", "Headline", "Metadata" }) app.Resources[key] = new Style(typeof(Label));
    using var handler = new ServiceHandler(state);
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var client = new VouchaApiClient(http);
    using var services = new ServiceCollection().AddSingleton(http).AddSingleton(client)
        .AddSingleton<IUiLocalization>(UiLocalization.English).BuildServiceProvider();
    using var news = new NewsFeedsViewModel(new ApiNewsFeedService(client));
    var owner = new NewsFeedsPage(news, new Session(), services, null!);
    var navigation = new NavigationPage(owner);

    await owner.AddSourceAsync();
    using var creation = Assert.IsType<SourceCreationPage>(navigation.Navigation.NavigationStack.Last());
    var model = Assert.IsType<SourceCreationViewModel>(creation.BindingContext);
    Task operation = Task.CompletedTask;
    if (state != "unsubmitted")
    {
      creation.FindByName<Entry>("UrlEntry").Text = "https://example.test/feed.xml";
      operation = creation.CreateAsync();
      if (state == "failed") await operation;
    }
    Assert.Equal(state == "working", model.IsBusy);
    Assert.Equal(state == "unsubmitted", model.CanCreate);
    Assert.False(model.CanOpen);
    Assert.False(model.CanRetry);
    Assert.True(Shell.GetNavBarIsVisible(creation));
    var back = Shell.GetBackButtonBehavior(creation);
    Assert.True(back?.IsVisible ?? true);
    Assert.True(back?.IsEnabled ?? true);
    Assert.True(NavigationPage.GetHasNavigationBar(creation));
    Assert.True(NavigationPage.GetHasBackButton(creation));
    Assert.Equal(2, navigation.Navigation.NavigationStack.Count);

    Assert.Same(creation, await navigation.PopAsync());
    await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.Same(owner, Assert.Single(navigation.Navigation.NavigationStack));
    Assert.Null(creation.BindingContext);
    Assert.False(model.IsBusy);
    Assert.False(model.CanCreate);
    Assert.False(model.CanOpen);
    Assert.False(model.CanRetry);
    Assert.Equal(state == "working", handler.CancellationObserved);
    Assert.Equal(state == "unsubmitted" ? 0 : 1, handler.Posts);
    Assert.Equal(state == "failed" ? 1 : 0, handler.StatusReads);
  }

  private sealed class ServiceHandler(string state) : HttpMessageHandler
  {
    private readonly TaskCompletionSource<HttpResponseMessage> pending = new();
    public int Posts { get; private set; }
    public int StatusReads { get; private set; }
    public bool CancellationObserved { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      if (request.Method == HttpMethod.Post)
      {
        Posts++;
        return state == "working" ? WaitForCancellationAsync(cancellationToken)
            : Respond(HttpStatusCode.Accepted, new RssFeedImportSubmission(
                new("import", 1, 0, 0, 1, null, DateTimeOffset.UnixEpoch),
                new("/api/v1/my/import/rss-feeds/import", UriKind.Relative)));
      }
      StatusReads++;
      return Respond(HttpStatusCode.OK, new RssFeedImportStatus(
          new("import", 1, 0, 1, 0, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
          [new("row", "https://example.test/feed.xml", ImportResultStatus.Error, null, null, null,
              RssFeedImportErrorCode.ImportFailed)]));
    }

    private async Task<HttpResponseMessage> WaitForCancellationAsync(CancellationToken token)
    {
      try { return await pending.Task.WaitAsync(token).ConfigureAwait(false); }
      catch (OperationCanceledException) { CancellationObserved = true; throw; }
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
