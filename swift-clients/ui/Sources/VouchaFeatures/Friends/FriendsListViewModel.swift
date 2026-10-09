import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

@Observable
@MainActor
public final class FriendsListViewModel {
    public var tab: FriendsTab = .following

    var followingPagination = CursorPaginationState<PublicUser>()
    var followersPagination = CursorPaginationState<PublicUser>()

    public var following: [PublicUser] {
        followingPagination.items
    }

    public var followers: [PublicUser] {
        followersPagination.items
    }

    public var state: LoadState {
        pagination(for: tab).state
    }

    public internal(set) var followingIds: Set<String> = []

    public var hasMore: Bool {
        let pagination = pagination(for: tab)
        return pagination.hasMore && (pagination.endCursor != nil || pagination.lastError != nil)
    }

    public var isLoadingMore: Bool {
        pagination(for: tab).isLoading && pagination(for: tab).hasLoadedPage
    }

    public var items: [PublicUser] {
        switch tab {
        case .following: following
        case .followers: followers
        }
    }

    public var hasPaginationError: Bool {
        pagination(for: tab).lastError != nil
    }

    var inFlightFollows: Set<String> = []

    let client: APIClient
    let userId: String?
    private let config: AppConfig

    public init(client: APIClient, userId: String?, config: AppConfig) {
        self.client = client
        self.userId = userId
        self.config = config
    }

    public func avatarURL(for user: PublicUser) -> String? {
        config.imageURL(for: user.profileImagePlacement)
    }

    public func isFollowing(userId: String) -> Bool {
        followingIds.contains(userId)
    }

    public func canToggleFollow(userId: String) -> Bool {
        guard self.userId != nil else { return false }
        guard !inFlightFollows.contains(userId) else { return false }
        guard tab == .following else { return true }
        return followingIds.contains(userId) || isFollowingListComplete
    }

    public func load() async {
        await loadTab(append: false)
    }

    public func loadMore() async {
        await loadTab(append: true)
    }

    public func reload() async {
        switch tab {
        case .following:
            followingPagination.reset()
        case .followers:
            followersPagination.reset()
        }
        await loadTab(append: false)
    }

    func pagination(for tab: FriendsTab) -> CursorPaginationState<PublicUser> {
        switch tab {
        case .following: followingPagination
        case .followers: followersPagination
        }
    }

    private var isFollowingListComplete: Bool {
        followingPagination.hasLoadedPage && !followingPagination.hasMore
    }
}
