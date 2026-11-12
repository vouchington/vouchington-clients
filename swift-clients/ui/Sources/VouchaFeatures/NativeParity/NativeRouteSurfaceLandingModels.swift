import VouchaAPI

struct NativePublicLandingPageResponse: Decodable {
    let landingPage: NativePublicLandingPage
    let user: NativeLandingPageOwner
}

typealias NativePublicLandingPage = LandingPageDetail

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
