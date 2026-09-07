import VouchaLocalization
import VouchaModels

struct NativePostDetailResponse: Decodable {
    let post: NativePostSummary
}

struct NativeEntityListSurface {
    let path: String
    let icon: String
    let title: UiMessageKey
}

struct NativeGenericListResponse: Decodable {
    let results: [NativeGenericEntity]
    let pageInfo: Page<FixtureReference>.PageInfo?
    let posts: [String: NativePostSummary]?
    let rssFeedItems: [String: NativeRssFeedItemSummary]?
    let hostnames: [String: NativeGenericEntity]?
    let topics: [String: NativeGenericEntity]?
    let communities: [String: NativeGenericEntity]?
    let agents: [String: NativeGenericEntity]?
    let supportThreads: [String: NativeGenericEntity]?
    let users: [String: NativeGenericEntity]?
    let topicElections: [String: TopicElection]?
    let electionVotes: [String: NativeViewerVote]?

    func hydratedEntity(for result: NativeGenericEntity) -> NativeGenericEntity {
        if let post = posts?[result.id] {
            return hydratedPostEntity(post)
        }
        if let item = rssFeedItems?[result.id] {
            return hydratedRssFeedItemEntity(item)
        }
        if let topic = topics?[result.id] {
            return topic
        }
        if let community = communities?[result.id] {
            return community
        }
        if let agent = agents?[result.id] {
            return agent
        }
        if let supportThread = supportThreads?[result.id] {
            return supportThread
        }
        if let hostname = hostnames?[result.id] {
            return hostname
        }
        if let user = users?[result.id] {
            return user
        }
        return result
    }

    private func hydratedPostEntity(_ post: NativePostSummary) -> NativeGenericEntity {
        NativeGenericEntity(
            id: post.id,
            slug: post.slug,
            name: nil,
            title: post.title,
            username: nil,
            subject: nil,
            status: post.postType.protocolValue,
            postType: post.postType.protocolValue,
            topicType: nil,
            feedType: nil,
            pathname: nil,
            hostname: nil,
            url: nil,
            description: post.markdown,
            summary: post.createdById,
            declaredLanguage: post.declaredLanguage,
            linguaRsDetectedLanguage: post.linguaRsDetectedLanguage
        )
    }

    private func hydratedRssFeedItemEntity(_ item: NativeRssFeedItemSummary) -> NativeGenericEntity {
        NativeGenericEntity(
            id: item.id,
            slug: nil,
            name: nil,
            title: item.title ?? item.data?.title ?? item.rssFeed?.title,
            username: nil,
            subject: nil,
            status: item.mediaType,
            postType: nil,
            topicType: nil,
            feedType: item.rssFeed?.feedType,
            pathname: nil,
            hostname: item.rssFeed?.hostname.map { .object($0.hostname) },
            url: item.link ?? item.data?.link ?? item.rssFeed?.rssFeedUrl.url,
            description: item.rssFeed?.title,
            summary: item.mediaType
        )
    }
}

struct NativeGenericEntityEnvelope: Decodable {
    let topic: NativeGenericEntity?
    let domain: NativeGenericEntity?
    let hostname: NativeGenericEntity?
    let url: NativeGenericEntity?
    let user: NativeGenericEntity?
    let community: NativeGenericEntity?
    let agent: NativeGenericEntity?
    let supportThread: NativeGenericEntity?
    let entity: NativeGenericEntity?

    var firstEntity: NativeGenericEntity? {
        topic ?? domain ?? hostname ?? url ?? user ?? community ?? agent ?? supportThread ?? entity
    }
}

extension String {
    var ifNotEmpty: String? {
        isEmpty ? nil : self
    }

    var routeLastSegment: String? {
        split(separator: "/").last.map(String.init)
    }
}
