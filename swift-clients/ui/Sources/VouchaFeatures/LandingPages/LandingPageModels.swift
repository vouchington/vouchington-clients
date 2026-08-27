import Foundation

public struct LandingPagesResponse: Decodable, Sendable {
    public let results: [LandingPage]
}

public struct LandingPageResponse: Decodable, Sendable {
    public let landingPage: LandingPage
}

public struct LandingPageWithItemsResponse: Decodable, Sendable {
    public let landingPage: LandingPageWithItems
}

public struct LandingPageCandidatesResponse: Decodable, Sendable {
    public let candidates: LandingPageCandidates
}

public struct LandingPage: Decodable, Identifiable, Equatable, Sendable {
    public let id: String
    public let userId: String
    public let title: String
    public let subtitle: String?
    public let slug: String
    public let isDefault: Bool
    public let createdAt: Date
    public let updatedAt: Date
}

public struct LandingPageWithItems: Decodable, Identifiable, Equatable, Sendable {
    public let id: String
    public let userId: String
    public let title: String
    public let subtitle: String?
    public let slug: String
    public let isDefault: Bool
    public let createdAt: Date
    public let updatedAt: Date
    public let items: [LandingPageItem]
    public var summary: LandingPage {
        LandingPage(
            id: id,
            userId: userId,
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: createdAt,
            updatedAt: updatedAt
        )
    }
}

public struct LandingPageCandidates: Decodable, Equatable, Sendable {
    public static let empty = LandingPageCandidates(
        canCreateLandingPages: false,
        profileLinks: [],
        reviews: [],
        referralLinks: []
    )
    public let canCreateLandingPages: Bool
    public let profileLinks: [LandingPageProfileLink]
    public let reviews: [LandingPageReview]
    public let referralLinks: [LandingPageReferralLink]
}

public struct LandingPageProfileLink: Decodable, Identifiable, Equatable, Sendable {
    public let id: String
    public let url: String?
    public let handle: String?
    public let name: String?
}

public struct LandingPageReview: Decodable, Identifiable, Equatable, Sendable {
    public let id: String
    public let title: String
    public let slug: String?
    public let markdown: String
    public let createdAt: Date
    public let reviewTopicRatings: [LandingPageReviewTopicRating]
}

public struct LandingPageReviewTopicRating: Decodable, Equatable, Sendable {
    public let topicId: String
    public let topicName: String
    public let topicSlug: String
    public let rating: Int
    public let orderIndex: Int
}

public struct LandingPageReferralLink: Decodable, Identifiable, Equatable, Sendable {
    public let id: String
    public let referralProgramId: String
    public let referralProgramName: String
    public let referralProgramSlug: String
    public let label: String?
    public let url: String
}

public struct LandingPageTopic: Decodable, Identifiable, Equatable, Sendable {
    public let id: String
    public let name: String
    public let slug: String
    public let topicType: String
}

public enum LandingPageItem: Decodable, Identifiable, Equatable, Sendable {
    case profileLink(id: String, profileLink: LandingPageProfileLink)
    case review(id: String, review: LandingPageReview)
    case referralLink(id: String, referralLink: LandingPageReferralLink)
    case topicGroup(id: String, topic: LandingPageTopic, entries: [LandingPageTopicGroupEntry])
    case link(id: String, label: String, url: String)

    public var id: String {
        switch self {
        case let .profileLink(id, _), let .review(id, _), let .referralLink(id, _),
             let .topicGroup(id, _, _), let .link(id, _, _):
            id
        }
    }

    enum CodingKeys: String, CodingKey {
        case id
        case type
        case profileLink
        case review
        case referralLink
        case topic
        case entries
        case label
        case url
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let id = try container.decode(String.self, forKey: .id)
        switch try container.decode(String.self, forKey: .type) {
        case "profile_link":
            let profileLink = try container.decode(LandingPageProfileLink.self, forKey: .profileLink)
            self = .profileLink(id: id, profileLink: profileLink)
        case "review":
            let review = try container.decode(LandingPageReview.self, forKey: .review)
            self = .review(id: id, review: review)
        case "referral_link":
            let referralLink = try container.decode(LandingPageReferralLink.self, forKey: .referralLink)
            self = .referralLink(id: id, referralLink: referralLink)
        case "topic_group":
            let topic = try container.decode(LandingPageTopic.self, forKey: .topic)
            let entries = try container.decode([LandingPageTopicGroupEntry].self, forKey: .entries)
            self = .topicGroup(id: id, topic: topic, entries: entries)
        case "link":
            let label = try container.decode(String.self, forKey: .label)
            let url = try container.decode(String.self, forKey: .url)
            self = .link(id: id, label: label, url: url)
        default:
            throw DecodingError.dataCorruptedError(
                forKey: .type,
                in: container,
                debugDescription: "Unknown landing page item type"
            )
        }
    }
}

public enum LandingPageTopicGroupEntry: Decodable, Identifiable, Equatable, Sendable {
    case review(id: String, review: LandingPageReview)
    case referralLink(id: String, referralLink: LandingPageReferralLink)

    public var id: String {
        switch self {
        case let .review(id, _), let .referralLink(id, _): id
        }
    }

    enum CodingKeys: String, CodingKey {
        case id
        case type
        case review
        case referralLink
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let id = try container.decode(String.self, forKey: .id)
        switch try container.decode(String.self, forKey: .type) {
        case "review":
            let review = try container.decode(LandingPageReview.self, forKey: .review)
            self = .review(id: id, review: review)
        case "referral_link":
            let referralLink = try container.decode(LandingPageReferralLink.self, forKey: .referralLink)
            self = .referralLink(id: id, referralLink: referralLink)
        default:
            throw DecodingError.dataCorruptedError(
                forKey: .type,
                in: container,
                debugDescription: "Unknown landing page group entry type"
            )
        }
    }
}
