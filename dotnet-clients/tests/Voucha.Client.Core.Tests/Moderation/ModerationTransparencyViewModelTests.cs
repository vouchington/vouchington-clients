using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationTransparencyViewModelTests
{
  [Fact]
  public async Task LoadAsyncRendersAggregateRowsAndReplacesPreviousRows()
  {
    var handler = new RecordingHandler("""{"range":"30d","buckets":[{"date":"2026-08-01","metric":"reports","category":"all","count":20}]}""");
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("Reports", viewModel.Items.Single().Title);
    Assert.Contains("20", viewModel.Items.Single().Detail);
    Assert.Contains("Other", viewModel.Items.Single().Detail);
    Assert.Equal("/api/v1/moderation-transparency?range=30d", handler.Requests.Single().PathAndQuery);
  }

  [Theory]
  [InlineData("/moderation-transparency?range=today", "today")]
  [InlineData("/moderation-transparency?range=7d", "7d")]
  [InlineData("/moderation-transparency?range=30d", "30d")]
  [InlineData("/moderation-transparency?range=90d", "90d")]
  [InlineData("/moderation-transparency?range=all", "all")]
  [InlineData("/moderation-transparency?range=invalid", "30d")]
  public void TransparencyRouteValidatesItsRange(string path, string expectedRange)
  {
    Assert.True(ModerationRoutes.TryResolve(path, out var context));

    Assert.Equal(expectedRange, context.TransparencyRange);
  }

  [Fact]
  public async Task SelectTransparencyRangeReloadsTheSelectedPeriod()
  {
    var handler = new RecordingHandler("""{"range":"7d","buckets":[]}""");
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);

    await viewModel.SelectTransparencyRangeAsync(ModerationTransparencyRange.SevenDays, TestContext.Current.CancellationToken);

    Assert.Equal(ModerationTransparencyRange.SevenDays, viewModel.TransparencyRange);
    Assert.Equal("/api/v1/moderation-transparency?range=7d", handler.Requests.Single().PathAndQuery);
  }

  [Fact]
  public async Task AllTimeTransparencyFormatsMonthlyCohortsAsMonthAndYear()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    var handler = new RecordingHandler("""{"range":"all","buckets":[{"date":"2026-08-01","metric":"reports","category":"spam","count":20}]}""");
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(Client(handler)),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency?range=all", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains("August 2026", viewModel.Items.Single().Detail);
    Assert.DoesNotContain("8/1/2026", viewModel.Items.Single().Detail, StringComparison.Ordinal);
    controller.ApplySavedLocale("fr");
    Assert.Contains("août 2026", viewModel.Items.Single().Detail, StringComparison.Ordinal);
  }

  [Fact]
  public async Task LoadAsyncPreservesEachTransparencyBucketDimension()
  {
    var handler = new RecordingHandler("""{"range":"30d","buckets":[{"date":"2026-08-01","metric":"moderation_actions","category":"remove","count":20},{"date":"2026-08-02","metric":"moderation_actions","category":"warn","count":25}]}""");
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, viewModel.Items.Count);
    Assert.Contains(viewModel.Items, row => row.Detail.Contains("Remove", StringComparison.Ordinal));
    Assert.Contains(viewModel.Items, row => row.Detail.Contains("Warn", StringComparison.Ordinal));
  }

  [Fact]
  public async Task LocaleChangeReformatsAlreadyLoadedAggregateCount()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(Client(new RecordingHandler(
            """{"range":"30d","buckets":[{"date":"2026-08-01","metric":"reports","category":"all","count":1000}]}"""))),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains("1,000", viewModel.Items.Single().Detail);
    controller.ApplySavedLocale("fr");

    Assert.Contains("1 000", viewModel.Items.Single().Detail);
  }

  [Fact]
  public async Task LoadAsyncTreatsForbiddenAsLockedAndClearsAggregateRows()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    var handler = new RecordingHandler([
      new("""{"range":"30d","buckets":[{"date":"2026-08-01","metric":"reports","category":"spam","count":20}]}"""),
      new("{}", HttpStatusCode.Forbidden),
    ]);
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(Client(handler)),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Contains("Spam", viewModel.Items.Single().Detail);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Single(viewModel.Items);
    Assert.Contains("Plus", viewModel.Items.Single().Detail);
    controller.ApplySavedLocale("fr");
    Assert.Single(viewModel.Items);
    Assert.DoesNotContain("Spam", viewModel.Items.Single().Detail, StringComparison.Ordinal);
  }

  [Fact]
  public async Task LoadAsyncTreatsMissingEndpointAsLocked()
  {
    var handler = new RecordingHandler("{}", HttpStatusCode.NotFound);
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Single(viewModel.Items);
    Assert.Contains("Plus", viewModel.Items.Single().Detail);
  }

  [Fact]
  public async Task FailedReloadClearsCachedTransparencyBuckets()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    var handler = new RecordingHandler([
      new("""{"range":"30d","buckets":[{"date":"2026-08-01","metric":"reports","category":"spam","count":20}]}"""),
      new("{}", HttpStatusCode.InternalServerError),
    ]);
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(Client(handler)),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    controller.ApplySavedLocale("fr");

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task StaleRangeResponseCannotReplaceTheRelocalizationCache()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    var handler = new DelayedTransparencyHandler();
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(new VouchaApiClient(new HttpClient(handler)
        {
          BaseAddress = new Uri("https://api.test"),
        })),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);

    var olderLoad = viewModel.SelectTransparencyRangeAsync(
        ModerationTransparencyRange.All,
        TestContext.Current.CancellationToken);
    await handler.OlderRequestStarted.Task;
    await viewModel.SelectTransparencyRangeAsync(
        ModerationTransparencyRange.SevenDays,
        TestContext.Current.CancellationToken);
    handler.CompleteOlderResponse();
    await olderLoad;

    Assert.Equal("transparency-2026-08-14-reports-harassment", viewModel.Items.Single().Id);
    controller.ApplySavedLocale("fr");
    Assert.Equal("transparency-2026-08-14-reports-harassment", viewModel.Items.Single().Id);
  }

  [Fact]
  public async Task ContinuationEntitlementLossReplacesPreviouslyLoadedRows()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    var handler = new RecordingHandler([
      new("""{"range":"all","buckets":[{"date":"2026-08-01","metric":"reports","category":"spam","count":20}],"next_cursor":"older-page"}"""),
      new("{}", HttpStatusCode.Forbidden),
    ]);
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(Client(handler)),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency?range=all", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal("locked", viewModel.Items.Single().Id);
    Assert.False(viewModel.HasMore);
    controller.ApplySavedLocale("fr");
    Assert.Equal("locked", viewModel.Items.Single().Id);
  }

  [Fact]
  public async Task StaleCanceledRangeLoadCannotResetNewerLoadedState()
  {
    var handler = new DelayedTransparencyHandler();
    var viewModel = new ModerationViewModel(new ApiModerationService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") })));
    Assert.True(ModerationRoutes.TryResolve("/moderation-transparency", out var context));
    viewModel.SetContext(context);
    using var olderCancellation = new CancellationTokenSource();

    var olderLoad = viewModel.SelectTransparencyRangeAsync(
        ModerationTransparencyRange.All,
        olderCancellation.Token);
    await handler.OlderRequestStarted.Task;
    await viewModel.SelectTransparencyRangeAsync(
        ModerationTransparencyRange.SevenDays,
        TestContext.Current.CancellationToken);
    await olderCancellation.CancelAsync();
    await olderLoad;

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("transparency-2026-08-14-reports-harassment", viewModel.Items.Single().Id);
  }

  private static VouchaApiClient Client(RecordingHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private sealed class EnglishDeviceLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }

  private sealed class DelayedTransparencyHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource<HttpResponseMessage> olderResponse = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource OlderRequestStarted { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public void CompleteOlderResponse() => olderResponse.SetResult(Response(
        """{"range":"all","buckets":[{"date":"2025-08-01","metric":"reports","category":"spam","count":30}]}"""));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      if (request.RequestUri?.Query.Contains("range=all", StringComparison.Ordinal) == true)
      {
        OlderRequestStarted.SetResult();
        return olderResponse.Task.WaitAsync(cancellationToken);
      }
      return Task.FromResult(Response(
          """{"range":"7d","buckets":[{"date":"2026-08-14","metric":"reports","category":"harassment","count":25}]}"""));
    }

    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
  }
}
