import VouchaAPI
import VouchaModels

extension EndpointManifestCoverage {
    private static let conversationId = "00000000-0000-7000-8000-000000000101"
    private static let participantUserId = "00000000-0000-7000-8000-000000000002"
    private static let addedUserId = "00000000-0000-7000-8000-000000000003"
    private static let conversationsAfter =
        "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDA5OjE1OjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiJ9"
    private static let messagesAfter =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSJ9"

    static let nativeMessageEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.messages.conversations.default") {
            Endpoint.myMessages()
        },
        ManifestRegisteredEndpoint(id: "native.messages.conversations.page-2") {
            Endpoint.myMessages(after: conversationsAfter)
        },
        ManifestRegisteredEndpoint(id: "native.messages.conversation.default") {
            Endpoint.myMessageConversation(conversationId: conversationId)
        },
        ManifestRegisteredEndpoint(id: "native.messages.create.default") {
            Endpoint.createMyMessageConversation(userIds: [participantUserId])
        },
        ManifestRegisteredEndpoint(id: "native.messages.thread.default") {
            Endpoint.myMessageConversationMessages(conversationId: conversationId)
        },
        ManifestRegisteredEndpoint(id: "native.messages.thread.page-2") {
            Endpoint.myMessageConversationMessages(conversationId: conversationId, after: messagesAfter, limit: 50)
        },
        ManifestRegisteredEndpoint(id: "native.messages.send.default") {
            Endpoint.sendMyMessageConversationMessage(conversationId: conversationId, text: "Sent from native")
        },
        ManifestRegisteredEndpoint(id: "native.messages.participants.default") {
            Endpoint.myMessageConversationParticipants(conversationId: conversationId)
        },
        ManifestRegisteredEndpoint(id: "native.messages.participant-add.default") {
            Endpoint.addMyMessageConversationParticipant(conversationId: conversationId, userId: addedUserId)
        },
        ManifestRegisteredEndpoint(id: "native.messages.participant-remove.default") {
            Endpoint.removeMyMessageConversationParticipant(conversationId: conversationId, userId: participantUserId)
        },
        ManifestRegisteredEndpoint(id: "native.messages.policy.default") {
            Endpoint.updateMyMessageConversationParticipantAddPolicy(
                conversationId: conversationId,
                policy: .ownerOnly
            )
        },
        ManifestRegisteredEndpoint(id: "native.messages.user-search.default") {
            Endpoint.myMessageUserSearch(query: "bo", limit: 10)
        },
        ManifestRegisteredEndpoint(id: "native.memberships.plans.default") {
            Endpoint.membershipPlans
        },
        ManifestRegisteredEndpoint(id: "native.memberships.grant.default") {
            Endpoint.grantMembership(
                userId: "00000000-0000-7000-8000-000000000003",
                plan: .plus,
                skuId: "00000000-0000-7000-8000-000000000701"
            )
        },
        ManifestRegisteredEndpoint(id: "native.identity-verification-attempts.grant.default") {
            Endpoint.grantIdentityVerificationAttempt(
                userId: "00000000-0000-7000-8000-000000000003",
                note: "Provider terminal error reviewed by support."
            )
        }
    ]
}
