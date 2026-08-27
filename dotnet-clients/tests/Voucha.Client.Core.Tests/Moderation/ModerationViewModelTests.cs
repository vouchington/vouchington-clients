using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationViewModelTests
{
  [Theory]
  [MemberData(nameof(RouteCases))]
  public async Task LoadAsyncMapsModerationRoutesToRows(
      string path,
      string fixtureId,
      string expectedRequest,
      string expectedTitle,
      string expectedDetail)
  {
    var (viewModel, handler) = CreateViewModel(fixtureId);
    Assert.True(ModerationRoutes.TryResolve(path, out var context));

    viewModel.SetContext(context);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasContext);
    Assert.True(viewModel.HasItems);
    Assert.False(viewModel.IsLoading);
    Assert.False(viewModel.HasError);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal(context.Title, viewModel.Title);
    Assert.Equal(expectedRequest, handler.Requests.Single().PathAndQuery);
    Assert.Equal(expectedTitle, viewModel.Items[0].Title);
    Assert.Equal(expectedDetail, viewModel.Items[0].Detail);
  }

  [Fact]
  public async Task LoadAsyncKeepsIdleWithoutContext()
  {
    var (viewModel, handler) = CreateViewModel("native.moderation.appeals.default");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.False(viewModel.HasItems);
    Assert.Empty(handler.Requests);
    Assert.Equal("Moderation", viewModel.Title);
  }

  [Fact]
  public void LocaleChangeRefreshesVisibleRouteTitle()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(Client(new RecordingHandler("{}"))),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/appeals", out var context));
    viewModel.SetContext(context);
    var titleChanged = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ModerationViewModel.Title)) titleChanged++;
    };

    controller.ApplySavedLocale("fr");

    Assert.Equal("Appels", viewModel.Title);
    Assert.Equal(1, titleChanged);
  }

  [Fact]
  public async Task LocaleChangeRefreshesAlreadyLoadedModerationRowCopy()
  {
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    using var viewModel = new ModerationViewModel(
        new ApiModerationService(Client(new RecordingHandler(
            """{"results":[{"id":"case-1"},{"id":"case-2"}]}"""))),
        new UiLocalization(controller),
        controller);
    Assert.True(ModerationRoutes.TryResolve("/my/warnings", out var context));
    viewModel.SetContext(context);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("2 items", viewModel.Items.Single().Detail);
    controller.ApplySavedLocale("es");

    Assert.Equal("2 elementos", viewModel.Items.Single().Detail);
  }

  [Fact]
  public async Task LoadAsyncSurfacesApiErrors()
  {
    var handler = new RecordingHandler("{}", System.Net.HttpStatusCode.InternalServerError);
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/appeals", out var context));

    viewModel.SetContext(context);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.False(viewModel.HasItems);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.NotNull(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task LoadMoreAsyncForwardsCursorAndAppendsStableRows()
  {
    const string firstFixtureId = "native.moderation.appeals.default";
    const string secondFixtureId = "native.moderation.appeals.page-2";
    var firstPage = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse(firstFixtureId), VouchaApiJson.Options)!;
    var secondPage = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse(secondFixtureId), VouchaApiJson.Options)!;
    var handler = new RecordingHandler([
      new(ApiFixtureLoader.LoadResponse(firstFixtureId)),
      new(ApiFixtureLoader.LoadResponse(secondFixtureId)),
    ]);
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/appeals", out var context));
    viewModel.SetContext(context);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMore);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(
        firstPage.Appeals.Concat(secondPage.Appeals).Select(appeal => appeal.Id),
        viewModel.Items.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
    var after = ApiFixtureLoader.QueryValue(secondFixtureId, "after");
    Assert.Equal(
        $"/api/v1/appeals?after={after}&limit=25&status=pending",
        handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task LoadMoreAsyncPreservesRowsAndCursorForRetry()
  {
    const string firstFixtureId = "native.moderation.appeals.default";
    const string secondFixtureId = "native.moderation.appeals.page-2";
    var firstPage = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse(firstFixtureId), VouchaApiJson.Options)!;
    var secondPage = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse(secondFixtureId), VouchaApiJson.Options)!;
    var handler = new RecordingHandler([
      new(ApiFixtureLoader.LoadResponse(firstFixtureId)),
      new("{}", System.Net.HttpStatusCode.InternalServerError),
      new(ApiFixtureLoader.LoadResponse(secondFixtureId)),
    ]);
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve("/appeals", out var context));
    viewModel.SetContext(context);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(firstPage.Appeals.Select(appeal => appeal.Id), viewModel.Items.Select(item => item.Id));
    Assert.True(viewModel.HasPaginationError);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(
        firstPage.Appeals.Concat(secondPage.Appeals).Select(appeal => appeal.Id),
        viewModel.Items.Select(item => item.Id));
    Assert.Equal(handler.Requests[1].PathAndQuery, handler.Requests[2].PathAndQuery);
  }

  [Theory]
  [InlineData("/my/warnings", "/api/v1/my/warnings?limit=25", "Warnings")]
  public async Task LoadAsyncMapsPersonalModerationCaseRoutesToCountRows(
      string path,
      string expectedRequest,
      string expectedTitle)
  {
    var handler = new RecordingHandler("""{"results":[{"id":"case-1"},{"id":"case-2"}]}""");
    var viewModel = new ModerationViewModel(new ApiModerationService(Client(handler)));
    Assert.True(ModerationRoutes.TryResolve(path, out var context));

    viewModel.SetContext(context);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(expectedRequest, handler.Requests.Single().PathAndQuery);
    Assert.Equal(expectedTitle, viewModel.Title);
    Assert.Equal(expectedTitle, viewModel.Items.Single().Title);
    Assert.Equal("2 items", viewModel.Items.Single().Detail);
  }

  public static IEnumerable<object[]> RouteCases()
  {
    yield return Case(
        "/appeals",
        "native.moderation.appeals.default",
        "/api/v1/appeals?limit=25&status=pending",
        "case-1",
        "Pending · post post-1");
    yield return Case(
        "/my/appeals",
        "native.moderation.appeals.default",
        "/api/v1/appeals?limit=25&mine=true&status=pending",
        "case-1",
        "Pending · post post-1");
    yield return Case(
        "/disputes",
        "native.moderation.disputes.default",
        "/api/v1/disputes?limit=25&status=pending",
        "Native review dispute",
        "Pending · remove");
    yield return Case(
        "/admin/modlog",
        "native.moderation.modlog.default",
        "/api/v1/admin/modlog",
        "remove",
        "admin · Spam");
    yield return Case(
        "/admin/moderation-analytics",
        "native.moderation.analytics.default",
        "/api/v1/admin/moderation-analytics?range=30d",
        "Scope",
        "global");
  }

  private static object[] Case(
      string path,
      string fixtureId,
      string expectedRequest,
      string expectedTitle,
      string expectedDetail) =>
      [path, fixtureId, expectedRequest, expectedTitle, expectedDetail];

  private static (ModerationViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      string fixtureId)
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(fixtureId));
    return (new ModerationViewModel(new ApiModerationService(Client(handler))), handler);
  }

  private sealed class EnglishDeviceLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }

  private static VouchaApiClient Client(RecordingHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
}
