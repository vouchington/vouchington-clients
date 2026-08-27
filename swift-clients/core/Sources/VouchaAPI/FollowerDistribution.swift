import Foundation

public enum FollowerDistributionAudience: String, Codable, Sendable {
    case allFollowers = "all_followers"
    case selectedFollowers = "selected_followers"
}

public enum FollowerDistributionRequestError: Error, Equatable, Sendable {
    case emptySelectedRecipients
    case tooManySelectedRecipients
    case duplicateSelectedRecipients
    case invalidRecipientId
}

public struct FollowerDistributionRequest: Encodable, Sendable {
    public static let maximumSelectedRecipients = 100
    public static let allFollowers = Self(audience: .allFollowers, recipientUserIds: nil)
    private let audience: FollowerDistributionAudience
    private let recipientUserIds: [String]?

    public init(selectedRecipientIds: [String]) throws {
        guard !selectedRecipientIds.isEmpty else {
            throw FollowerDistributionRequestError.emptySelectedRecipients
        }
        guard selectedRecipientIds.count <= Self.maximumSelectedRecipients else {
            throw FollowerDistributionRequestError.tooManySelectedRecipients
        }
        guard Set(selectedRecipientIds).count == selectedRecipientIds.count else {
            throw FollowerDistributionRequestError.duplicateSelectedRecipients
        }
        guard selectedRecipientIds.allSatisfy({ UUID(uuidString: $0) != nil }) else {
            throw FollowerDistributionRequestError.invalidRecipientId
        }
        self.init(audience: .selectedFollowers, recipientUserIds: selectedRecipientIds)
    }

    private init(audience: FollowerDistributionAudience, recipientUserIds: [String]?) {
        self.audience = audience
        self.recipientUserIds = recipientUserIds
    }

    public func encode(to encoder: Encoder) throws {
        enum CodingKeys: String, CodingKey {
            case audience
            case recipientUserIds = "recipient_user_ids"
        }
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(audience, forKey: .audience)
        if let recipientUserIds {
            try container.encode(recipientUserIds, forKey: .recipientUserIds)
        }
    }
}

public struct FollowerDistributionAcceptedResponse: Codable, Sendable, Equatable {
    public let status: String
    public let distributionId: String
}
