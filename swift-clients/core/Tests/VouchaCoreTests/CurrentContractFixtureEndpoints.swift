import VouchaAPI
import VouchaModels

private let chatConversationId = "0198ffff-0000-7000-8000-000000000001"
private let chatUserMessageId = "0198ffff-0001-7000-8000-000000000001"
private let chatAssistantMessageId = "0198ffff-0001-7000-8000-000000000002"
private let chatPageTwoCursor = "eyJpZCI6IjAxOThmZmZmLTAwMDEtNzAwMC04MDAwLTAwMDAwMDAwMDAwMiJ9"

private let persistedChatEndpoint = Endpoint.clientGeneratedChat(
    conversationId: chatConversationId,
    message: "Hello",
    messageIds: (user: chatUserMessageId, assistant: chatAssistantMessageId),
    assistantContent: "Hi",
    model: (provider: "apple_foundation", name: nil)
)

let currentContractFixtureEndpoints: [String: Endpoint] = [
    "native.chat.completed": persistedChatEndpoint,
    "native.chat.duplicate": persistedChatEndpoint,
    "native.chat.retry": persistedChatEndpoint,
    "native.chat.unauthorized": persistedChatEndpoint,
    "native.chat.forbidden": persistedChatEndpoint,
    "native.chat.identity-conflict": persistedChatEndpoint,
    "native.chat.page-1": .myConversationMessages(conversationId: chatConversationId, limit: 1),
    "native.chat.page-2": .myConversationMessages(
        conversationId: chatConversationId, after: chatPageTwoCursor, limit: 1
    ),
    "native.chat.incomplete": .myConversationMessages(conversationId: chatConversationId, limit: 1),
    "native.chat.conversations": .myConversations(),
    "native.moderation.appeals.detail.default": .appeal(id: "00000000-0000-7000-8000-000000000102"),
    "native.moderation.copyright.image-similarity-candidates.default": .copyrightImageSimilarityCandidates(
        noticeId: "00000000-0000-7000-8000-000000000804",
        targetId: "00000000-0000-7000-8000-000000000805"
    ),
    "native.my.api-keys.create": .createMyApiKey(
        label: "Coding agent", type: .mcp, permissions: ["mcp.user:read", "mcp.user:write"]
    ),
    "native.my.api-keys.rotate": .rotateMyApiKey(id: "00000000-0000-7000-8000-000000000701"),
    "native.my.oauth-grants.paginated": .myOAuthGrants(
        after: "fixture-owner-scoped-oauth-grant-cursor", limit: 1
    ),
    "native.my.oauth-grants.revoke": .revokeMyOAuthGrant(id: "00000000-0000-7000-8000-000000000711"),
    "native.posts.images.placement.default": .postImages(idOrSlug: "00000000-0000-7000-8000-000000000801"),
    "shared.scopes.catalog": .scopeCatalog,
    "web.communities.automod-settings.update.default": .updateCommunityAutomodSettings(
        idOrSlug: "test-community", automodAction: .reviewQueue
    )
]
