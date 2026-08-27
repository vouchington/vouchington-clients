import Foundation

public enum FriendRecommendationProvider: String, Codable, Sendable {
    case facebook
    // swiftlint:disable:next identifier_name
    case x
    case github
}

public struct FriendRecommendation: Codable, Identifiable, Sendable {
    public let entityType: String
    public let id: String
    public let provider: FriendRecommendationProvider
    public let providerFriendName: String

    private enum CodingKeys: String, CodingKey {
        case id
        case provider
        case providerFriendName
    }

    public init(from decoder: any Decoder) throws {
        let raw = try decoder.singleValueContainer().decode([String: DecodedJSONValue].self)
        guard case let .string(entityType) = raw["__entity_type"] else {
            throw DecodingError.keyNotFound(
                CodingKeys.id,
                .init(codingPath: decoder.codingPath, debugDescription: "Missing __entity_type")
            )
        }
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.entityType = entityType
        id = try container.decode(String.self, forKey: .id)
        provider = try container.decode(FriendRecommendationProvider.self, forKey: .provider)
        providerFriendName = try container.decode(String.self, forKey: .providerFriendName)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode([
            "__entity_type": DecodedJSONValue.string(entityType),
            "id": .string(id),
            "provider": .string(provider.rawValue),
            "provider_friend_name": .string(providerFriendName)
        ])
    }
}

public struct FriendRecommendationsResponse: Codable, Sendable {
    public let results: [FriendRecommendation]
    public let pageInfo: Page<FriendRecommendation>.PageInfo
    public let users: [String: PublicUser]
}
