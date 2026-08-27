import VouchaAPI
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func userPostPage(
        userId: String,
        filter: NativeUserProfileScope.PostFilter,
        after: String?,
        client: APIClient
    ) async throws -> NativeUserProfileCollectionPage {
        let response: UserProfilePostFeedResponse = try await client.send(.userPosts(
            userId: userId,
            postTypes: filter.queryValue,
            after: after
        ))
        let hydratedPosts = response.posts.mapValues { post in
            post.hydrated(
                renderedHtml: response.markdownToHtml?[post.id],
                metrics: response.postsMetrics[post.id],
                election: response.postElections[post.id],
                voteChoice: response.electionVotes?[post.id]?.choice
            )
        }
        return collectionPage(
            response.pageInfo,
            collection: .posts(response.results.compactMap { result in
                hydratedPosts[result.id].flatMap { NativeUserProfilePostRow.hydrated(
                    post: $0,
                    posts: hydratedPosts
                ) }
            })
        )
    }

    func userTopicPage(userId: String, after: String?, client: APIClient) async throws
        -> NativeUserProfileCollectionPage {
        let response: Page<Topic> = try await client.send(.userTopicsFollowing(userId: userId, after: after))
        return collectionPage(response.pageInfo, collection: .topics(response.results))
    }

    func userFriendPage(
        scope: NativeUserProfileScope,
        userId: String,
        after: String?,
        client: APIClient
    ) async throws -> NativeUserProfileCollectionPage {
        let endpoint = scope == .usersFollowing
            ? Endpoint.userFollowing(userId: userId, after: after, limit: 25)
            : Endpoint.userFollowers(userId: userId, after: after, limit: 25)
        let response: Page<PublicUser> = try await client.send(endpoint)
        return collectionPage(response.pageInfo, collection: .users(response.results))
    }

    func userSourcePage(
        userId: String,
        filter: NativeUserProfileScope.SourceFilter?,
        after: String?,
        client: APIClient
    ) async throws -> NativeUserProfileCollectionPage {
        let response: RssFeedSourceListResponse = try await client.send(.userRssFeeds(
            userId: userId,
            feedType: filter?.rawValue,
            after: after,
            limit: 25
        ))
        return collectionPage(response.pageInfo, collection: .sources(response.results))
    }

    func userCommunityPage(userId: String, after: String?, client: APIClient) async throws
        -> NativeUserProfileCollectionPage {
        let response: Page<Community> = try await client.send(.userCommunitiesMember(userId: userId, after: after))
        return collectionPage(response.pageInfo, collection: .communities(response.results))
    }

    private func collectionPage(
        _ pageInfo: Page<some Decodable & Sendable>.PageInfo,
        collection: NativeUserProfileCollection
    ) -> NativeUserProfileCollectionPage {
        .init(
            collection: collection,
            pageInfo: .init(hasNextPage: pageInfo.hasNextPage, endCursor: pageInfo.endCursor)
        )
    }
}
