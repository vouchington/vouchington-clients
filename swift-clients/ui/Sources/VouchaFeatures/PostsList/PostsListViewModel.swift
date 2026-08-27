import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

// MARK: - ViewModel

@Observable
@MainActor
public final class PostsListViewModel {
    var pagination = CursorPaginationState<Post>()
    public var items: [Post] {
        pagination.items
    }

    public var state: LoadState {
        pagination.state
    }

    public var hasMore: Bool {
        pagination.hasMore
    }

    public var hasPaginationError: Bool {
        pagination.lastError != nil
    }

    public var canAutomaticallyLoad: Bool {
        pagination.canAutomaticallyLoad
    }

    public internal(set) var myVotesByPostId: [String: ElectionVoteChoice] = [:]
    public internal(set) var savedPostIds: Set<String> = []
    public internal(set) var hiddenPostIds: Set<String> = []
    let emailVerificationGate = EmailVerificationGatedMutation()

    public var filter: PostFilter = .all {
        didSet {
            Task { await reload() }
        }
    }

    var inFlightVotePostIds: Set<String> = []
    var inFlightBookmarkKeys: Set<String> = []

    let client: APIClient
    /// The feed scope path segment, e.g. "any" (default), "follow_users".
    private let feedType: String

    public init(client: APIClient, feedType: String = "any") {
        self.client = client
        self.feedType = feedType
    }

    /// Load the first page if idle and there is more to load.
    public func load() async {
        guard let request = pagination.beginInitialPageIfNeeded() else { return }
        await loadPage(request)
    }

    /// Fetch the next page and append results.
    public func loadNextPage() async {
        guard let request = pagination.beginNextPage() else { return }
        await loadPage(request)
    }

    private func loadPage(_ request: CursorPageRequest) async {
        do {
            let endpoint = Endpoint.posts(feedType: feedType, after: request.cursor, postTypes: filter.postTypes)
            let page: PostsPage = try await client.send(endpoint)
            guard pagination.isCurrent(request) else { return }
            let newItems = page.results.compactMap { result -> Post? in
                guard let base = page.posts[result.entityId] else { return nil }
                let post = base.hydrated(
                    renderedHtml: page.markdownToHtml?[base.id],
                    metrics: page.postsMetrics[base.id],
                    election: page.postElections[base.id],
                    voteChoice: page.electionVotes?[base.id]?.choice
                )
                if let myVote = post.election?.myVote {
                    myVotesByPostId[base.id] = myVote
                }
                applyBookmarkState(for: base.id, from: page.bookmarks)
                return post
            }
            pagination.complete(
                request,
                items: newItems,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            if Task.isCancelled {
                pagination.cancel(request)
            } else {
                pagination.fail(request, error: error)
            }
        } catch {
            if Task.isCancelled {
                pagination.cancel(request)
            } else {
                pagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            }
        }
    }

    /// Reset all pagination state.
    public func reset() {
        pagination.reset()
        myVotesByPostId = [:]
        savedPostIds = []
        hiddenPostIds = []
        inFlightVotePostIds = []
        inFlightBookmarkKeys = []
    }

    /// Clear and reload from the first page.
    public func reload() async {
        reset()
        await loadNextPage()
    }

    private func applyBookmarkState(for postId: String, from bookmarks: [String: [String: Bool]]?) {
        guard let predicates = bookmarks?[postId] else { return }
        if predicates["save"] == true {
            savedPostIds.insert(postId)
        }
        if predicates["hide"] == true {
            hiddenPostIds.insert(postId)
        }
    }
}
