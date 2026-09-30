import Foundation

public struct PublicUser: Codable, Identifiable, Sendable {
    public let id: String
    public let name: String?
    public let username: String
    public let entityType: String?
    public let displayAccount: UserDisplayAccount?
    public let isOfficialAccount: Bool?
    public let roles: [String]?
    public let displayNameSource: String?
    public let useDisplayNameFrom: DisplayNameSource?
    public let profileImageId: String?
    public let markdown: String?
    public let verificationStatus: String?
    public let verifiedBadgeVisible: Bool?
    public let verifiedDisplayName: String?
    public let publicVerifiedNameDisplay: String?
    public let linguaRsDetectedLanguage: String?
    public let createdAt: Date?
    public let updatedAt: Date?
    private static let iso8601Formatter = ISO8601DateFormatter()

    private enum CodingKeys: String, CodingKey {
        case id
        case name
        case username
        case entityType = "__entity_type"
        case displayAccount
        case isOfficialAccount
        case roles
        case displayNameSource
        case useDisplayNameFrom
        case profileImageId
        case markdown
        case verificationStatus
        case verifiedBadgeVisible
        case verifiedDisplayName
        case publicVerifiedNameDisplay
        case linguaRsDetectedLanguage
        case createdAt
        case updatedAt
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let raw = try decoder.singleValueContainer().decode([String: DecodedJSONValue].self)
        id = try container.decode(String.self, forKey: .id)
        name = try container.decodeIfPresent(String.self, forKey: .name)
        username = try container.decode(String.self, forKey: .username)
        if case let .string(value) = raw["__entity_type"] {
            entityType = value
        } else {
            entityType = nil
        }
        displayAccount = try container.decodeIfPresent(UserDisplayAccount.self, forKey: .displayAccount)
        isOfficialAccount = try container.decodeIfPresent(Bool.self, forKey: .isOfficialAccount)
        roles = try container.decodeIfPresent([String].self, forKey: .roles)
        displayNameSource = try container.decodeIfPresent(String.self, forKey: .displayNameSource)
        useDisplayNameFrom = try container.decodeIfPresent(DisplayNameSource.self, forKey: .useDisplayNameFrom)
        profileImageId = try container.decodeIfPresent(String.self, forKey: .profileImageId)
        markdown = try container.decodeIfPresent(String.self, forKey: .markdown)
        verificationStatus = try container.decodeIfPresent(String.self, forKey: .verificationStatus)
        verifiedBadgeVisible = try container.decodeIfPresent(Bool.self, forKey: .verifiedBadgeVisible)
        verifiedDisplayName = try container.decodeIfPresent(String.self, forKey: .verifiedDisplayName)
        publicVerifiedNameDisplay = try container.decodeIfPresent(String.self, forKey: .publicVerifiedNameDisplay)
        linguaRsDetectedLanguage = try container.decodeIfPresent(String.self, forKey: .linguaRsDetectedLanguage)
        createdAt = try container.decodeIfPresent(Date.self, forKey: .createdAt)
        updatedAt = try container.decodeIfPresent(Date.self, forKey: .updatedAt)
    }

    public func encode(to encoder: Encoder) throws {
        var object: [String: DecodedJSONValue] = [
            "id": .string(id),
            "username": .string(username)
        ]
        object["__entity_type"] = entityType.map { .string($0) }
        object["name"] = name.map { .string($0) }
        object["display_account"] = displayAccount.map {
            .object([
                "name": $0.name.map { .string($0) } ?? .null
            ])
        }
        object["is_official_account"] = isOfficialAccount.map { .bool($0) }
        object["roles"] = roles.map { .array($0.map { .string($0) }) }
        object["display_name_source"] = displayNameSource.map { .string($0) }
        object["use_display_name_from"] = useDisplayNameFrom.map { .string($0.rawValue) }
        object["profile_image_id"] = profileImageId.map { .string($0) }
        object["markdown"] = markdown.map { .string($0) }
        object["verification_status"] = verificationStatus.map { .string($0) }
        object["verified_badge_visible"] = verifiedBadgeVisible.map { .bool($0) }
        object["verified_display_name"] = verifiedDisplayName.map { .string($0) }
        object["public_verified_name_display"] = publicVerifiedNameDisplay.map { .string($0) }
        object["lingua_rs_detected_language"] = linguaRsDetectedLanguage.map { .string($0) }
        object["created_at"] = createdAt.map { .string(PublicUser.iso8601Formatter.string(from: $0)) }
        object["updated_at"] = updatedAt.map { .string(PublicUser.iso8601Formatter.string(from: $0)) }
        var container = encoder.singleValueContainer()
        try container.encode(object)
    }
}

public struct UserDisplayAccount: Codable, Sendable {
    public let name: String?
}

public struct OAuthAccountInfo: Codable, Equatable, Sendable {
    public let id: String
    public let name: String?
    public let emailAddress: String?

    public init(id: String, name: String?, emailAddress: String?) {
        self.id = id
        self.name = name
        self.emailAddress = emailAddress
    }
}

/// AT Protocol account linking (Phase D) has no numeric provider ID or email — identity is the
/// DID, with handle as the mutable human-readable label — so it does not fit UserDisplayAccount's
/// shape.
public struct BlueskyAccount: Codable, Sendable {
    public let did: String
    public let handle: String?
}
