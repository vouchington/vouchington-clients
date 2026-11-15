using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  /// <summary>One canonical mcp-results.json case and the Responses turn a model would send for it.</summary>
  private sealed class HttpFactCase
  {
    private readonly JsonElement row;

    private HttpFactCase(JsonElement row) => this.row = row;

    public string Tool => row.GetProperty("tool").GetString()!;
    public JsonElement Arguments => row.GetProperty("arguments");
    public JsonElement Structured => row.GetProperty("structuredContent");

    public static HttpFactCase Load(string id)
    {
      var fixtures = JsonDocument.Parse(File.ReadAllText(ContractPaths.ApiFixture("mcp-results.json")));
      return new(fixtures.RootElement.GetProperty("cases").EnumerateArray()
          .Single(item => item.GetProperty("id").GetString() == id).Clone());
    }

    public string Call(string callId) => JsonSerializer.Serialize(new
    {
      output = new[]
      {
        new { type = "function_call", call_id = callId, name = Tool, arguments = Arguments.GetRawText() },
      },
    });

    private static readonly string[] LegalFactTools =
        ["search_copyright_notices_for_association", "list_repeat_infringer_accounts"];

    /// <summary>
    /// The member server's tools plus the legal server's copyright tools. Native chat connects only to the
    /// member server today, so the legal tools exercise the shared bridge through the catalog a legal
    /// connection would supply.
    /// </summary>
    public static JsonElement PublishedCatalog()
    {
      using var manifest = JsonDocument.Parse(File.ReadAllText(ContractPaths.ApiFixture("mcp.json")));
      var servers = manifest.RootElement.GetProperty("servers").EnumerateArray().ToArray();
      var tools = servers.Single(item => item.GetProperty("name").GetString() == "voucha-user-mcp")
          .GetProperty("tools").EnumerateArray().Select(item => item.GetProperty("tool"))
          .Concat(servers.Single(item => item.GetProperty("name").GetString() == "voucha-legal-mcp")
              .GetProperty("tools").EnumerateArray().Select(item => item.GetProperty("tool"))
              .Where(tool => LegalFactTools.Contains(tool.GetProperty("name").GetString())));
      return JsonDocument.Parse(JsonSerializer.Serialize(new { tools })).RootElement.Clone();
    }
  }

  private sealed class HttpFactHarness : IDisposable
  {
    public required FakeSessionStore Accounts { get; init; }
    public required FakeFactory Factory { get; init; }
    public required MemberMcpSession Session { get; init; }
    public required ScriptedResponsesHandler Handler { get; init; }
    public required OpenAICompatibleLocalChatProvider Provider { get; init; }
    public int ToolCalls => Factory.Connections.Single().ToolCalls;

    public static async Task<HttpFactHarness> CreateAsync(
        HttpFactCase fixture, ScriptedResponsesHandler handler, bool isError = false,
        JsonElement? structured = null, bool holdToolCall = false)
    {
      var accounts = new FakeSessionStore("account-a");
      var factory = new FakeFactory { Catalog = HttpFactCase.PublishedCatalog(), HoldToolCall = holdToolCall };
      var session = new MemberMcpSession(accounts, new FakeStore(), factory);
      await session.ConnectAsync(TestContext.Current.CancellationToken);
      using var content = JsonDocument.Parse("[]");
      factory.Connections.Single().ToolResult = new McpToolResult(
          content.RootElement.Clone(), (structured ?? fixture.Structured).Clone(), isError);
      var profile = new LocalLLMEndpointProfile(
          Guid.NewGuid(), "Local", true, "http://127.0.0.1:11434/v1", ["model"], "model");
      var provider = new OpenAICompatibleLocalChatProvider(
          new InMemoryLocalLLMConfigurationStore(new([profile], profile.Id,
              LocalChatProviderIds.OpenAICompatible(profile.Id))),
          new InMemoryLocalLLMSecretStore(), new OpenAICompatibleResponsesClient(handler),
          new(new Dictionary<string, string?> { ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED"] = "true" }),
          profile.Id, mcpSession: session);
      return new() { Accounts = accounts, Factory = factory, Session = session, Handler = handler, Provider = provider };
    }

    public Task<LocalChatGenerationResult> GenerateAsync(CancellationToken cancellationToken = default) =>
        Provider.GenerateAssistantContentAsync("Read it", [],
            Session.ConnectedSnapshot(), cancellationToken);

    public void Dispose()
    {
      Session.Dispose();
      Handler.Dispose();
    }
  }

  private sealed class ScriptedResponsesHandler(params string[] replies) : HttpMessageHandler
  {
    public List<string> Bodies { get; } = [];
    public Action<int>? OnRequest { get; init; }

    /// <summary>The exact function_call_output envelope the model received for a call.</summary>
    public JsonDocument ContinuationEnvelope(string callId)
    {
      using var body = JsonDocument.Parse(Bodies[1]);
      var output = body.RootElement.GetProperty("input").EnumerateArray().Last();
      Assert.Equal(callId, output.GetProperty("call_id").GetString());
      return JsonDocument.Parse(output.GetProperty("output").GetString()!);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
      OnRequest?.Invoke(Bodies.Count);
      return new(HttpStatusCode.OK) { Content = new StringContent(replies[Bodies.Count - 1]) };
    }
  }

  private const string FinalAnswer = """{"output":[],"output_text":"Done"}""";
  private const string FailedContinuation = """{"status":"failed","output":[]}""";
}
