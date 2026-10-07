using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static class CurrentFixtureRequests
{
  private const string ConversationId = "0198ffff-0000-7000-8000-000000000001";
  private const string ApiKeyId = "00000000-0000-7000-8000-000000000701";
  private const string ChatPageCursor = "eyJpZCI6IjAxOThmZmZmLTAwMDEtNzAwMC04MDAwLTAwMDAwMDAwMDAwMiJ9";

  public static Dictionary<string, ApiRequest> WithCurrentFixtureRequests(
      this IReadOnlyDictionary<string, ApiRequest> existing)
  {
    var registry = new Dictionary<string, ApiRequest>(existing, StringComparer.Ordinal);
    var chatBody = new CreateClientGeneratedChatBody(
        "Hello", "Hi", "apple_foundation",
        "0198ffff-0001-7000-8000-000000000001",
        "0198ffff-0001-7000-8000-000000000002");
    foreach (var id in new[]
    {
      "native.chat.completed", "native.chat.duplicate", "native.chat.retry",
      "native.chat.unauthorized", "native.chat.forbidden", "native.chat.identity-conflict",
    })
    {
      registry.Add(id, VouchaApiEndpoints.CreateClientGeneratedChat(ConversationId, chatBody));
    }

    registry.Add("native.chat.page-1", VouchaApiEndpoints.MyChatConversationMessages(ConversationId, limit: 1));
    registry.Add("native.chat.page-2", VouchaApiEndpoints.MyChatConversationMessages(ConversationId, ChatPageCursor, 1));
    registry.Add("native.chat.incomplete", VouchaApiEndpoints.MyChatConversationMessages(ConversationId, limit: 1));
    registry.Add("native.chat.conversations", VouchaApiEndpoints.MyChatConversations());
    registry.Add("native.my.api-keys.rotate", VouchaApiEndpoints.RotateApiKey(ApiKeyId));
    registry.Add("web.communities.automod-settings.update.default", VouchaApiEndpoints.UpdateCommunityAutomodSettings(
        "test-community", new UpdateCommunityAutomodSettingsRequest("review_queue")));
    registry.Add("native.moderation.appeals.detail.default", VouchaApiEndpoints.Appeal("00000000-0000-7000-8000-000000000102"));
    return registry;
  }

  public static Dictionary<string, Type> WithCurrentFixtureTypes(this Dictionary<string, Type> registry)
  {
    foreach (var id in new[] { "native.chat.completed", "native.chat.duplicate", "native.chat.retry" })
    {
      registry.Add(id, typeof(ClientGeneratedChatResponse));
    }

    foreach (var id in new[] { "native.chat.unauthorized", "native.chat.forbidden", "native.chat.identity-conflict" })
    {
      registry.Add(id, typeof(Dictionary<string, string>));
    }

    foreach (var id in new[] { "native.chat.page-1", "native.chat.page-2", "native.chat.incomplete" })
    {
      registry.Add(id, typeof(ChatMessagesResponse));
    }

    registry.Add("native.chat.conversations", typeof(ChatConversationListResponse));
    registry.Add("native.my.api-keys.rotate", typeof(ApiKeyCreationResponse));
    registry.Add("web.communities.automod-settings.update.default", typeof(CommunityResponse));
    registry.Add("native.moderation.appeals.detail.default", typeof(ModerationAppealResponse));
    return registry;
  }
}
