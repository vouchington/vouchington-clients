import VouchaModels

struct CommunityCreateBody: Encodable {
    let name: String
    let slug: String?
    let markdown: String?
    let visibility: CommunityVisibility?
    let listType: CommunityListType?
    let memberRosterVisibility: CommunityMemberRosterVisibility?
    let memberInvitesAllowedAt: Bool?
    let postApprovalRequiredAt: Bool?
    let shouldAllowReviewPosts: Bool?
    let shouldAllowDataPointPosts: Bool?
    let profileImageId: String?
    let bannerImageId: String?
    let defaultLanguage: String?
    let cfTurnstileResponse: String?
}

struct CommunityUpdateBody: Encodable {
    let name: String?
    let slug: String?
    let markdown: String?
    let clearMarkdown: Bool
    let visibility: CommunityVisibility?
    let memberRosterVisibility: CommunityMemberRosterVisibility?
    let listType: CommunityListType?
    let clearListType: Bool
    let memberInvitesAllowedAt: Bool?
    let postApprovalRequiredAt: Bool?
    let profileImageId: String?
    let clearProfileImageId: Bool
    let bannerImageId: String?
    let clearBannerImageId: Bool
    let defaultLanguage: String?
    let clearDefaultLanguage: Bool
    let archive: Bool?

    enum CodingKeys: String, CodingKey {
        case name
        case slug
        case markdown
        case visibility
        case memberRosterVisibility = "member_roster_visibility"
        case listType = "list_type"
        case memberInvitesAllowedAt = "member_invites_allowed_at"
        case postApprovalRequiredAt = "post_approval_required_at"
        case profileImageId = "profile_image_id"
        case bannerImageId = "banner_image_id"
        case defaultLanguage = "default_language"
        case archive
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encodeIfPresent(name, forKey: .name)
        try container.encodeIfPresent(slug, forKey: .slug)
        if let markdown {
            try container.encode(markdown, forKey: .markdown)
        } else if clearMarkdown {
            try container.encodeNil(forKey: .markdown)
        }
        try container.encodeIfPresent(visibility, forKey: .visibility)
        try container.encodeIfPresent(memberRosterVisibility, forKey: .memberRosterVisibility)
        if let listType {
            try container.encode(listType, forKey: .listType)
        } else if clearListType {
            try container.encodeNil(forKey: .listType)
        }
        try container.encodeIfPresent(memberInvitesAllowedAt, forKey: .memberInvitesAllowedAt)
        try container.encodeIfPresent(postApprovalRequiredAt, forKey: .postApprovalRequiredAt)
        if let profileImageId {
            try container.encode(profileImageId, forKey: .profileImageId)
        } else if clearProfileImageId {
            try container.encodeNil(forKey: .profileImageId)
        }
        if let bannerImageId {
            try container.encode(bannerImageId, forKey: .bannerImageId)
        } else if clearBannerImageId {
            try container.encodeNil(forKey: .bannerImageId)
        }
        if let defaultLanguage {
            try container.encode(defaultLanguage, forKey: .defaultLanguage)
        } else if clearDefaultLanguage {
            try container.encodeNil(forKey: .defaultLanguage)
        }
        try container.encodeIfPresent(archive, forKey: .archive)
    }
}

public struct CommunityAutomodSimulationBody: Encodable, Sendable {
    public let promptId: String
    public let prompt: String?
    public let timeWindowHours: Int?
    public let limit: Int?

    public init(
        promptId: String,
        prompt: String? = nil,
        timeWindowHours: Int? = nil,
        limit: Int? = nil
    ) {
        self.promptId = promptId
        self.prompt = prompt
        self.timeWindowHours = timeWindowHours
        self.limit = limit
    }
}
