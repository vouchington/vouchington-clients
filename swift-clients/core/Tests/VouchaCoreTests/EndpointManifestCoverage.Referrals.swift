import VouchaAPI

extension EndpointManifestCoverage {
    static let referralEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "web.topics.search.referral-programs.default") {
            Endpoint.topics(query: "test", limit: 10, topicTypes: ["referral_program"])
        },
        ManifestRegisteredEndpoint(id: "web.referral-links.feed.default") {
            Endpoint.referralLinksFeed(feedType: "follow_users")
        },
        ManifestRegisteredEndpoint(id: "web.referral-links.mine.default") {
            Endpoint.referralLinks()
        },
        ManifestRegisteredEndpoint(id: "web.referral-clicks.mine.default") {
            Endpoint.myReferralClicks()
        },
        ManifestRegisteredEndpoint(id: "native.referral-links.mine.default") {
            Endpoint.referralLinks()
        },
        ManifestRegisteredEndpoint(id: "native.referral-clicks.mine.default") {
            Endpoint.myReferralClicks()
        },
        ManifestRegisteredEndpoint(id: "web.trending-referral-programs.default") {
            Endpoint.trendingReferralPrograms()
        },
        ManifestRegisteredEndpoint(id: "web.referral-links.prioritized.default") {
            Endpoint.prioritizedReferralLinks(referralProgramId: "referral-program-1", all: true)
        }
    ]
}
