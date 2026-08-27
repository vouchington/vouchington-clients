import VouchaAPI
import VouchaCore

extension NativeRouteSurfaceViewModel {
    func loadUserProfileCollection(
        scope: NativeUserProfileScope,
        userId: String,
        after: String?,
        client: APIClient
    ) async throws -> NativeUserProfileCollectionPage {
        switch scope {
        case let .posts(filter):
            try await userPostPage(userId: userId, filter: filter, after: after, client: client)
        case .topicsFollowing:
            try await userTopicPage(userId: userId, after: after, client: client)
        case .usersFollowing, .usersFollowers:
            try await userFriendPage(scope: scope, userId: userId, after: after, client: client)
        case let .sourcesFollowing(filter):
            try await userSourcePage(userId: userId, filter: filter, after: after, client: client)
        case .communitiesMember:
            try await userCommunityPage(userId: userId, after: after, client: client)
        case .overview:
            .init(collection: .none, pageInfo: .init(hasNextPage: false, endCursor: nil))
        }
    }

    public func loadMoreUserProfileRows() async {
        guard let client,
              let scope = userProfile.scope,
              let userId = userProfile.header?.user.id,
              let request = userProfile.pagination.beginNextPage()
        else { return }
        do {
            let page = try await loadUserProfileCollection(
                scope: scope,
                userId: userId,
                after: request.cursor,
                client: client
            )
            guard userProfile.pagination.isCurrent(request) else { return }
            guard userProfile.pagination.complete(
                request,
                items: page.collection.paginationItems,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            ) else { return }
            userProfile.collection = userProfile.collection.appending(page.collection)
        } catch let error as VouchaError {
            userProfile.pagination.fail(request, error: error)
        } catch {
            userProfile.pagination.fail(
                request,
                error: .unexpected(error.localizedDescription)
            )
        }
    }
}
