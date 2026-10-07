using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ChatTranscriptContractTests
{
  [Theory]
  [InlineData("native.chat.completed")]
  [InlineData("native.chat.duplicate")]
  [InlineData("native.chat.retry")]
  public void ClientGeneratedTranscriptPreservesTurnAndCompletion(string fixtureId)
  {
    var response = JsonSerializer.Deserialize<ClientGeneratedChatResponse>(
        ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)!;

    Assert.Equal("0198ffff-0001-7000-8000-000000000001", response.Turn.UserMessageId);
    Assert.Equal("0198ffff-0001-7000-8000-000000000002", response.Turn.AssistantMessageId);
    Assert.Equal(response.UserMessage.Id, response.Turn.UserMessageId);
    Assert.Equal(response.AssistantMessage.Id, response.Turn.AssistantMessageId);
    Assert.Equal("completed", response.UserMessage.Completion!.Status);
    Assert.Equal("completed", response.AssistantMessage.Completion!.Status);
    Assert.Null(response.AssistantMessage.CreatedById);

    using var encoded = JsonDocument.Parse(JsonSerializer.Serialize(response, VouchaApiJson.Options));
    Assert.False(encoded.RootElement.TryGetProperty("agentic_run", out _));
    Assert.Equal(response.Turn.AssistantMessageId,
        encoded.RootElement.GetProperty("turn").GetProperty("assistant_message_id").GetString());
  }

  [Fact]
  public void IncompleteTranscriptPreservesStatusAndNullContent()
  {
    var response = JsonSerializer.Deserialize<ChatMessagesResponse>(
        ApiFixtureLoader.LoadResponse("native.chat.incomplete"), VouchaApiJson.Options)!;
    var message = Assert.Single(response.Results);

    Assert.Equal("incomplete", message.Completion!.Status);
    Assert.Equal("assistant", message.Content.Role);
    Assert.Null(message.Content.Content);
  }
}
