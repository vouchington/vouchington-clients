import VouchaModels

struct NativeTopicRecommendationMutationBody: Encodable {
    let title: String?
    let markdown: String
    let topicTitle: String
    let topicSlug: String
    let topicMarkdown: String?
    let topicHostname: String?
    let topicHostnames: [String]
    let topicAliases: [String]
    let topicType: String
    let exampleReferralLink: String?
    let landingPageUrls: [String]
    let cfTurnstileResponse: String?
}

struct NativeTopicRecommendationPostEnvelope: Decodable {
    let post: NativeTopicRecommendationPost
    let postElection: PostElection?
    let electionVote: NativeViewerVote?
}

struct NativeTopicRecommendationPost: Decodable {
    let id: String
    let title: String?
    let markdown: String?
    let topicRecommendation: NativeTopicRecommendationFields?
}

struct NativeTopicRecommendationFields: Decodable {
    let topicTitle: String?
    let topicSlug: String?
    let topicMarkdown: String?
    let hostname: NativeHostnameName?
    let hostnames: [NativeHostnameName]?
    let aliases: [String]?
    let topicType: String?
    let exampleReferralLink: String?
    let landingPageUrls: [String]?
}
