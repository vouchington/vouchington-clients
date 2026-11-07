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
  [Fact]
  public async Task MountedFormPreservesDraftRowsCursorAndSlotsAfterCodedCapAndCanRetry()
  {
    DispatcherProvider.SetCurrent(new ImmediateTimerDispatcherProvider(ImmediateTimerStart.StopAfterTick));
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var handler = new PromptHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = DispatchProxy.Create<ICommunitiesService, PromptProxy>();
    ((PromptProxy)service).Api = new ApiCommunitiesService(new VouchaApiClient(http));
    var session = new Session();
    using var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityAgentPromptsPage(model, session);
    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.AgentPrompts, TestContext.Current.CancellationToken);
    var slots = model.AgentPromptSlots;
    Find<Entry>(page, "community-agent-prompt-draft").Text = "My unsubmitted draft";
    Find<Button>(page, "community-agent-prompt-create").SendClicked();
    await WaitAsync(() => model.HasReachedAgentPromptLimit && !model.IsLoading);
    Assert.Equal("Prompt creation limit reached for this community", Find<Label>(page, "community-agent-prompt-creation-error").Text);
    Assert.Equal("My unsubmitted draft", Find<Entry>(page, "community-agent-prompt-draft").Text);
    Assert.Single(model.AgentPrompts);
    Assert.Equal(slots, model.AgentPromptSlots);
    Assert.True(model.HasMoreCommunityList);
    Assert.False(model.HasError);
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
    public List<string> Paths { get; } = [];
    public List<string> Drafts { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      if (request.Method == HttpMethod.Post)
      {
        Posts++;
        var input = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
        Drafts.Add(input["prompt"]!.GetValue<string>());
        var conflict = JsonNode.Parse(Fixture("native.chat.identity-conflict"))!;
        conflict["code"] = "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED";
        conflict["message"] = "Prompt creation limit reached for this community";
        return new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(conflict.ToJsonString()) };
      }
      var body = JsonNode.Parse(Fixture("web.communities.agent-prompts.default"))!;
      body["slot_info"] = JsonSerializer.SerializeToNode(new CommunityAgentPromptSlotInfo(9, 10, 1, new(3, 10)), VouchaApiJson.Options);
      var continuation = request.RequestUri.Query.Contains("after=", StringComparison.Ordinal);
      if (continuation) body["community_agent_prompts"]![0]!["id"] = "prompt-2";
      body["page_info"] = JsonSerializer.SerializeToNode(new CommunityAgentPromptPageInfo(continuation ? null : "opaque/+?", !continuation, null), VouchaApiJson.Options);
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString()) };
    }
    private static string Fixture(string id) => File.ReadAllText(FilamentsContractPaths.ApiFixture($"responses/{id}.json"));
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("moderator-1", "moderator", Roles: ["moderator"]));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
