import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

struct RssFeedSourcesResponse: Decodable {
    let results: [RssFeedSource]
    let topicElections: [String: TopicElection]
    let electionVotes: [String: ElectionVote]?
    let bookmarks: [String: [String: Bool]]?
}

/// View model for a two-scope source list: followed sources and all sources.
@Observable
@MainActor
public final class SourcesListViewModel {
    public var scope: SourceScope = .your

    public internal(set) var yourSources: [RssFeedSource] = []
    public internal(set) var allSources: [RssFeedSource] = []
    public internal(set) var state: LoadState = .idle
    public internal(set) var statusMessage: UiVerbatimText?
    public internal(set) var followedSourceIds: Set<String> = []
    public internal(set) var followedTopicIds: Set<String> = []
    public internal(set) var mutedSourceIds: Set<String> = []
    public internal(set) var mutedTopicIds: Set<String> = []
    public internal(set) var myVotesByTopicId: [String: ElectionVoteChoice] = [:]
    var serverVotesByTopicId: [String: ElectionVoteChoice] = [:]

    public var items: [RssFeedSource] {
        switch scope {
        case .your: yourSources
        case .all: allSources
        }
    }

    public func isFollowing(sourceId: String) -> Bool {
        followedSourceIds.contains(sourceId)
    }

    public func isFollowingTopic(topicId: String?) -> Bool {
        guard let topicId else { return false }
        return followedTopicIds.contains(topicId)
    }

    public func isMuted(sourceId: String) -> Bool {
        mutedSourceIds.contains(sourceId)
    }

    public func isMutedTopic(topicId: String?) -> Bool {
        guard let topicId else { return false }
        return mutedTopicIds.contains(topicId)
    }

    public var canToggleFollow: Bool {
        userId != nil
    }

    var yourLoaded = false
    var allLoaded = false
    var togglingSourceIds: Set<String> = []
    var togglingTopicIds: Set<String> = []
    var togglingMutedSourceIds: Set<String> = []
    var togglingMutedTopicIds: Set<String> = []
    var votingTopicIds: Set<String> = []
    var topicElectionsById: [String: TopicElection] = [:]

    struct SourceRollbackPosition {
        let previousSourceId: String?
        let nextSourceId: String?
        let fallbackIndex: Int?
    }

    let client: APIClient
    let userId: String?
    let feedType: String?

    public init(client: APIClient, userId: String?, feedType: String?) {
        self.client = client
        self.userId = userId
        self.feedType = feedType
    }

    public func load() async {
        await loadCurrentScope()
    }

    public func reload() async {
        switch scope {
        case .your:
            yourLoaded = false
            yourSources = []
        case .all:
            allLoaded = false
            allSources = []
        }
        await loadCurrentScope()
    }

    private func loadCurrentScope() async {
        statusMessage = nil
        let requestedScope = scope
        guard loadEmptyYourSourcesIfNeeded(requestedScope: requestedScope) else { return }
        guard shouldFetch(requestedScope) else { return }

        await fetchAndStore(requestedScope: requestedScope)
    }

    public func vote(topicId: String, choice: ElectionVoteChoice?) async {
        guard userId != nil else { return }
        guard !votingTopicIds.contains(topicId) else { return }
        votingTopicIds.insert(topicId)
        defer { votingTopicIds.remove(topicId) }

        let previous = myVotesByTopicId[topicId]
        let previousElection = topicElectionsById[topicId]
        reconcileVote(topicId: topicId, previous: previous, next: choice)
        if let choice {
            myVotesByTopicId[topicId] = choice
        } else {
            myVotesByTopicId.removeValue(forKey: topicId)
        }
        do {
            if let choice {
                let _: EmptyResponse = try await client.send(.voteTopic(topicId: topicId, choice: choice))
            } else {
                let _: EmptyResponse = try await client.send(.clearTopicVote(topicId: topicId))
            }
        } catch {
            if let previousElection {
                topicElectionsById[topicId] = previousElection
            } else {
                topicElectionsById.removeValue(forKey: topicId)
            }
            if let previous {
                myVotesByTopicId[topicId] = previous
            } else {
                myVotesByTopicId.removeValue(forKey: topicId)
            }
        }
    }

    private func reconcileVote(topicId: String, previous: ElectionVoteChoice?, next: ElectionVoteChoice?) {
        guard let election = topicElectionsById[topicId] else { return }
        let counts = ElectionVoteCountReconciler.reconcile(
            previous: previous ?? election.myVote,
            next: next,
            positive: election.votesCountUp,
            negative: election.votesCountDown
        )
        topicElectionsById[topicId] = .init(
            votesScoreNet: election.votesScoreNet,
            votesCountUp: counts.positive,
            votesCountDown: counts.negative,
            myVote: next
        )
    }

}
