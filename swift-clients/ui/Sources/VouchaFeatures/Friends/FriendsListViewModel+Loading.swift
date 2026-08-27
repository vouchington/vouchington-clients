import VouchaAPI
import VouchaCore
import VouchaModels

extension FriendsListViewModel {
    func loadTab(append: Bool) async {
        let requestedTab = tab
        guard let request = beginRequest(for: requestedTab, append: append) else { return }
        guard let userId else {
            complete(request, for: requestedTab, users: [], pageInfo: .init(hasNextPage: false, endCursor: nil))
            return
        }

        do {
            let response: FriendsListUsersResponse = try await client.send(
                usersEndpoint(tab: requestedTab, userId: userId, after: request.cursor)
            )
            complete(request, for: requestedTab, users: response.results, pageInfo: response.pageInfo)
        } catch is CancellationError {
            cancel(request, for: requestedTab)
        } catch let error as VouchaError {
            if Self.isURLCancellation(error) {
                cancel(request, for: requestedTab)
            } else {
                fail(request, for: requestedTab, error: error)
            }
        } catch {
            fail(request, for: requestedTab, error: .api(statusCode: 0, preconditionCode: nil))
        }
    }

    private func beginRequest(for tab: FriendsTab, append: Bool) -> CursorPageRequest? {
        switch tab {
        case .following:
            if append {
                followingPagination.beginNextPage()
            } else {
                followingPagination.beginInitialPageIfNeeded()
            }
        case .followers:
            if append {
                followersPagination.beginNextPage()
            } else {
                followersPagination.beginInitialPageIfNeeded()
            }
        }
    }

    private func complete(
        _ request: CursorPageRequest,
        for tab: FriendsTab,
        users: [PublicUser],
        pageInfo: FriendsListUsersResponse.PageInfo
    ) {
        switch tab {
        case .following:
            guard followingPagination.complete(
                request,
                items: users,
                endCursor: pageInfo.endCursor,
                hasNextPage: pageInfo.hasNextPage
            ) else { return }
            followingIds.formUnion(users.map(\.id))
        case .followers:
            followersPagination.complete(
                request,
                items: users,
                endCursor: pageInfo.endCursor,
                hasNextPage: pageInfo.hasNextPage
            )
        }
    }

    private func fail(_ request: CursorPageRequest, for tab: FriendsTab, error: VouchaError) {
        switch tab {
        case .following: followingPagination.fail(request, error: error)
        case .followers: followersPagination.fail(request, error: error)
        }
    }

    private func cancel(_ request: CursorPageRequest, for tab: FriendsTab) {
        switch tab {
        case .following: followingPagination.cancel(request)
        case .followers: followersPagination.cancel(request)
        }
    }

    private func usersEndpoint(tab: FriendsTab, userId: String, after: String?) -> Endpoint {
        switch tab {
        case .following: .userFollowing(userId: userId, after: after)
        case .followers: .userFollowers(userId: userId, after: after)
        }
    }

    private static func isURLCancellation(_ error: VouchaError) -> Bool {
        guard case let .network(urlError) = error else { return false }
        return urlError.code == .cancelled
    }
}
