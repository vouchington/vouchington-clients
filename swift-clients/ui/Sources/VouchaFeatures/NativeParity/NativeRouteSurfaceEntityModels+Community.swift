import VouchaModels

struct NativeCommunityDetailResponse: Decodable {
    let community: NativeGenericEntity
    let communityMetrics: NativeCommunityMetrics?
    let membership: NativeCommunityMember?
    let hasPendingApplication: Bool?
}

struct NativeCommunityMetrics: Decodable {
    let memberCount: Int?
    let postCount: Int?
    let listItemCount: Int?
}

struct NativeCommunityMembersResponse: Decodable {
    let results: [NativeGenericEntity]
    let communityMembers: [String: NativeCommunityMember]
    let users: [String: NativeGenericEntity]
}

struct NativeCommunityMember: Decodable {
    let userId: String
    let role: String
}

struct NativeCommunityPostsResponse: Decodable {
    let results: [NativeGenericEntity]
    let pageInfo: Page<FixtureReference>.PageInfo?
    let posts: [String: NativePostSummary]
    let postLinkEmbeds: [String: UrlEmbed]?

    init(
        results: [NativeGenericEntity],
        pageInfo: Page<FixtureReference>.PageInfo? = nil,
        posts: [String: NativePostSummary]
    ) {
        self.results = results
        self.pageInfo = pageInfo
        self.posts = posts
        postLinkEmbeds = nil
    }
}

struct NativeCommunityListItemCounts: Decodable {
    let topic: Int
    let rssFeed: Int
    let post: Int
    let urlHostname: Int
    let url: Int

    var total: Int {
        topic + rssFeed + post + urlHostname + url
    }
}
