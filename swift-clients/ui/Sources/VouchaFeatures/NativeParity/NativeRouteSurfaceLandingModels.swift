struct NativePublicLandingPageResponse: Decodable {
    let landingPage: NativeLandingPageSummary

    private enum CodingKeys: String, CodingKey {
        case landingPage
        case page
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
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
