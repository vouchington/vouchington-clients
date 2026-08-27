import Foundation
import VouchaModels

public struct ProfileLink: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let linkType: String
    public let sortOrder: Int
    public let urlId: String?
    public let url: String?
    public let handle: String?
    public let name: String?
    public let imageId: String?
    public let createdAt: Date
    public let updatedAt: Date
}

public struct LandingPageSummary: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let title: String
    public let subtitle: String?
    public let slug: String
    public let isDefault: Bool
    public let createdAt: Date
    public let updatedAt: Date
}

public struct LandingPageListResponse: Codable, Sendable {
    public let results: [LandingPageSummary]
    public let pageInfo: Page<LandingPageSummary>.PageInfo
}

/// GET /api/v1/my/landing-pages — bounded by a create-time `MAX_LANDING_PAGES` cap, so it returns
/// a plain results array with no page_info (unlike the cursor-paginated admin listing above).
public struct MyLandingPageListResponse: Codable, Sendable {
    public let results: [LandingPageSummary]
}

public struct LandingPageDetailResponse: Codable, Sendable {
    public let landingPage: LandingPageDetail
}

public struct LandingPageSummaryResponse: Codable, Sendable {
    public let landingPage: LandingPageSummary
}

public struct LandingPageCandidatesResponse: Codable, Sendable {
    public let candidates: LandingPageCandidates
}

public struct LandingPageCandidates: Codable, Sendable {
    public let canCreateLandingPages: Bool
    public let profileLinks: [ProfileLink]
    public let reviews: [LandingPageReview]
    public let referralLinks: [LandingPageReferralLink]
}

public struct LandingPageDetail: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let title: String
    public let subtitle: String?
    public let slug: String
    public let isDefault: Bool
    public let createdAt: Date
    public let updatedAt: Date
    public let items: [LandingPageItem]
}

public struct LandingPageReview: Codable, Identifiable, Sendable {
    public let id: String
    public let title: String
    public let slug: String?
    public let markdown: String
    public let createdAt: Date
    public let reviewTopicRatings: [LandingPageReviewTopicRating]
}

public struct LandingPageReviewTopicRating: Codable, Sendable {
    public let topicId: String
    public let topicName: String
    public let topicSlug: String
    public let rating: Int
    public let orderIndex: Int
}

public struct LandingPageReferralLink: Codable, Identifiable, Sendable {
    public let id: String
    public let referralProgramId: String
    public let referralProgramName: String
    public let referralProgramSlug: String
    public let label: String?
    public let url: String
}

public struct LandingPageTopic: Codable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String
    public let topicType: String
}
