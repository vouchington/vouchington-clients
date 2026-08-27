import Foundation
import VouchaCore
import VouchaModels

extension SourcesListViewModel {
    func loadEmptyYourSourcesIfNeeded(requestedScope: SourceScope) -> Bool {
        guard requestedScope == .your, userId == nil else { return true }

        yourSources = []
        followedSourceIds = []
        followedTopicIds = []
        mutedSourceIds = []
        mutedTopicIds = []
        yourLoaded = true
        if requestedScope == scope {
            state = .loaded
        }
        return false
    }

    func shouldFetch(_ requestedScope: SourceScope) -> Bool {
        let isLoaded: Bool = switch requestedScope {
        case .your: yourLoaded
        case .all: allLoaded
        }
        guard !isLoaded else {
            state = .loaded
            return false
        }

        state = .loading
        return true
    }

    func fetchAndStore(requestedScope: SourceScope) async {
        do {
            let response = try await fetchSources(scope: requestedScope)
            storeSources(response, requestedScope: requestedScope)
        } catch is CancellationError {
            return
        } catch let error as VouchaError {
            guard !Self.isURLCancellation(error) else { return }
            if requestedScope == scope {
                state = .error(error)
            }
        } catch {
            if requestedScope == scope {
                state = .error(.api(statusCode: 0, preconditionCode: nil))
            }
        }
    }

    func fetchSources(scope: SourceScope) async throws -> RssFeedSourcesResponse {
        switch scope {
        case .your:
            guard let userId else {
                return RssFeedSourcesResponse(results: [], topicElections: [:], electionVotes: nil, bookmarks: nil)
            }
            let response: RssFeedSourcesResponse = try await client.send(.userRssFeeds(
                userId: userId,
                feedType: feedType
            ))
            return response

        case .all:
            let response: RssFeedSourcesResponse = try await client.send(.allRssFeeds(feedType: feedType))
            return response
        }
    }

    func storeSources(_ response: RssFeedSourcesResponse, requestedScope: SourceScope) {
        let sources = response.results
        topicElectionsById.merge(response.topicElections, uniquingKeysWith: { _, new in new })
        if let electionVotes = response.electionVotes {
            for (topicId, vote) in electionVotes {
                serverVotesByTopicId[topicId] = vote.choice
                if !votingTopicIds.contains(topicId) {
                    myVotesByTopicId[topicId] = vote.choice
                }
            }
        }
        switch requestedScope {
        case .your:
            yourSources = sources
            yourLoaded = true
            followedSourceIds = Set(sources.map(\.id))
            if let bookmarks = response.bookmarks {
                followedTopicIds.formUnion(Self.followedTopicIds(from: bookmarks, sources: sources))
                mutedSourceIds.formUnion(Self.mutedSourceIds(from: bookmarks))
                mutedTopicIds.formUnion(Self.mutedTopicIds(from: bookmarks, sources: sources))
            }
        case .all:
            allSources = sources
            allLoaded = true
            if let bookmarks = response.bookmarks {
                followedSourceIds.formUnion(Self.followedSourceIds(from: bookmarks))
                followedTopicIds.formUnion(Self.followedTopicIds(from: bookmarks, sources: sources))
                replaceLoadedMutedState(bookmarks: bookmarks, sources: sources)
            }
        }
        if requestedScope == scope {
            state = .loaded
        }
    }

    private func replaceLoadedMutedState(bookmarks: [String: [String: Bool]], sources: [RssFeedSource]) {
        let loadedSourceIds = Set(sources.map(\.id))
        let loadedTopicIds = Set(sources.compactMap(\.topic?.id))
        mutedSourceIds.subtract(loadedSourceIds)
        mutedTopicIds.subtract(loadedTopicIds)
        mutedSourceIds.formUnion(Self.mutedSourceIds(from: bookmarks).intersection(loadedSourceIds))
        mutedTopicIds.formUnion(Self.mutedTopicIds(from: bookmarks, sources: sources))
    }

    func source(with sourceId: String) -> RssFeedSource? {
        yourSources.first(where: { $0.id == sourceId }) ?? allSources.first(where: { $0.id == sourceId })
    }

    static func followedSourceIds(from bookmarks: [String: [String: Bool]]) -> Set<String> {
        Set(bookmarks.compactMap { sourceId, predicates in
            predicates["follow"] == true ? sourceId : nil
        })
    }

    static func mutedSourceIds(from bookmarks: [String: [String: Bool]]) -> Set<String> {
        Set(bookmarks.compactMap { sourceId, predicates in
            predicates["mute"] == true ? sourceId : nil
        })
    }

    static func followedTopicIds(
        from bookmarks: [String: [String: Bool]],
        sources: [RssFeedSource]
    ) -> Set<String> {
        let sourceTopicIds = Set(sources.compactMap(\.topic?.id))
        return Set(bookmarks.compactMap { entityId, predicates in
            predicates["follow"] == true && sourceTopicIds.contains(entityId) ? entityId : nil
        })
    }

    static func mutedTopicIds(
        from bookmarks: [String: [String: Bool]],
        sources: [RssFeedSource]
    ) -> Set<String> {
        let sourceTopicIds = Set(sources.compactMap(\.topic?.id))
        return Set(bookmarks.compactMap { entityId, predicates in
            predicates["mute"] == true && sourceTopicIds.contains(entityId) ? entityId : nil
        })
    }

    static func isURLCancellation(_ error: VouchaError) -> Bool {
        guard case let .network(urlError) = error else { return false }
        return urlError.code == .cancelled
    }
}
