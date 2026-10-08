public enum LandingPageTopicGroupEntryInput: Encodable, Equatable, Sendable {
    case review(id: String)
    case referralLink(id: String)

    private enum CodingKeys: String, CodingKey {
        case type
        case reviewId = "reviewPostId"
        case referralLinkId
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case let .review(id):
            try container.encode("review", forKey: .type)
            try container.encode(id, forKey: .reviewId)
        case let .referralLink(id):
            try container.encode("referral_link", forKey: .type)
            try container.encode(id, forKey: .referralLinkId)
        }
    }
}

public enum LandingPageItemInput: Encodable, Equatable, Sendable {
    case profileLink(id: String)
    case review(id: String)
    case referralLink(id: String)
    case topicGroup(topicId: String, entries: [LandingPageTopicGroupEntryInput])
    case link(label: String, url: String)

    private enum CodingKeys: String, CodingKey {
        case type
        case profileLinkId
        case reviewId = "reviewPostId"
        case referralLinkId
        case topicId
        case entries
        case label
        case url
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case let .profileLink(id):
            try container.encode("profile_link", forKey: .type)
            try container.encode(id, forKey: .profileLinkId)
        case let .review(id):
            try container.encode("review", forKey: .type)
            try container.encode(id, forKey: .reviewId)
        case let .referralLink(id):
            try container.encode("referral_link", forKey: .type)
            try container.encode(id, forKey: .referralLinkId)
        case let .topicGroup(topicId, entries):
            try container.encode("topic_group", forKey: .type)
            try container.encode(topicId, forKey: .topicId)
            try container.encode(entries, forKey: .entries)
        case let .link(label, url):
            try container.encode("link", forKey: .type)
            try container.encode(label, forKey: .label)
            try container.encode(url, forKey: .url)
        }
    }
}
