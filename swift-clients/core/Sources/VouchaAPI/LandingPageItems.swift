public enum LandingPageTopicGroupEntry: Codable, Identifiable, Sendable {
    case review(id: String, review: LandingPageReview)
    case referralLink(id: String, referralLink: LandingPageReferralLink)

    private enum CodingKeys: String, CodingKey {
        case id
        case type
        case review
        case referralLink
    }

    public var id: String {
        switch self {
        case let .review(id, _), let .referralLink(id, _):
            id
        }
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let id = try container.decode(String.self, forKey: .id)
        let type = try container.decode(String.self, forKey: .type)
        switch type {
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
                debugDescription: "Unsupported landing page topic group entry type: \(type)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        switch self {
        case let .review(_, review):
            try container.encode("review", forKey: .type)
            try container.encode(review, forKey: .review)
        case let .referralLink(_, referralLink):
            try container.encode("referral_link", forKey: .type)
            try container.encode(referralLink, forKey: .referralLink)
        }
    }
}

public enum LandingPageItem: Codable, Identifiable, Sendable {
    case profileLink(id: String, profileLink: ProfileLink)
    case review(id: String, review: LandingPageReview)
    case referralLink(id: String, referralLink: LandingPageReferralLink)
    case topicGroup(id: String, topic: LandingPageTopic, entries: [LandingPageTopicGroupEntry])
    case link(id: String, label: String, url: String)

    private enum CodingKeys: String, CodingKey {
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

    public var id: String {
        switch self {
        case let .profileLink(id, _), let .review(id, _), let .referralLink(id, _),
             let .topicGroup(id, _, _), let .link(id, _, _):
            id
        }
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let id = try container.decode(String.self, forKey: .id)
        let type = try container.decode(String.self, forKey: .type)
        switch type {
        case "profile_link":
            let profileLink = try container.decode(ProfileLink.self, forKey: .profileLink)
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
                debugDescription: "Unsupported landing page item type: \(type)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        switch self {
        case let .profileLink(_, profileLink):
            try container.encode("profile_link", forKey: .type)
            try container.encode(profileLink, forKey: .profileLink)
        case let .review(_, review):
            try container.encode("review", forKey: .type)
            try container.encode(review, forKey: .review)
        case let .referralLink(_, referralLink):
            try container.encode("referral_link", forKey: .type)
            try container.encode(referralLink, forKey: .referralLink)
        case let .topicGroup(_, topic, entries):
            try container.encode("topic_group", forKey: .type)
            try container.encode(topic, forKey: .topic)
            try container.encode(entries, forKey: .entries)
        case let .link(_, label, url):
            try container.encode("link", forKey: .type)
            try container.encode(label, forKey: .label)
            try container.encode(url, forKey: .url)
        }
    }
}
