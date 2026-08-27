import Foundation

public struct Community: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let name: String
    public let slug: String
    public let markdown: String?
    public let visibility: CommunityVisibility
    public let memberRosterVisibility: CommunityMemberRosterVisibility
    @RequiredNullable
    public var listType: CommunityListType?
    @RequiredNullable
    public var memberInvitesAllowedAt: Date?
    @RequiredNullable
    public var postApprovalRequiredAt: Date?
    public let allowReviewPosts: Bool
    public let allowDataPointPosts: Bool
    @RequiredNullable
    public var trustedAt: Date?
    @RequiredNullable
    public var profileImageId: String?
    @RequiredNullable
    public var bannerImageId: String?
    public let createdById: String
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable
    public var deletedAt: Date?
    @RequiredNullable
    public var deletedById: String?
    @RequiredNullable
    public var archivedAt: Date?
    @RequiredNullable
    public var archivedById: String?
    @RequiredNullable
    public var defaultLanguage: String?
    @RequiredNullable
    public var linguaRsDetectedLanguage: String?
    @RequiredNullable
    public var rulesMarkdown: String?
    public let owner: DecodedJSONValue?

    private enum CodingKeys: String, CodingKey {
        case entityType
        case id
        case name
        case slug
        case markdown
        case visibility
        case memberRosterVisibility
        case listType
        case memberInvitesAllowedAt
        case postApprovalRequiredAt
        case allowReviewPosts
        case allowDataPointPosts
        case trustedAt
        case profileImageId
        case bannerImageId
        case createdById
        case createdAt
        case updatedAt
        case deletedAt
        case deletedById
        case archivedAt
        case archivedById
        case defaultLanguage
        case linguaRsDetectedLanguage
        case rulesMarkdown
        case owner
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let raw = try decoder.singleValueContainer().decode([String: DecodedJSONValue].self)
        if case let .string(value) = raw["__entity_type"] {
            entityType = value
        } else {
            entityType = nil
        }
        id = try container.decode(String.self, forKey: .id)
        name = try container.decode(String.self, forKey: .name)
        slug = try container.decode(String.self, forKey: .slug)
        markdown = try container.decodeIfPresent(String.self, forKey: .markdown)
        visibility = try container.decode(CommunityVisibility.self, forKey: .visibility)
        memberRosterVisibility = try container.decode(
            CommunityMemberRosterVisibility.self,
            forKey: .memberRosterVisibility
        )
        _listType = try container.decode(RequiredNullable<CommunityListType>.self, forKey: .listType)
        _memberInvitesAllowedAt = try container.decode(RequiredNullable<Date>.self, forKey: .memberInvitesAllowedAt)
        _postApprovalRequiredAt = try container.decode(RequiredNullable<Date>.self, forKey: .postApprovalRequiredAt)
        allowReviewPosts = try container.decode(Bool.self, forKey: .allowReviewPosts)
        allowDataPointPosts = try container.decode(Bool.self, forKey: .allowDataPointPosts)
        _trustedAt = try container.decode(RequiredNullable<Date>.self, forKey: .trustedAt)
        _profileImageId = try container.decode(RequiredNullable<String>.self, forKey: .profileImageId)
        _bannerImageId = try container.decode(RequiredNullable<String>.self, forKey: .bannerImageId)
        createdById = try container.decode(String.self, forKey: .createdById)
        createdAt = try container.decode(Date.self, forKey: .createdAt)
        updatedAt = try container.decode(Date.self, forKey: .updatedAt)
        _deletedAt = try container.decode(RequiredNullable<Date>.self, forKey: .deletedAt)
        _deletedById = try container.decode(RequiredNullable<String>.self, forKey: .deletedById)
        _archivedAt = try container.decode(RequiredNullable<Date>.self, forKey: .archivedAt)
        _archivedById = try container.decode(RequiredNullable<String>.self, forKey: .archivedById)
        _defaultLanguage = try container.decode(RequiredNullable<String>.self, forKey: .defaultLanguage)
        _linguaRsDetectedLanguage = try container.decode(
            RequiredNullable<String>.self,
            forKey: .linguaRsDetectedLanguage
        )
        _rulesMarkdown = try container.decode(RequiredNullable<String>.self, forKey: .rulesMarkdown)
        owner = try container.decodeIfPresent(DecodedJSONValue.self, forKey: .owner)
    }

    public func encode(to encoder: any Encoder) throws {
        var raw = encoder.singleValueContainer()
        var object: [String: DecodedJSONValue] = [:]
        if let entityType {
            object["__entity_type"] = .string(entityType)
        }
        object["id"] = .string(id)
        object["name"] = .string(name)
        object["slug"] = .string(slug)
        object["markdown"] = markdown.map(DecodedJSONValue.string) ?? .null
        object["visibility"] = .string(visibility.rawValue)
        object["member_roster_visibility"] = .string(memberRosterVisibility.rawValue)
        object["list_type"] = listType.map { .string($0.rawValue) } ?? .null
        object["member_invites_allowed_at"] = memberInvitesAllowedAt.map(Self.dateValue) ?? .null
        object["post_approval_required_at"] = postApprovalRequiredAt.map(Self.dateValue) ?? .null
        object["allow_review_posts"] = .bool(allowReviewPosts)
        object["allow_data_point_posts"] = .bool(allowDataPointPosts)
        object["trusted_at"] = trustedAt.map(Self.dateValue) ?? .null
        object["profile_image_id"] = profileImageId.map(DecodedJSONValue.string) ?? .null
        object["banner_image_id"] = bannerImageId.map(DecodedJSONValue.string) ?? .null
        object["created_by_id"] = .string(createdById)
        object["created_at"] = Self.dateValue(createdAt)
        object["updated_at"] = Self.dateValue(updatedAt)
        object["deleted_at"] = deletedAt.map(Self.dateValue) ?? .null
        object["deleted_by_id"] = deletedById.map(DecodedJSONValue.string) ?? .null
        object["archived_at"] = archivedAt.map(Self.dateValue) ?? .null
        object["archived_by_id"] = archivedById.map(DecodedJSONValue.string) ?? .null
        object["default_language"] = defaultLanguage.map(DecodedJSONValue.string) ?? .null
        object["lingua_rs_detected_language"] = linguaRsDetectedLanguage.map(DecodedJSONValue.string) ?? .null
        object["rules_markdown"] = rulesMarkdown.map(DecodedJSONValue.string) ?? .null
        if let owner {
            object["owner"] = owner
        }
        try raw.encode(object)
    }

    private static func dateValue(_ date: Date) -> DecodedJSONValue {
        .string(ISO8601DateFormatter().string(from: date))
    }
}
