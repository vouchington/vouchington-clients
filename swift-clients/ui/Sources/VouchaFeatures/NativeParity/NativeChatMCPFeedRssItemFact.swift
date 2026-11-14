import Foundation
import VouchaAPI
import VouchaModels

extension NativeChatMCPFact {
    /// Feed results contain delivery references, not hydrated RSS items.
    static func makeFeedRssItemIds(_ value: DecodedJSONValue) -> Self? {
        guard let data = try? value.exactJSONData(),
              let envelope = try? JSONDecoder().decode(FeedRssItemsEnvelope.self, from: data),
              envelope.success,
              envelope.results.count <= 100,
              envelope.results.allSatisfy({ UUID(uuidString: $0.entityId) != nil }),
              Set(envelope.results.map(\.entityId)).count == envelope.results.count else { return nil }
        return .feedRssItemIds(envelope.results.map(\.entityId))
    }
}

private struct FeedRssItemsEnvelope: Decodable {
    let success: Bool
    let results: [FeedRssItemDelivery]
}

private struct FeedRssItemDelivery: Decodable {
    let entityId: String

    enum CodingKeys: String, CodingKey { case entityId = "entity_id" }
}
