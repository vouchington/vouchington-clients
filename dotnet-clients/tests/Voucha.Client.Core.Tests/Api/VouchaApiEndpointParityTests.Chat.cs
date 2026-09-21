using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  [Theory]
  [MemberData(nameof(ChatSupportEndpointCases))]
  public void ChatSupportEndpointsMirrorSwiftRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  public static IEnumerable<object[]> ChatSupportEndpointCases()
  {
    yield return Case("myChatConversations", VouchaApiEndpoints.MyChatConversations("cursor-8", 19), HttpMethod.Get, "/api/v1/my/conversations", Query(("limit", "19"), ("after", "cursor-8")));
    yield return Case("createChatConversation", VouchaApiEndpoints.CreateChatConversation(new CreateChatConversationBody("Title")), HttpMethod.Post, "/api/v1/conversations", Query(), hasBody: true);
    yield return Case("myChatConversationMessages", VouchaApiEndpoints.MyChatConversationMessages("conversation-1"), HttpMethod.Get, "/api/v1/my/conversations/conversation-1/messages", Query());
    yield return Case("updateChatConversationTitle", VouchaApiEndpoints.UpdateChatConversationTitle("conversation-1", new UpdateChatConversationTitleBody("Updated")), HttpMethod.Patch, "/api/v1/my/conversations/conversation-1", Query(), hasBody: true);
    yield return Case("generateChatConversationTitle", VouchaApiEndpoints.GenerateChatConversationTitle("conversation-1"), HttpMethod.Post, "/api/v1/my/conversations/conversation-1/title", Query());
    yield return Case("deleteChatConversation", VouchaApiEndpoints.DeleteChatConversation("conversation-1"), HttpMethod.Delete, "/api/v1/my/conversations/conversation-1", Query());
    yield return Case("streamChatConversationMessage", VouchaApiEndpoints.StreamChatConversationMessage("conversation-1", "Hello"), HttpMethod.Post, "/api/v1/conversations/conversation-1/chat", Query(), hasBody: true);
    yield return Case(
      "createClientGeneratedChat",
      VouchaApiEndpoints.CreateClientGeneratedChat(
        "conversation-1",
        new CreateClientGeneratedChatBody("Hello", "Hi", "windows_foundry", "phi-silica")),
      HttpMethod.Post,
      "/api/v1/conversations/conversation-1/client-generated-chat",
      Query(),
      hasBody: true);
  }
}
