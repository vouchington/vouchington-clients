using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityAgentPromptPageTests
{
  [Theory]
  [InlineData(false, false)]
  [InlineData(true, false)]
  [InlineData(false, true)]
  [InlineData(true, true)]
  public async Task MountedFormPreservesDraftRowsCursorAndSlotsAfterFailedCreateAndCanRetry(bool readCompletesFirst, bool genericFailure)
  {
    DispatcherProvider.SetCurrent(new ImmediateTimerDispatcherProvider(ImmediateTimerStart.StopAfterTick));
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var handler = new PromptHandler { GenericCreateFailure = genericFailure };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = DispatchProxy.Create<ICommunitiesService, PromptProxy>();
    ((PromptProxy)service).Api = new ApiCommunitiesService(new VouchaApiClient(http));
    var session = new Session();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityAgentPromptsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, TestContext.Current.CancellationToken);
    var slots = model.AgentPromptSlots;
    Find<Entry>(page, "community-agent-prompt-draft").Text = "My unsubmitted draft";
    var postGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var readGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    handler.WaitForPost = postGate.Task;
    Find<Button>(page, "community-agent-prompt-create").SendClicked();
    if (!readCompletesFirst) handler.WaitForRead = readGate.Task;
    var reload = model.LoadAgentPromptsAsync(TestContext.Current.CancellationToken);
    if (readCompletesFirst) await reload;
    postGate.SetResult();
    await WaitAsync(() => genericFailure ? model.HasError : model.HasReachedAgentPromptLimit);
    if (genericFailure) Assert.Equal("Create temporarily unavailable", model.ErrorMessage);
    else Assert.Equal("Prompt creation limit reached for this community", Find<Label>(page, "community-agent-prompt-creation-error").Text);
    Assert.Equal("My unsubmitted draft", Find<Entry>(page, "community-agent-prompt-draft").Text);
    Assert.Single(model.AgentPrompts);
    Assert.Equal(slots, model.AgentPromptSlots);
    Assert.True(model.HasMoreCommunityList);
    Assert.Equal(genericFailure, model.HasError);
    readGate.SetResult();
    await reload;
    await WaitAsync(() => !model.IsLoading);
    Assert.Equal("My unsubmitted draft", Find<Entry>(page, "community-agent-prompt-draft").Text);
    Find<Button>(page, "community-agent-prompt-create").SendClicked();
    await WaitAsync(() => handler.Posts == 2 && !model.IsLoading);
    Assert.Equal("My unsubmitted draft", Find<Entry>(page, "community-agent-prompt-draft").Text);
    Assert.All(handler.Drafts, draft => Assert.Equal("My unsubmitted draft", draft));
    var pagination = Find<HybridPaginationControl>(page, "pagination-community-agentprompts");
    Assert.True(pagination.HasMore);
    pagination.TryLoadAutomatically();
    await WaitAsync(() => model.AgentPrompts.Count == 2);
    Assert.Equal(["prompt-1", "prompt-2"], model.AgentPrompts.Select(prompt => prompt.Id));
    Assert.Equal(slots, model.AgentPromptSlots);
    Assert.Contains(handler.Paths, path => path.Contains("after=opaque%2F%2B%3F", StringComparison.Ordinal));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CommittedCreateClearsMountedDraftWhenRefreshFailsOrCancelsAndRetryDoesNotPost(bool canceled)
  {
    DispatcherProvider.SetCurrent(new ImmediateTimerDispatcherProvider(ImmediateTimerStart.StopAfterTick));
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var handler = new PromptHandler { CommitCreates = true, RejectLaterCreates = true, CancelRefresh = canceled };
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = DispatchProxy.Create<ICommunitiesService, PromptProxy>();
    ((PromptProxy)service).Api = new ApiCommunitiesService(new VouchaApiClient(http));
    var session = new Session();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityAgentPromptsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, TestContext.Current.CancellationToken);
    var slots = model.AgentPromptSlots;
    Find<Entry>(page, "community-agent-prompt-draft").Text = "Committed draft";
    Find<Button>(page, "community-agent-prompt-create").SendClicked();
    await WaitAsync(() => model.HasAgentPromptRefreshError && Find<Entry>(page, "community-agent-prompt-draft").Text == string.Empty);
    Assert.Equal(1, handler.Posts);
    Assert.Equal("prompt-1", Assert.Single(model.AgentPrompts).Id);
    Assert.Equal(slots, model.AgentPromptSlots);
    Assert.True(model.HasMoreCommunityList);
    Assert.False(model.HasError);
    Assert.Null(model.AgentPromptCreationError);
    Assert.False(string.IsNullOrWhiteSpace(Find<Label>(page, "community-agent-prompt-refresh-error").Text));
    Find<Entry>(page, "community-agent-prompt-draft").Text = "Second draft";
    Find<Button>(page, "community-agent-prompt-create").SendClicked();
    await WaitAsync(() => model.HasReachedAgentPromptLimit && !model.IsLoading);
    Assert.True(model.HasAgentPromptRefreshError);
    Assert.Equal("Second draft", Find<Entry>(page, "community-agent-prompt-draft").Text);
    Assert.Equal("prompt-1", Assert.Single(model.AgentPrompts).Id);
    Assert.Equal(slots, model.AgentPromptSlots);
    Assert.True(model.HasMoreCommunityList);
    var requests = handler.Paths.Count;
    var retry = Find<Button>(page, "community-agent-prompt-refresh-retry");
    Assert.True(retry.IsVisible);
    retry.SendClicked();
    await WaitAsync(() => !model.HasAgentPromptRefreshError && model.AgentPrompts.Any(prompt => prompt.Id == "prompt-created"));
    Assert.Equal(2, handler.Posts);
    Assert.Equal(requests + 1, handler.Paths.Count);
    Assert.Equal("Second draft", Find<Entry>(page, "community-agent-prompt-draft").Text);
    Assert.False(Find<Button>(page, "community-agent-prompt-refresh-retry").IsVisible);
  }

  private static T Find<T>(Element page, string id) where T : Element =>
      Assert.Single(VisualTreeElements.Descendants<T>(page), item => item.AutomationId == id);

  private static async Task WaitAsync(Func<bool> done)
  {
    for (var attempt = 0; attempt < 100 && !done(); attempt++) await Task.Delay(10);
    Assert.True(done());
  }

  public class PromptProxy : CommunityModerationPageAutomodNavigationTests.CommunityServiceProxy
  {
    public ApiCommunitiesService Api { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
      nameof(ICommunitiesService.FetchAgentPromptsPageAsync) => Api.FetchAgentPromptsPageAsync((string)args![0]!, (string?)args[1], (int)args[2]!, (CancellationToken)args[3]!),
      nameof(ICommunitiesService.CreateAgentPromptAsync) => Api.CreateAgentPromptAsync((string)args![0]!, (CommunityAgentPromptUpsertRequest)args[1]!, (CancellationToken)args[2]!),
      _ => base.Invoke(method, args),
    };
  }

  private sealed class PromptHandler : HttpMessageHandler
  {
    public int Posts { get; private set; }
    public Task? WaitForPost { get; set; }
    public Task? WaitForRead { get; set; }
    public bool CommitCreates { get; init; }
    public bool RejectLaterCreates { get; init; }
    public bool GenericCreateFailure { get; init; }
    public bool CancelRefresh { get; init; }
    private bool refreshFailed;
    public List<string> Paths { get; } = [];
    public List<string> Drafts { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      if (request.Method == HttpMethod.Post)
      {
        Posts++;
        if (WaitForPost is { } post) await post;
        var input = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
        Drafts.Add(input["prompt"]!.GetValue<string>());
        if (CommitCreates && (!RejectLaterCreates || Posts == 1))
        {
          var created = JsonNode.Parse(Fixture("web.communities.agent-prompts.default"))!["community_agent_prompts"]![0]!.DeepClone();
          created["id"] = "prompt-created";
          created["prompt"] = input["prompt"]!.GetValue<string>();
          return new HttpResponseMessage(HttpStatusCode.OK)
          {
            Content = new StringContent(new JsonObject { ["community_agent_prompt"] = created }.ToJsonString()),
          };
        }
        if (GenericCreateFailure) throw new HttpRequestException("Create temporarily unavailable");
        var conflict = JsonNode.Parse(Fixture("native.chat.identity-conflict"))!;
        conflict["code"] = "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED";
        conflict["message"] = "Prompt creation limit reached for this community";
        return new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(conflict.ToJsonString()) };
      }
      if (CommitCreates && Posts > 0 && !refreshFailed)
      {
        refreshFailed = true;
        if (CancelRefresh) throw new OperationCanceledException(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
          Content = new StringContent("{\"message\":\"Refresh temporarily unavailable\"}"),
        };
      }
      if (WaitForRead is { } read) await read;
      var body = JsonNode.Parse(Fixture("web.communities.agent-prompts.default"))!;
      if (CommitCreates && Posts > 0) body["community_agent_prompts"]![0]!["id"] = "prompt-created";
      body["slot_info"] = JsonSerializer.SerializeToNode(new CommunityAgentPromptSlotInfo(9, 10, 1, new(3, 10)), VouchaApiJson.Options);
      var continuation = request.RequestUri.Query.Contains("after=", StringComparison.Ordinal);
      if (continuation) body["community_agent_prompts"]![0]!["id"] = "prompt-2";
      body["page_info"] = JsonSerializer.SerializeToNode(new CommunityAgentPromptPageInfo(continuation ? null : "opaque/+?", !continuation, null), VouchaApiJson.Options);
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString()) };
    }
    private static string Fixture(string id) => File.ReadAllText(ContractPaths.ApiFixture($"responses/{id}.json"));
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("moderator-1", "moderator", Roles: ["moderator"]));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
