import Foundation
import Observation
import VouchaAPI
import VouchaModels

@Observable
@MainActor
public final class RSSFeedListViewModel {
    var pagination = CursorPaginationState<RssFeedItem>()
    private var actionState: LoadState?
    public var items: [RssFeedItem] {
        pagination.items
    }

    public internal(set) var state: LoadState {
        get { actionState ?? pagination.state }
        set { actionState = newValue }
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

    public internal(set) var myVotesByItemId: [String: ElectionVoteChoice] = [:]
    public internal(set) var savedItemIds: Set<String> = []
    public internal(set) var hiddenItemIds: Set<String> = []
    let emailVerificationGate = EmailVerificationGatedMutation()
    let contributionIdentity = ContributionRequestIdentity()
    var serverVotesByItemId: [String: ElectionVoteChoice] = [:]
    public let apiBaseURL: URL

    public var isLoading: Bool {
        pagination.isLoading
    }

    public let contentType: ContentType
    public let feedSource: FeedSource

    var itemElectionsById: [String: RssFeedItemElection] = [:]
    var embedsByItemId: [String: UrlEmbed] = [:]
    private var votingItemIds: Set<String> = []
    var inFlightBookmarkKeys: Set<String> = []
    var bookmarkMutationGeneration = 0
    var storyIdsByItemId: [String: String] = [:]
    var storyRelatedArticlesByStoryId: [String: StoryRelatedArticles] = [:]
    var storyPostIdsByStoryId: [String: String] = [:]
    var storyDiscussionDestinationsByStoryId: [String: StoryDiscussionDestination] = [:]
    var fallbackStoryDestinationsByStoryId: [String: StoryDiscussionDestination] = [:]
    var startedDiscussionStoryIds: Set<String> = []
    var startingDiscussionStoryIds: Set<String> = []

    let client: APIClient

    public init(client: APIClient, contentType: ContentType, feedSource: FeedSource = .your) {
        self.client = client
        self.contentType = contentType
        self.feedSource = feedSource
        apiBaseURL = client.baseURL
    }

    /// Load the first page if idle and there is more to load.
    public func load() async {
        await loadIfNeeded()
    }

    /// Fetch the next page and append results.
    public func loadNextPage() async {
        await loadNextFeedPage()
    }

    /// Reset all pagination state (safe to call before reload).
    public func reset() {
        bookmarkMutationGeneration += 1
        pagination.reset()
        actionState = nil
        itemElectionsById = [:]
        embedsByItemId = [:]
        serverVotesByItemId = [:]
        myVotesByItemId = [:]
        savedItemIds = []
        hiddenItemIds = []
        inFlightBookmarkKeys = []
        storyIdsByItemId = [:]
        storyRelatedArticlesByStoryId = [:]
        storyPostIdsByStoryId = [:]
        storyDiscussionDestinationsByStoryId = [:]
        startedDiscussionStoryIds = []
    }

    /// Clear and reload from the first page.
    public func reload() async {
        reset()
        await loadNextPage()
    }

    public func election(for itemId: String) -> RssFeedItemElection? {
        guard let election = itemElectionsById[itemId] else { return nil }
        return RssFeedItemElection(
            votesScoreNet: election.votesScoreNet,
            votesCountUp: election.votesCountUp,
            votesCountDown: election.votesCountDown,
            myVote: serverVotesByItemId[itemId] ?? election.myVote
        )
    }

    public func vote(rssFeedItemId: String, choice: ElectionVoteChoice?) async {
        guard !votingItemIds.contains(rssFeedItemId) else { return }
        votingItemIds.insert(rssFeedItemId)
        defer { votingItemIds.remove(rssFeedItemId) }

        let previous = myVotesByItemId[rssFeedItemId]
        let previousElection = itemElectionsById[rssFeedItemId]
        reconcileVote(rssFeedItemId: rssFeedItemId, previous: previous, next: choice)
        if let choice {
            myVotesByItemId[rssFeedItemId] = choice
        } else {
            myVotesByItemId.removeValue(forKey: rssFeedItemId)
        }
        let _: EmptyResponse? = try? await emailVerificationGate.perform(rollbackOnFailure: {
            if let previousElection {
                itemElectionsById[rssFeedItemId] = previousElection
            } else {
                itemElectionsById.removeValue(forKey: rssFeedItemId)
            }
            if let previous {
                myVotesByItemId[rssFeedItemId] = previous
            } else {
                myVotesByItemId.removeValue(forKey: rssFeedItemId)
            }
        }, {
            if let choice {
                try await client.send(.voteRssFeedItem(rssFeedItemId: rssFeedItemId, choice: choice))
            } else {
                try await client.send(.clearRssFeedItemVote(rssFeedItemId: rssFeedItemId))
            }
        })
    }

    private func reconcileVote(rssFeedItemId: String, previous: ElectionVoteChoice?, next: ElectionVoteChoice?) {
        guard let election = itemElectionsById[rssFeedItemId] else { return }
        let counts = ElectionVoteCountReconciler.reconcile(
            previous: previous ?? election.myVote,
            next: next,
            positive: election.votesCountUp,
            negative: election.votesCountDown
        )
        itemElectionsById[rssFeedItemId] = .init(
            votesScoreNet: election.votesScoreNet,
            votesCountUp: counts.positive,
            votesCountDown: counts.negative,
            myVote: next
        )
    }

}
