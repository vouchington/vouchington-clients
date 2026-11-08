struct NativePublicLandingPageResponse: Decodable {
    let landingPage: NativeLandingPageSummary
    let user: NativeLandingPageOwner?

    private enum CodingKeys: String, CodingKey {
        case user
        case landingPage
        case page
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        user = try container.decodeIfPresent(NativeLandingPageOwner.self, forKey: .user)
        if let landingPage = try container.decodeIfPresent(NativeLandingPageSummary.self, forKey: .landingPage) {
            self.landingPage = landingPage
            return
        }
        if let page = try container.decodeIfPresent(NativeLandingPageSummary.self, forKey: .page) {
            landingPage = page
            return
        }
        landingPage = try NativeLandingPageSummary(from: decoder)
    }
}

struct NativeTopicRecommendationsFeedResponse: Decodable {
    let results: [NativePostFeedResult]
    let posts: [String: NativePostSummary]
}

struct NativeTopicRecommendationDetailResponse: Decodable {
    let post: NativePostSummary
}

struct NativeLandingPageOwner: Decodable {
    let username: String?
}
