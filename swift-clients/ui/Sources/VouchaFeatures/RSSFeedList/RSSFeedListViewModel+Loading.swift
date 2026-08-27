import VouchaAPI
import VouchaCore
import VouchaModels

extension RSSFeedListViewModel {
    func loadIfNeeded() async {
        guard let request = pagination.beginInitialPageIfNeeded() else { return }
        await loadFeedPage(request)
    }

    func loadNextFeedPage() async {
        guard let request = pagination.beginNextPage() else { return }
        await loadFeedPage(request)
    }

    private func loadFeedPage(_ request: CursorPageRequest) async {
        do {
            let page: RssFeedPage = try await client.send(feedEndpoint(after: request.cursor))
            guard pagination.isCurrent(request) else { return }
            let newItems = mergeSidecarsAndBuildItems(page: page)
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

    private func feedEndpoint(after cursor: String?) -> Endpoint {
        switch feedSource {
        case .your:
            Endpoint.rssFeedItems(
                feedType: contentType.feedType,
                after: cursor,
                mediaType: contentType.mediaType
            )
        case .all:
            Endpoint.allRssFeedItems(
                after: cursor,
                mediaType: contentType.mediaType
            )
        }
    }

    private func mergeSidecarsAndBuildItems(page: RssFeedPage) -> [RssFeedItem] {
        let newItems = page.results.compactMap { result -> RssFeedItem? in
            let itemId = result.entityId ?? result.id
            guard let item = page.rssFeedItems[itemId] else { return nil }
            if let storyId = result.storyId {
                storyIdsByItemId[itemId] = storyId
            }
            let thumbnailURL = VouchaURLResolver.absoluteString(
                for: page.rssFeedItemThumbnailUrl?[itemId],
                relativeTo: apiBaseURL
            )
            return item.replacingThumbnailURL(thumbnailURL)
        }
        itemElectionsById.merge(page.rssFeedItemElections ?? [:], uniquingKeysWith: { _, new in new })
        storyMemberIdsByStoryId.merge(page.storyMemberIds ?? [:], uniquingKeysWith: { _, new in new })
        storyPostIdsByStoryId.merge(page.storyPostIds ?? [:], uniquingKeysWith: { _, new in new })
        applyBookmarkState(from: page.bookmarks)
        mergeVotes(page.electionVotes)
        return newItems
    }

    private func mergeVotes(_ electionVotes: [String: ElectionVote]?) {
        guard let electionVotes else { return }
        for (itemId, vote) in electionVotes {
            serverVotesByItemId[itemId] = vote.choice
            if myVotesByItemId[itemId] == nil {
                myVotesByItemId[itemId] = vote.choice
            }
        }
    }

    private func applyBookmarkState(from bookmarks: [String: [String: Bool]]?) {
        guard let bookmarks else { return }
        for (itemId, predicates) in bookmarks {
            if predicates["save"] == true {
                savedItemIds.insert(itemId)
            }
            if predicates["hide"] == true {
                hiddenItemIds.insert(itemId)
            }
        }
    }
}
