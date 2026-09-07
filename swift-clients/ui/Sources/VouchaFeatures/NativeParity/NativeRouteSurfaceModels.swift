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
}

struct NativePostFeedResponse: Decodable {
    let results: [NativePostFeedResult]
    let pageInfo: Page<FixtureReference>.PageInfo?
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
