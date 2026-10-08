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
        var newItems: [RssFeedItem] = []
        for result in page.results {
            let itemId = result.entityId ?? result.id
            guard let item = page.rssFeedItems[itemId], !hiddenItemIds.contains(itemId) else { continue }
            let thumbnailURL = VouchaURLResolver.absoluteString(
                for: page.rssFeedItemThumbnailUrl?[itemId],
                relativeTo: apiBaseURL
            )
            let feedItem = item.replacingThumbnailURL(thumbnailURL)
            if let storyId = result.storyId, result.deliveryType != "share" {
                storyIdsByItemId[itemId] = storyId
                if let existing = storyRelatedArticlesByStoryId[storyId], existing.primaryItemId != itemId {
                    mergeStoryPeer(feedItem, into: existing)
                    mergeStoryPreview(storyId: storyId, page: page, into: existing)
                    continue
                }
                if let existing = storyRelatedArticlesByStoryId[storyId] {
                    mergeStoryPreview(storyId: storyId, page: page, into: existing)
                }
                promoteStoryGroupIfPossible(
                    storyId: storyId, itemId: itemId, feedItem: feedItem, page: page, newItems: &newItems
                )
                if let group = storyRelatedArticlesByStoryId[storyId], group.primaryItemId != itemId {
                    continue
                }
            }
            newItems.append(feedItem)
        }
        applyPageSidecars(page)
        return newItems
    }

    private func promoteStoryGroupIfPossible(
        storyId: String,
        itemId: String,
        feedItem: RssFeedItem,
        page: RssFeedPage,
        newItems: inout [RssFeedItem]
    ) {
        guard storyRelatedArticlesByStoryId[storyId] == nil,
              let preview = page.storyMemberPages?[storyId] else { return }
        let peers = hydrateStoryItems(
            ids: preview.itemIds,
            items: page.rssFeedItems,
            thumbnails: page.rssFeedItemThumbnailUrl
        )
        guard preview.pageInfo.hasNextPage || peers.contains(where: { $0.id != itemId }) else { return }
        let displayedMembers = (items + newItems).filter {
            storyIdsByItemId[$0.id] == storyId
        }
        let primaryItemId = displayedMembers.first {
            !hiddenItemIds.contains($0.id) && page.bookmarks?[$0.id]?["hide"] != true
        }?.id ?? itemId
        storyRelatedArticlesByStoryId[storyId] = StoryRelatedArticles(
            primaryItemId: primaryItemId,
            items: displayedMembers.filter { $0.id != primaryItemId } + peers + [feedItem],
            pageInfo: preview.pageInfo
        )
        pagination.remove {
            $0.id != primaryItemId && storyIdsByItemId[$0.id] == storyId
        }
        newItems.removeAll {
            $0.id != primaryItemId && storyIdsByItemId[$0.id] == storyId
        }
    }

    private func mergeStoryPeer(_ item: RssFeedItem, into group: StoryRelatedArticles) {
        guard !group.pagination.items.contains(where: { $0.id == item.id }) else { return }
        group.pagination.invalidateRequestsPreservingPage()
        group.pagination.replaceItems(group.pagination.items + [item])
    }

    private func mergeStoryPreview(storyId: String, page: RssFeedPage, into group: StoryRelatedArticles) {
        guard let preview = page.storyMemberPages?[storyId] else { return }
        let peers = hydrateStoryItems(
            ids: preview.itemIds,
            items: page.rssFeedItems,
            thumbnails: page.rssFeedItemThumbnailUrl
        ).filter {
            $0.id != group.primaryItemId && !hiddenItemIds.contains($0.id) &&
                page.bookmarks?[$0.id]?["hide"] != true
        }
        let existingIds = Set(group.pagination.items.map(\.id))
        let newPeers = peers.filter { !existingIds.contains($0.id) }
        let hasNewContinuation = !group.pagination.hasMore && preview.pageInfo.hasNextPage
        guard !newPeers.isEmpty || hasNewContinuation else { return }
        group.pagination.invalidateRequestsPreservingPage()
        if !newPeers.isEmpty {
            group.pagination.replaceItems(group.pagination.items + newPeers)
        }
        if hasNewContinuation {
            group.pagination.restoreContinuation(endCursor: preview.pageInfo.endCursor, hasMore: true)
        }
    }

    private func applyPageSidecars(_ page: RssFeedPage) {
        itemElectionsById.merge(page.rssFeedItemElections ?? [:], uniquingKeysWith: { _, new in new })
        embedsByItemId.merge(page.rssFeedItemEmbeds ?? [:], uniquingKeysWith: { _, new in new })
        storyPostIdsByStoryId.merge(page.storyPostIds ?? [:], uniquingKeysWith: { _, new in new })
        applyBookmarkState(from: page.bookmarks)
        mergeVotes(page.electionVotes)
    }

    func mergeVotes(_ electionVotes: [String: ElectionVote]?) {
        guard let electionVotes else { return }
        for (itemId, vote) in electionVotes {
            serverVotesByItemId[itemId] = vote.choice
            if myVotesByItemId[itemId] == nil {
                myVotesByItemId[itemId] = vote.choice
            }
        }
    }

    func applyBookmarkState(from bookmarks: [String: [String: Bool]]?) {
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
