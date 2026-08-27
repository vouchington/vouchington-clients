import Foundation

public struct CrmContactSocialAccount: Codable, Identifiable, Sendable {
    public let id: String
    public let entityType: String?
    public let contactId: String
    public let platform: CrmSocialPlatform
    public let handle: String
    public let profileUrl: String?
    public let followerCount: Int?
    public let followerCountUpdatedAt: Date?
    public let createdAt: Date
    public let updatedAt: Date

    public init(
        id: String,
        contactId: String,
        platform: CrmSocialPlatform,
        handle: String,
        profileUrl: String? = nil,
        followerCount: Int? = nil,
        followerCountUpdatedAt: Date? = nil,
        createdAt: Date,
        updatedAt: Date,
        entityType: String? = "crm_contact_social_account"
    ) {
        self.id = id
        self.entityType = entityType
        self.contactId = contactId
        self.platform = platform
        self.handle = handle
        self.profileUrl = profileUrl
        self.followerCount = followerCount
        self.followerCountUpdatedAt = followerCountUpdatedAt
        self.createdAt = createdAt
        self.updatedAt = updatedAt
    }

    enum CodingKeys: String, CodingKey {
        case id
        case entityType = "__entity_type"
        case contactId
        case platform
        case handle
        case profileUrl
        case followerCount
        case followerCountUpdatedAt
        case createdAt
        case updatedAt
    }
}
