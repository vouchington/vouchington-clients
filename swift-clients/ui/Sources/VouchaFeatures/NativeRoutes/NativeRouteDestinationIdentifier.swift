public enum NativeRouteDestinationIdentifier: String, CaseIterable, Identifiable, Sendable {
    case feedPosts = "feed-posts"
    case feedNews = "feed-news"
    case feedPodcasts = "feed-podcasts"
    case feedVideos = "feed-videos"
    case feedReferralLinks = "feed-referral-links"

    case webSearch = "web-search"
    case fediverseSearch = "fediverse-search"
    case fediverseInstances = "fediverse-instances"
    case signIn = "sign-in"

    case postsBrowse = "posts-browse"
    case postDetail = "post-detail"
    case postCompose = "post-compose"
    case rssFeedItemDetail = "rss-feed-item-detail"

    case storiesBrowse = "stories-browse"

    case topicsBrowse = "topics-browse"
    case topicImportExport = "topic-import-export"
    case topicDetail = "topic-detail"
    case topicManagement = "topic-management"

    case sourcesBrowse = "sources-browse"
    case sourceImportExport = "source-import-export"
    case sourceDetail = "source-detail"

    case domainsBrowse = "domains-browse"
    case domainDetail = "domain-detail"
    case urlsBrowse = "urls-browse"
    case urlDetail = "url-detail"

    case usersBrowse = "users-browse"
    case userProfile = "user-profile"
    case userAdmin = "user-admin"
    case crmContacts = "crm-contacts"
    case membershipGrants = "membership-grants"
    case supportStaffThreads = "support-staff-threads"
    case supportStaffContacts = "support-staff-contacts"
    case engineeringQueues = "engineering-queues"
    case engineeringPostgresql = "engineering-postgresql"
    case engineeringValkey = "engineering-valkey"
    case engineeringAiCosts = "engineering-ai-costs"
    case engineeringDynamicConfig = "engineering-dynamic-config"
    case growthDashboard = "growth-dashboard"

    case communitiesBrowse = "communities-browse"
    case communityDetail = "community-detail"

    case messages
    case chat
    case support
    case notifications

    case accountSettings = "account-settings"
    case profileSettings = "profile-settings"
    case household
    case paymentCards = "payment-cards"
    case pointValuations = "point-valuations"
    case spendingCategories = "spending-categories"
    case rewardsProgramStatuses = "rewards-program-statuses"
    case advancedSettings = "advanced-settings"
    case notificationSettings = "notification-settings"
    case friendRecommendations = "friend-recommendations"

    case referrals
    case landingPages = "landing-pages"
    case lists
    case bookmarks
    case plans
    case topicRecommendations = "topic-recommendations"
    case moderationReports = "moderation-reports"
    case moderationAppeals = "moderation-appeals"
    case moderationDisputes = "moderation-disputes"
    case moderationReviewQueue = "moderation-review-queue"
    case moderationAdmin = "moderation-admin"
    case moderationIntegrity = "moderation-integrity"
    case moderationCases = "moderation-cases"
    case moderationTransparency = "moderation-transparency"
    case compare

    public var id: String {
        rawValue
    }

    public var requiresAuthenticatedSession: Bool {
        self == .topicImportExport || self == .sourceImportExport || self == .friendRecommendations
    }

    public var isFediverseDestination: Bool {
        self == .fediverseSearch || self == .fediverseInstances
    }

    public func shouldInvalidateRoute(featureFlags: [String: Bool]) -> Bool {
        isFediverseDestination && featureFlags["fediverse"] != true
    }
}

public enum NativeFeatureFlagRouteInvalidation {
    public static func clearIfNeeded(
        entry: inout NativeRouteCatalogEntry?,
        match: inout NativeRouteMatch?,
        query: inout String?,
        featureFlags: [String: Bool]
    ) {
        let destinationIsUnavailable =
            entry?.destinationIdentifier?.shouldInvalidateRoute(featureFlags: featureFlags) == true
        let matchedRouteIsUnavailable =
            match?.requiresFediverseFeature == true && featureFlags["fediverse"] != true
        guard destinationIsUnavailable || matchedRouteIsUnavailable else { return }
        entry = nil
        match = nil
        query = nil
    }
}
