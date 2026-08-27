import VouchaModels

struct NativeHostnameDetailResponse: Decodable {
    let hostname: NativeHostnameSummary
    let hostnameElection: TopicElection?
    let electionVote: NativeViewerVote?
    let rssFeeds: [NativeRssFeedSummary]?
    let topUrls: [NativeUrlSummary]?
    let topic: NativeTopicSummary?
}

struct NativeUrlDetailResponse: Decodable {
    let canTriggerCrawl: Bool
    let canViewCrawlHistory: Bool
    let canViewLatestCrawl: Bool
    let latestCrawl: NativeUrlCrawlSummary?
    let url: NativeUrlSummary
    let urlType: String?
}

struct NativeUrlSummary: Decodable, Identifiable {
    let id: String
    let url: String
    let hostname: NativeHostnameSummary?
    let pathname: String?
}
