using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task CanonicalFeedDeliveryProducesFactOnlyAfterBridgeValidation()
  {
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    var row = fixtures.RootElement.GetProperty("cases").EnumerateArray()
        .Single(item => item.GetProperty("id").GetString() == "native.mcp.read-feed.posts");
    var factory = new FakeFactory { Catalog = ProducerCatalog() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    factory.Connections[0].ToolResult = new McpToolResult(
        JsonSerializer.SerializeToElement(new[] { new { type = "text", text = "external result" } }),
        row.GetProperty("structuredContent"), false);
    var bridge = new MemberMcpToolBridge(snapshot.Catalog);
    var validated = await bridge.ExecuteValidatedResultAsync("read_feed",
        row.GetProperty("arguments").GetRawText(), session, snapshot, "call-1",
        cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("account-a", validated.FeedPostFact?.AccountId);
    Assert.Equal("call-1", validated.FeedPostFact?.CallId);
    Assert.Equal(["0198ffff-0000-7000-8000-000000000002"], validated.FeedPostFact?.PostIds);
    using var envelope = JsonDocument.Parse(validated.Envelope);
    Assert.Equal(row.GetProperty("structuredContent").GetRawText(),
        envelope.RootElement.GetProperty("structuredContent").GetRawText());
    Assert.Equal("external result", envelope.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
    Assert.Equal(1, factory.Connections[0].ToolCalls);
  }

  [Fact]
  public async Task InvalidMutationOutputStopsModelLoopWithoutRedispatch()
  {
    var catalog = ProducerCatalog();
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    var fixture = fixtures.RootElement.GetProperty("cases").EnumerateArray()
        .Single(row => row.GetProperty("id").GetString() == "native.mcp.manage-bookmark.set");
    var rawArguments = fixture.GetProperty("arguments").GetRawText();
    var factory = new FakeFactory { Catalog = catalog };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    factory.Connections[0].ToolResult = MemberMcpOutputContractTests.Result(
        JsonSerializer.SerializeToElement(new { success = true }));
    var bridge = new MemberMcpToolBridge(snapshot.Catalog);
    using var handler = new OneMutationModelHandler(rawArguments);
    using var client = new OpenAICompatibleResponsesClient(handler);
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Test", true,
        "http://127.0.0.1:11434/v1", ["model"], "model");

    await Assert.ThrowsAsync<PossibleMcpToolSideEffectException>(() =>
        client.GenerateAssistantContentWithToolsAsync("Bookmark this post", [], profile, null,
            bridge.Tools, () => true,
            (name, arguments, _) =>
            {
              bridge.Validate(name, arguments);
              return Task.CompletedTask;
            },
            (name, arguments, token, admitted) => bridge.ExecuteAsync(
                name, arguments, session, snapshot, admitted, token), TestContext.Current.CancellationToken));

    Assert.Equal(1, factory.Connections[0].ToolCalls);
    Assert.Equal(1, handler.Calls);
  }

  [Fact]
  public async Task RealSelectedResultsPreserveCompleteEnvelopeAndInputDescriptors()
  {
    var factory = new FakeFactory { Catalog = ProducerCatalog() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    var bridge = new MemberMcpToolBridge(snapshot.Catalog);
    foreach (var descriptor in bridge.Tools)
    {
      var tool = snapshot.Catalog.GetProperty("tools").EnumerateArray()
          .Single(item => item.GetProperty("name").GetString() == descriptor.Name);
      Assert.Equal(tool.GetProperty("inputSchema").GetRawText(), descriptor.Parameters.GetRawText());
    }
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    var cases = fixtures.RootElement.GetProperty("cases").EnumerateArray().ToArray();
    foreach (var row in cases)
    {
      factory.Connections[0].ToolResult = new McpToolResult(JsonSerializer.SerializeToElement(new[]
      {
        new { type = "text", text = "<external-content>member facts</external-content>" },
      }), row.GetProperty("structuredContent"), false);
      var envelope = await bridge.ExecuteAsync(row.GetProperty("tool").GetString()!,
          row.GetProperty("arguments").GetRawText(), session, snapshot, TestContext.Current.CancellationToken);
      using var actual = JsonDocument.Parse(envelope);
      Assert.Equal(row.GetProperty("structuredContent").GetRawText(), actual.RootElement.GetProperty("structuredContent").GetRawText());
      Assert.Equal("<external-content>member facts</external-content>", actual.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
      Assert.False(actual.RootElement.GetProperty("isError").GetBoolean());
    }
    Assert.Equal(cases.Length, factory.Connections[0].ToolCalls);
  }

  private static JsonElement ProducerCatalog()
  {
    using var manifest = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp.json")));
    var tools = manifest.RootElement.GetProperty("servers").EnumerateArray()
        .Single(server => server.GetProperty("name").GetString() == "voucha-user-mcp")
        .GetProperty("tools").EnumerateArray().Select(row => row.GetProperty("tool")).ToArray();
    return JsonSerializer.SerializeToElement(new { tools });
  }

  private sealed class OneMutationModelHandler(string arguments) : HttpMessageHandler
  {
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Calls++;
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
          output = new[] { new { type = "function_call", call_id = "mutation-1", name = "manage_bookmark", arguments } },
        }), Encoding.UTF8, "application/json"),
      });
    }
  }
}
