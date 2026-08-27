public struct LandingPageAnalyticsResponse: Codable, Sendable {
    public let analytics: LandingPageAnalytics
}

public struct AdminLandingPageAnalyticsResponse: Codable, Sendable {
    public let landingPage: LandingPageDetail
    public let analytics: LandingPageAnalytics
}

public struct LandingPageAnalytics: Codable, Sendable {
    public let totalVisits: Int
    public let totalClicks: Int
    public let uniqueVisitors: Int
    public let ctr: Double
    public let itemClicks: [LandingPageItemClick]
    public let dailyStats: [LandingPageDailyStat]
    public let utmSources: [LandingPageUtmSource]
    public let conversionFunnel: LandingPageConversionFunnel
}

public struct LandingPageItemClick: Codable, Identifiable, Sendable {
    public let itemId: String
    public let itemType: String
    public let clickCount: Int

    public var id: String {
        itemId
    }
}

public struct LandingPageDailyStat: Codable, Identifiable, Sendable {
    public let date: String
    public let visits: Int
    public let clicks: Int
    public let uniqueVisitors: Int

    public var id: String {
        date
    }
}

public struct LandingPageUtmSource: Codable, Identifiable, Sendable {
    public let utmSource: String
    public let visits: Int

    public var id: String {
        utmSource
    }
}

public struct LandingPageConversionFunnel: Codable, Sendable {
    public let totalVisits: Int
    public let totalClicks: Int
    public let totalSignups: Int
    public let visitToClickRate: Double
}
