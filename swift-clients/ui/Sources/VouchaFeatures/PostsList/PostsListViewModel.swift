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
    public internal(set) var postEmbedsByPostId: [String: UrlEmbed] = [:]
    public internal(set) var hackerNewsReferencesByPostId: [String: HackerNewsReferences] = [:]
    let emailVerificationGate = EmailVerificationGatedMutation()

    var filterReloadTask: Task<Void, Never>?
    public var supportsHackerNewsFilter: Bool {
        feedType == "all"
    }

    public var hasHackerNewsReferences = false {
        didSet {
            guard hasHackerNewsReferences != oldValue else { return }
            if hasHackerNewsReferences, supportsHackerNewsFilter { timeRange = .lastYear }
            filterReloadTask = Task { await reload() }
        }
    }

    /// The backend flag; the filter stays hidden until a request reports references visible.
    public internal(set) var hackerNewsReferencesVisible = false
    /// Bumped per config request; only the newest request may publish its result.
    @ObservationIgnored var hackerNewsConfigGeneration = 0

    public var filter: PostFilter = .all {
        didSet {
            filterReloadTask = Task { await reload() }
        }
    }

    /// Feed has a fixed week; ordinary search (`all`) defaults to 30 days.
    var isFeed: Bool {
        feedType != "all"
    }

    /// Selected finite window. `nil` selects the ordinary-search 30-day default.
    public private(set) var timeRange: PostTimeRange?

    var inFlightVotePostIds: Set<String> = []
    var inFlightBookmarkKeys: Set<String> = []

    let client: APIClient
    /// The feed scope path segment, e.g. "any" (default), "follow_users".
    private let feedType: String

    public init(client: APIClient, feedType: String = "any", timeRange: PostTimeRange? = nil) {
        self.client = client
        self.feedType = feedType
        self.timeRange = feedType == "all" ? (timeRange == .last30Days ? nil : timeRange) : .lastWeek
    }

    /// Select a finite range (or `nil` for the default). A change drops the cursor and reloads.
    public func selectTimeRange(_ range: PostTimeRange?) async {
        guard !isFeed else { return }
        let range = hasHackerNewsReferences && supportsHackerNewsFilter
            ? (range == nil || range == .allTime ? .lastYear : range)
            : (range == .last30Days ? nil : range)
        guard range != timeRange else { return }
        timeRange = range
        await reload()
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
            let endpoint = feedEndpoint(after: request.cursor)
            let page: PostsPage = try await client.send(endpoint)
            guard pagination.isCurrent(request) else { return }
            let newItems = page.results.compactMap { result -> Post? in
                guard let id = result.entityId ?? result.id, let base = page.posts[id] else { return nil }
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
            guard pagination.complete(
                request,
                items: newItems,
                endCursor: page.pageInfo.endCursor,
                // Continue only with both signals; a short or cursorless page is terminal.
                hasNextPage: page.pageInfo.hasNextPage && page.pageInfo.endCursor != nil
            ) else { return }
            if request.cursor == nil {
                postEmbedsByPostId = page.postLinkEmbeds ?? [:]
                hackerNewsReferencesByPostId = page.postHackerNewsReferences ?? [:]
            } else {
                postEmbedsByPostId.merge(page.postLinkEmbeds ?? [:]) { _, new in new }
                hackerNewsReferencesByPostId.merge(page.postHackerNewsReferences ?? [:]) { _, new in new }
            }
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

    var timeRangeOptions: [PostTimeRange] {
        PostTimeRange.listOptions.filter { !hasHackerNewsReferences || $0 != .allTime }
    }

    var selectedTimeRange: PostTimeRange {
        if hasHackerNewsReferences, supportsHackerNewsFilter,
           timeRange == nil || timeRange == .allTime { return .lastYear }
        return timeRange ?? .last30Days
    }

    func feedEndpoint(after cursor: String?) -> Endpoint {
        if isFeed {
            return .posts(
                feedType: feedType,
                after: cursor,
                postTypes: filter.postTypes,
                timeRange: PostTimeRange.lastWeek.rawValue
            )
        }
        return .postsFeed(
            postTypes: filter.postTypes,
            timeRange: selectedTimeRange.rawValue,
            hasHackerNewsReferences: hasHackerNewsReferences && isHackerNewsFilterVisible ? true : nil,
            sort: "hot",
            after: cursor
        )
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
