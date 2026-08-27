using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class AgentConversationViewModelTests
{
  [Fact]
  public void PaginationActionTitleRefreshesWithLocale()
  {
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    using var viewModel = new AgentConversationViewModel(
        new StubAgentConversationsService(),
        new UiLocalization(controller),
        controller);
    var changes = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(AgentConversationViewModel.PaginationActionTitle)) changes++;
    };

    Assert.Equal("Load older messages", viewModel.PaginationActionTitle);

    controller.ApplySavedLocale("es");

    Assert.Equal("Cargar mensajes más antiguos", viewModel.PaginationActionTitle);
    Assert.Equal(1, changes);
  }

  [Theory]
  [InlineData("Reply text", "Request failed", "Reply text")]
  [InlineData("  ", "Request failed", "Request failed")]
  [InlineData(null, null, "No results")]
  public void TranscriptRowsPreferNonblankContentThenErrorThenLocalizedEmpty(
      string? content, string? error, string expected)
  {
    Assert.Equal(expected, AgentTranscriptRow.From(Message("message-1", content, error), "selected-agent", UiLocalization.English).Body);
  }

  [Fact]
  public async Task TranscriptRowsClassifySelectedAgentByAuthorId()
  {
    var service = new StubAgentConversationsService(
        _ => Task.FromResult(Response([
          Message("selected", "Selected agent", createdById: "selected-agent", role: "user"),
          Message("support", "Support user", createdById: "support-one", role: "assistant"),
          Message("other-agent", "Other agent", createdById: "other-agent", role: "assistant"),
        ], false, null)));
    var viewModel = new AgentConversationViewModel(service);

    await viewModel.LoadAsync("helper", "conversation-1", TestContext.Current.CancellationToken);

    var rowsById = viewModel.MessageRows.ToDictionary(row => row.Id);
    Assert.Equal("Agent", rowsById["selected"].Role);
    Assert.Equal("User", rowsById["support"].Role);
    Assert.Equal("User", rowsById["other-agent"].Role);
  }

  [Fact]
  public void TranscriptRowUsesDeletedAuthorFallback()
  {
    var localization = UiLocalization.English;

    var row = AgentTranscriptRow.From(Message("deleted-author", "body", createdById: null), "selected-agent", localization);

    Assert.Equal(localization.Localize(UiMessageKey.NativeSwiftPresentationValuesDeleted), row.Role);
  }

  [Fact]
  public async Task UntitledConversationUsesLocalizedFallback()
  {
    var service = new StubAgentConversationsService(
        _ => Task.FromResult(Response([], false, null, "  ")));
    using var controller = new UiLocaleController(new StubLanguageProvider("es"));
    using var viewModel = new AgentConversationViewModel(
        service,
        new UiLocalization(controller),
        controller);

    await viewModel.LoadAsync("helper", "conversation-1", TestContext.Current.CancellationToken);

    Assert.Equal("Conversación del agente", viewModel.ConversationDisplayTitle);
  }

  [Fact]
  public async Task ConversationCreatedAtRefreshesWithLocale()
  {
    var createdAt = new DateTimeOffset(2026, 8, 1, 15, 30, 0, TimeSpan.Zero);
    var service = new StubAgentConversationsService(
        _ => Task.FromResult(Response([], false, null, createdAt: createdAt)));
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var viewModel = new AgentConversationViewModel(service, localization, controller);

    await viewModel.LoadAsync("helper", "conversation-1", TestContext.Current.CancellationToken);
    Assert.Equal(localization.FormatDateTime(createdAt, TimeZoneInfo.Local), viewModel.ConversationCreatedAt);

    var refreshedProperties = new List<string?>();
    viewModel.PropertyChanged += (_, eventArgs) => refreshedProperties.Add(eventArgs.PropertyName);
    controller.ApplySavedLocale("es");

    Assert.Contains(nameof(AgentConversationViewModel.ConversationCreatedAt), refreshedProperties);
    Assert.Equal(localization.FormatDateTime(createdAt, TimeZoneInfo.Local), viewModel.ConversationCreatedAt);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InitialLoadEndpointFailuresUseLocalizedError(bool failsAgentEndpoint)
  {
    var service = new StubAgentConversationsService(
        _ => failsAgentEndpoint
            ? Task.FromResult(Response([], false, null))
            : Task.FromException<AgentConversationDetailResponse>(new HttpRequestException("conversation unavailable")))
    {
      Detail = failsAgentEndpoint
          ? Task.FromException<AgentDetailResponse>(new HttpRequestException("agent unavailable"))
          : Task.FromResult(new AgentDetailResponse(
              new AgentSummary("selected", "selected-agent", "helper", null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null),
              null)),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var viewModel = new AgentConversationViewModel(service, localization, controller);

    await viewModel.LoadAsync("helper", "conversation-1", TestContext.Current.CancellationToken);

    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetCsharpError), viewModel.ErrorMessage);
    controller.ApplySavedLocale("pt");
    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetCsharpError), viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ContinuationPrependsUniqueRowsAndPreservesCurrentCopies()
  {
    var service = new StubAgentConversationsService(
        _ => Task.FromResult(Response([Message("message-1", "current")], true, "older")),
        _ => Task.FromResult(Response([
          Message("message-0", "earlier"),
          Message("message-0", "duplicate"),
          Message("message-1", "stale overlap"),
        ], false, null)));
    var viewModel = new AgentConversationViewModel(service);

    await viewModel.LoadAsync("helper", "conversation-1", TestContext.Current.CancellationToken);
    var first = viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);
    var duplicate = viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);
    await Task.WhenAll(first, duplicate);

    Assert.Equal(["message-0", "message-1"], viewModel.Messages.Select(message => message.Id));
    Assert.Equal("current", viewModel.Messages[1].Content?.Content);
    Assert.False(viewModel.HasMore);
    Assert.Equal(2, service.Requests.Count);
    Assert.Equal("older", service.Requests[1].After);
  }

  [Fact]
  public async Task ContinuationFailurePreservesRowsAndCanRetry()
  {
    var service = new StubAgentConversationsService(
        _ => Task.FromResult(Response([Message("message-1", "current")], true, "older")),
        _ => Task.FromException<AgentConversationDetailResponse>(new HttpRequestException("offline")),
        _ => Task.FromResult(Response([Message("message-0", "earlier")], false, null)));
    var viewModel = new AgentConversationViewModel(service);

    await viewModel.LoadAsync("helper", "conversation-1", TestContext.Current.CancellationToken);
    await viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["message-1"], viewModel.Messages.Select(message => message.Id));
    Assert.Equal("Error", viewModel.PaginationErrorMessage);
    Assert.Equal("Retry", viewModel.PaginationActionTitle);
    Assert.True(viewModel.CanLoadOlder);

    await viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["message-0", "message-1"], viewModel.Messages.Select(message => message.Id));
    Assert.Null(viewModel.PaginationErrorMessage);
  }

  [Fact]
  public async Task ReplacementInitialLoadInvalidatesAnOlderContinuationBeforeAwait()
  {
    var continuation = new TaskCompletionSource<AgentConversationDetailResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var replacement = new TaskCompletionSource<AgentConversationDetailResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubAgentConversationsService(
        _ => Task.FromResult(Response([Message("message-1", "current")], true, "older")),
        _ => continuation.Task,
        _ => replacement.Task);
    var viewModel = new AgentConversationViewModel(service);
    await viewModel.LoadAsync("helper", "conversation-1", TestContext.Current.CancellationToken);

    var oldPage = viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);
    var replacementLoad = viewModel.LoadAsync(
        "replacement-agent",
        "replacement-conversation",
        TestContext.Current.CancellationToken);
    continuation.SetResult(Response([Message("message-stale", "stale")], false, null));
    replacement.SetResult(Response([Message("message-replacement", "replacement")], false, null));
    await Task.WhenAll(oldPage, replacementLoad);

    Assert.Equal(["message-replacement"], viewModel.Messages.Select(message => message.Id));
    Assert.Equal("replacement-agent", service.Requests[2].AgentIdOrSlug);
    Assert.False(viewModel.IsLoadingOlder);
  }

  private static AgentConversationDetailResponse Response(
      IReadOnlyList<AgentConversationMessage> messages,
      bool hasMore,
      string? cursor,
      string title = "Agent conversation",
      DateTimeOffset? createdAt = null) =>
      new(
          new AgentConversation(
              "conversation-1", "agent", title, createdAt ?? DateTimeOffset.UnixEpoch,
              "user-1", DateTimeOffset.UnixEpoch, null, null, null, messages.LastOrDefault()?.Id),
          messages,
          new PageInfo(cursor, hasMore, messages.LastOrDefault()?.Id));

  private static AgentConversationMessage Message(
      string id,
      string? text,
      string? error = null,
      string? createdById = "selected-agent",
      string role = "assistant") =>
      new(
          id, "conversation-1", DateTimeOffset.UnixEpoch, createdById, DateTimeOffset.UnixEpoch,
          null, null, null, new AgentConversationMessageContent(role, text, error));

  private sealed class StubAgentConversationsService(
      params Func<FetchAgentConversationRequest, Task<AgentConversationDetailResponse>>[] handlers)
      : IAgentConversationsService
  {
    private readonly Queue<Func<FetchAgentConversationRequest, Task<AgentConversationDetailResponse>>> pending =
        new(handlers);

    public List<FetchAgentConversationRequest> Requests { get; } = [];
    public Task<AgentDetailResponse> Detail { get; init; } = Task.FromResult(new AgentDetailResponse(
        new AgentSummary("selected", "selected-agent", "helper", null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null),
        null));

    public Task<AgentsResponse> FetchAgentsAsync(
        FetchAgentsRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AgentConversationsResponse> FetchAgentConversationsAsync(
        FetchAgentConversationsRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AgentDetailResponse> FetchAgentAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default) => Detail;

    public Task<AgentConversationDetailResponse> FetchAgentConversationAsync(
        FetchAgentConversationRequest request,
        CancellationToken cancellationToken = default)
    {
      Requests.Add(request);
      return pending.Dequeue()(request);
    }
  }

  private sealed class StubLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
