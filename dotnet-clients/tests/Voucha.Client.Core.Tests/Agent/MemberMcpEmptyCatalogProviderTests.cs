using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task ConnectedEmptyCatalogUsesOrdinaryTextRequest()
  {
    var profile = new LocalLLMEndpointProfile(Guid.NewGuid(), "Local", true,
        "http://127.0.0.1:11434/v1", ["model"], "model");
    var settings = new InMemoryLocalLLMConfigurationStore(new(
        [profile], profile.Id, LocalChatProviderIds.OpenAICompatible(profile.Id)));
    using var handler = new RecordingTextHandler();
    using var responses = new OpenAICompatibleResponsesClient(handler);
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), new FakeFactory());
    var provider = new OpenAICompatibleLocalChatProvider(settings, new InMemoryLocalLLMSecretStore(), responses,
        new(new Dictionary<string, string?> { ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED"] = "true" }),
        profile.Id, mcpSession: session);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());

    var result = await provider.GenerateAssistantContentAsync(
        "Hello", [], snapshot, TestContext.Current.CancellationToken);

    Assert.Equal("text only", result.AssistantContent);
    Assert.Single(handler.Bodies);
    using var body = JsonDocument.Parse(handler.Bodies[0]);
    Assert.False(body.RootElement.TryGetProperty("tools", out _));
    Assert.False(body.RootElement.TryGetProperty("parallel_tool_calls", out _));
    Assert.Equal("model", body.RootElement.GetProperty("model").GetString());
  }

  private sealed class RecordingTextHandler : HttpMessageHandler
  {
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
      return new(HttpStatusCode.OK) { Content = new StringContent("""{"output_text":"text only"}""") };
    }
  }
}
