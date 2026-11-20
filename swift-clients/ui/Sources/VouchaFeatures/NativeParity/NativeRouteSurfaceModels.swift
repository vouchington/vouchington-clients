import Foundation
import VouchaModels

struct NativeIdentifiedResult: Decodable {
    let id: String
}

struct NativeListResponse: Decodable {
    let results: [NativeIdentifiedResult]
}

struct NativeNotificationsResponse: Decodable {
    let results: [NativeIdentifiedResult]
    let pageInfo: Page<FixtureReference>.PageInfo?
    let notifications: [String: VouchaNotification]
    let hydration: NotificationHydration

    private enum CodingKeys: String, CodingKey {
        case results, pageInfo, notifications
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        results = try container.decode([NativeIdentifiedResult].self, forKey: .results)
        pageInfo = try container.decodeIfPresent(Page<FixtureReference>.PageInfo.self, forKey: .pageInfo)
        notifications = try container.decode([String: VouchaNotification].self, forKey: .notifications)
        hydration = try NotificationHydration(from: decoder)
    }
}

struct NativePostFeedResponse: Decodable {
    let results: [NativePostFeedResult]
    let pageInfo: Page<FixtureReference>.PageInfo?
    let postHackerNewsReferences: [String: HackerNewsReferences]?
    let posts: [String: NativePostSummary]
}

struct NativePostFeedResult: Decodable {
    let id: String
    let entityId: String?
    let deliveryType: String?
    let sharedByUserId: String?
}

struct NativePostSummary: Decodable, Identifiable {
    let id: String
    let slug: String?
    let postType: PostType
    let title: String?
    let markdown: String?
    let declaredLanguage: String?
    let linguaRsDetectedLanguage: String?
    let createdById: String?
    let rootId: String?
    let provenance: PublicContentProvenance?
    let staffProvenance: StaffContentProvenance?

    private enum CodingKeys: String, CodingKey {
        case id, slug, postType, title, markdown, declaredLanguage, linguaRsDetectedLanguage, createdById
        case rootId = "rootPostId"
        case provenance, staffProvenance
    }
}

struct NativeLandingPagesResponse: Decodable {
    let results: [NativeLandingPageSummary]
}

struct NativeLandingPageSummary: Decodable, Identifiable {
    let id: String
    let title: String
    let subtitle: String?
    let slug: String
    let isDefault: Bool
}

struct NativeLandingPageCandidatesResponse: Decodable {
    let candidates: NativeLandingPageCandidates
}

struct NativeLandingPageCandidates: Decodable {
    let profileLinks: [NativeListItem]
    let reviews: [NativeListItem]
    let referralLinks: [NativeListItem]
}

struct NativeApiKeysResponse: Decodable {
    let results: [NativeListItem]
}

struct NativeAsidePreferencesResponse: Decodable {
    let asidePreferences: [NativeAsidePreference]
}

struct NativeAsidePreference: Decodable {
    let asideKey: String
}

struct NativeConsentsResponse: Decodable {
    let results: [NativeListItem]
}

struct NativeContributionStatusResponse: Decodable {
    let contributionStatus: NativeContributionStatus
    let dailyQuota: NativeContributionQuota
}

struct NativeContributionStatus: Decodable {
    let allowed: Bool
    let reason: String?
}

struct NativeContributionQuota: Decodable {
    let limit: Int
    let used: Int
}

struct NativeListItem: Decodable {
    let id: String
}
