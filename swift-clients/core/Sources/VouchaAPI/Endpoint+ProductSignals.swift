import Foundation

public struct LandingPageVisitSignal: Encodable, Sendable, Equatable {
    public var referrer: String?
    public var utmSource: String?
    public var utmMedium: String?
    public var utmCampaign: String?
    public var utmContent: String?

    public init(
        referrer: String? = nil,
        utmSource: String? = nil,
        utmMedium: String? = nil,
        utmCampaign: String? = nil,
        utmContent: String? = nil
    ) {
        self.referrer = referrer
        self.utmSource = utmSource
        self.utmMedium = utmMedium
        self.utmCampaign = utmCampaign
        self.utmContent = utmContent
    }

    private enum CodingKeys: String, CodingKey {
        case referrer
        case utmSource = "utm_source"
        case utmMedium = "utm_medium"
        case utmCampaign = "utm_campaign"
        case utmContent = "utm_content"
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encodeIfPresent(referrer, forKey: .referrer)
        try container.encodeIfPresent(utmSource, forKey: .utmSource)
        try container.encodeIfPresent(utmMedium, forKey: .utmMedium)
        try container.encodeIfPresent(utmCampaign, forKey: .utmCampaign)
        try container.encodeIfPresent(utmContent, forKey: .utmContent)
    }
}

public struct LandingPageClickSignal: Encodable, Sendable, Equatable {
    public var landingPageItemId: String
    public var groupMemberId: String?

    public init(landingPageItemId: String, groupMemberId: String? = nil) {
        self.landingPageItemId = landingPageItemId
        self.groupMemberId = groupMemberId
    }

    private enum CodingKeys: String, CodingKey {
        case landingPageItemId = "landing_page_item_id"
        case groupMemberId = "group_member_id"
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(landingPageItemId, forKey: .landingPageItemId)
        try container.encodeIfPresent(groupMemberId, forKey: .groupMemberId)
    }
}

public extension Endpoint {
    static func landingPageVisit(landingPageId: String, signal: LandingPageVisitSignal) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/landing-pages/\(pathSegment(landingPageId))/visits",
            body: signal,
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func landingPageClick(landingPageId: String, signal: LandingPageClickSignal) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/landing-pages/\(pathSegment(landingPageId))/clicks",
            body: signal,
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func rssFeedView(rssFeedId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/rss-feeds/\(pathSegment(rssFeedId))/views")
    }

    static func rssFeedItemView(itemId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/rss-feed-items/\(pathSegment(itemId))/views")
    }
}
