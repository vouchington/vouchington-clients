import Foundation
import VouchaAPI
import VouchaModels

extension NativeChatMCPFact {
    /// Feed results contain delivery references, not hydrated post cards.
    static func makeFeedPostIds(_ value: DecodedJSONValue) -> Self? {
        guard let data = try? value.exactJSONData(),
              let envelope = try? JSONDecoder().decode(FeedPostsEnvelope.self, from: data),
              envelope.success,
              envelope.results.count <= 100,
              envelope.results.allSatisfy({ UUID(uuidString: $0.entityId) != nil }),
              Set(envelope.results.map(\.entityId)).count == envelope.results.count else { return nil }
        return .feedPostIds(envelope.results.map(\.entityId))
    }
}

private struct FeedPostsEnvelope: Decodable {
    let success: Bool
    let results: [FeedPostDelivery]
}

private struct FeedPostDelivery: Decodable {
    let entityId: String

    enum CodingKeys: String, CodingKey { case entityId = "entity_id" }
}
