using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public async Task SettingsContinuationsAppendUniqueRowsAndForwardEachCursor()
  {
    var service = new FakeSettingsService
    {
      InitialApiKeyPage = new("api-key-cursor", true, null),
      InitialSessionPage = new("session-cursor", true, null),
      InitialPushPage = new("push-cursor", true, null),
      ApiKeyContinuation = new([FakeSettingsService.CreateApiKey() with { Id = "api-key-2" }], TerminalPage),
      SessionContinuation = new([FakeSettingsService.CreateSession() with { Id = "session-2" }], TerminalPage),
      PushContinuation = new([FakeSettingsService.CreatePushSubscription() with { Id = "push-2" }], TerminalPage),
    };
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreApiKeysAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreSessionsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMorePushSubscriptionsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["api-key-1", "api-key-2"], viewModel.ApiKeys.Select(item => item.Id));
    Assert.Equal(["session-1", "session-2"], viewModel.Sessions.Select(item => item.Id));
    Assert.Equal(["push-1", "push-2"], viewModel.PushSubscriptions.Select(item => item.Id));
    Assert.Equal("api-key-cursor", service.ApiKeyAfter);
    Assert.Equal("session-cursor", service.SessionAfter);
    Assert.Equal("push-cursor", service.PushAfter);
  }

  [Fact]
  public async Task ApiKeyContinuationFailureIsIsolatedAndRetriesTheSameCursor()
  {
    var duplicate = FakeSettingsService.CreateApiKey();
    var service = new FakeSettingsService
    {
      InitialApiKeyPage = new("api-key-cursor", true, null),
    };
    service.ApiKeyPageResults.Enqueue(() => Task.FromException<ApiKeyListResponse>(
        new InvalidOperationException("api key page failed")));
    service.ApiKeyPageResults.Enqueue(() => Task.FromResult(new ApiKeyListResponse(
        [duplicate, duplicate with { Id = "api-key-2" }], TerminalPage)));
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreApiKeysAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["api-key-1"], viewModel.ApiKeys.Select(item => item.Id));
    Assert.True(viewModel.HasApiKeyPaginationError);
    Assert.False(viewModel.HasSessionPaginationError);
    Assert.False(viewModel.HasPushSubscriptionPaginationError);
    Assert.True(viewModel.HasMoreApiKeys);

    await viewModel.LoadMoreApiKeysAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["api-key-1", "api-key-2"], viewModel.ApiKeys.Select(item => item.Id));
    Assert.False(viewModel.HasApiKeyPaginationError);
    Assert.False(viewModel.HasMoreApiKeys);
    Assert.Equal(["api-key-cursor", "api-key-cursor"], service.ApiKeyAfters);
  }

  [Fact]
  public async Task CanceledSessionContinuationRetainsRowsAndCanRetry()
  {
    var service = new FakeSettingsService
    {
      InitialSessionPage = new("session-cursor", true, null),
    };
    service.SessionPageResults.Enqueue(() => Task.FromCanceled<AuthSessionListResponse>(
        new CancellationToken(true)));
    service.SessionPageResults.Enqueue(() => Task.FromResult(new AuthSessionListResponse(
        [FakeSettingsService.CreateSession() with { Id = "session-2" }], TerminalPage)));
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreSessionsAsync(new CancellationToken(true));

    Assert.Equal(["session-1"], viewModel.Sessions.Select(item => item.Id));
    Assert.False(viewModel.HasSessionPaginationError);
    Assert.True(viewModel.HasMoreSessions);

    await viewModel.LoadMoreSessionsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["session-1", "session-2"], viewModel.Sessions.Select(item => item.Id));
    Assert.Equal(["session-cursor", "session-cursor"], service.SessionAfters);
  }

  [Fact]
  public async Task UnexpectedSessionCancellationSurfacesAsRetryablePaginationFailure()
  {
    var service = new FakeSettingsService
    {
      InitialSessionPage = new("session-cursor", true, null),
    };
    service.SessionPageResults.Enqueue(() => Task.FromCanceled<AuthSessionListResponse>(
        new CancellationToken(true)));
    service.SessionPageResults.Enqueue(() => Task.FromResult(new AuthSessionListResponse(
        [FakeSettingsService.CreateSession() with { Id = "session-2" }], TerminalPage)));
    var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreSessionsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasSessionPaginationError);
    Assert.True(viewModel.HasMoreSessions);

    await viewModel.LoadMoreSessionsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["session-1", "session-2"], viewModel.Sessions.Select(item => item.Id));
    Assert.False(viewModel.HasSessionPaginationError);
    Assert.False(viewModel.HasMoreSessions);
    Assert.Equal(["session-cursor", "session-cursor"], service.SessionAfters);
  }

  private static PageInfo TerminalPage => new(null, false, null);

  private partial class FakeSettingsService
  {
    public ApiKeyListResponse? ApiKeyContinuation { get; init; }
    public AuthSessionListResponse? SessionContinuation { get; init; }
    public WebPushSubscriptionListResponse? PushContinuation { get; init; }
    public string? ApiKeyAfter { get; private set; }
    public string? SessionAfter { get; private set; }
    public string? PushAfter { get; private set; }
    public List<string?> ApiKeyAfters { get; } = [];
    public List<string?> SessionAfters { get; } = [];
    public Queue<Func<Task<ApiKeyListResponse>>> ApiKeyPageResults { get; } = [];
    public Queue<Func<Task<AuthSessionListResponse>>> SessionPageResults { get; } = [];
    public PageInfo InitialApiKeyPage { get; init; } = TerminalPage;
    public PageInfo InitialSessionPage { get; init; } = TerminalPage;
    public PageInfo InitialPushPage { get; init; } = TerminalPage;

    public Task<ApiKeyListResponse> FetchApiKeysPageAsync(
        string? after, int limit, CancellationToken cancellationToken = default)
    {
      ApiKeyAfter = after;
      ApiKeyAfters.Add(after);
      return ApiKeyPageResults.Count > 0
          ? ApiKeyPageResults.Dequeue()()
          : Task.FromResult(ApiKeyContinuation!);
    }

    public Task<AuthSessionListResponse> FetchAuthSessionsPageAsync(
        string? after, int limit, CancellationToken cancellationToken = default)
    {
      SessionAfter = after;
      SessionAfters.Add(after);
      return SessionPageResults.Count > 0
          ? SessionPageResults.Dequeue()()
          : Task.FromResult(SessionContinuation!);
    }

    public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsPageAsync(
        string? after, int limit, CancellationToken cancellationToken = default)
    {
      PushAfter = after;
      return Task.FromResult(PushContinuation!);
    }
  }
}
