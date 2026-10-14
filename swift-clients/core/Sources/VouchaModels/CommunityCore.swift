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
    public let automodAction: CommunityAutomodActionSetting
    public let shouldAllowReviewPosts: Bool
    public let shouldAllowDataPointPosts: Bool
    @RequiredNullable
    public var trustedAt: Date?
    @RequiredNullable
    public var profileImageId: String?
    @RequiredNullable
    public var bannerImageId: String?
    public var profileImagePlacement: ImagePlacement?
    public var bannerImagePlacement: ImagePlacement?
    var encodesProfileImagePlacement = true
    var encodesBannerImagePlacement = true
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
    public let provenance: PublicContentProvenance?
    public let staffProvenance: StaffContentProvenance?

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
        case automodAction
        case shouldAllowReviewPosts
        case shouldAllowDataPointPosts
        case trustedAt
        case profileImageId
        case bannerImageId
        case profileImagePlacement
        case bannerImagePlacement
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
        case provenance
        case staffProvenance
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
        automodAction = try container.decode(CommunityAutomodActionSetting.self, forKey: .automodAction)
        shouldAllowReviewPosts = try container.decode(Bool.self, forKey: .shouldAllowReviewPosts)
        shouldAllowDataPointPosts = try container.decode(Bool.self, forKey: .shouldAllowDataPointPosts)
        _trustedAt = try container.decode(RequiredNullable<Date>.self, forKey: .trustedAt)
        _profileImageId = try container.decode(RequiredNullable<String>.self, forKey: .profileImageId)
        _bannerImageId = try container.decode(RequiredNullable<String>.self, forKey: .bannerImageId)
        profileImagePlacement = try container.decodeIfPresent(ImagePlacement.self, forKey: .profileImagePlacement)
        bannerImagePlacement = try container.decodeIfPresent(ImagePlacement.self, forKey: .bannerImagePlacement)
        encodesProfileImagePlacement = container.contains(.profileImagePlacement)
        encodesBannerImagePlacement = container.contains(.bannerImagePlacement)
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
        provenance = try container.decodeIfPresent(PublicContentProvenance.self, forKey: .provenance)
        staffProvenance = try container.decodeIfPresent(StaffContentProvenance.self, forKey: .staffProvenance)
    }
}
