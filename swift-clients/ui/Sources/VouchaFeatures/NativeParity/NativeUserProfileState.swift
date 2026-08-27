import VouchaAPI
import VouchaCore
import VouchaModels

struct NativeUserProfileState {
    var scope: NativeUserProfileScope?
    var header: UserProfileResponse?
    var collection: NativeUserProfileCollection = .none
    var pagination = CursorPaginationState<NativeUserProfilePaginationItem>()
    var trustChoice: ElectionVoteChoice?
    var trustContext: UserTrustContext?
    var trustVoteInFlight = false

    var appendError: VouchaError? {
        pagination.lastError
    }

    var isLoadingMore: Bool {
        pagination.hasLoadedPage && pagination.isLoading
    }
}

struct NativeUserProfilePaginationItem: Identifiable {
    let id: String
}

struct NativeUserProfilePageInfo {
    let hasNextPage: Bool
    let endCursor: String?
}

struct NativeUserProfileCollectionPage {
    let collection: NativeUserProfileCollection
    let pageInfo: NativeUserProfilePageInfo
}

enum NativeUserProfileCollection {
    case none
    case posts([NativeUserProfilePostRow])
    case users([PublicUser])
    case topics([Topic])
    case sources([RssFeedSource])
    case communities([Community])

    var isEmpty: Bool {
        switch self {
        case .none: true
        case let .posts(items): items.isEmpty
        case let .users(items): items.isEmpty
        case let .topics(items): items.isEmpty
        case let .sources(items): items.isEmpty
        case let .communities(items): items.isEmpty
        }
    }

    var count: Int {
        switch self {
        case .none: 0
        case let .posts(items): items.count
        case let .users(items): items.count
        case let .topics(items): items.count
        case let .sources(items): items.count
        case let .communities(items): items.count
        }
    }

    var paginationItems: [NativeUserProfilePaginationItem] {
        switch self {
        case .none: []
        case let .posts(items): items.map { .init(id: $0.id) }
        case let .users(items): items.map { .init(id: $0.id) }
        case let .topics(items): items.map { .init(id: $0.id) }
        case let .sources(items): items.map { .init(id: $0.id) }
        case let .communities(items): items.map { .init(id: $0.id) }
        }
    }

    func appending(_ next: NativeUserProfileCollection) -> NativeUserProfileCollection {
        switch (self, next) {
        case let (.posts(current), .posts(items)): .posts(Self.merge(current, items))
        case let (.users(current), .users(items)): .users(Self.merge(current, items))
        case let (.topics(current), .topics(items)): .topics(Self.merge(current, items))
        case let (.sources(current), .sources(items)): .sources(Self.merge(current, items))
        case let (.communities(current), .communities(items)): .communities(Self.merge(current, items))
        case (.none, _): next
        default: next
        }
    }

    private static func merge<T: Identifiable>(_ current: [T], _ next: [T]) -> [T] where T.ID: Hashable {
        var seen = Set(current.map(\.id))
        return current + next.filter { seen.insert($0.id).inserted }
    }
}

enum NativeUserProfilePostRow: Identifiable {
    case root(Post)
    case comment(comment: Post, root: Post)

    var id: String {
        post.id
    }

    var post: Post {
        switch self {
        case let .root(post): post
        case let .comment(comment, _): comment
        }
    }

    static func hydrated(post: Post, posts: [String: Post]) -> NativeUserProfilePostRow? {
        guard post.postType == .comment else { return .root(post) }
        guard let rootId = post.rootId,
              let root = posts[rootId],
              root.postType != .comment
        else { return nil }
        return .comment(comment: post, root: root)
    }
}
