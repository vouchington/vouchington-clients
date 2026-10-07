import VouchaAPI
import VouchaModels

@MainActor
public extension RSSFeedListViewModel {
    func isSaved(rssFeedItemId: String) -> Bool {
        savedItemIds.contains(rssFeedItemId)
    }

    func isHidden(rssFeedItemId: String) -> Bool {
        hiddenItemIds.contains(rssFeedItemId)
    }

    func toggleSave(rssFeedItemId: String) async {
        await toggleBookmark(rssFeedItemId: rssFeedItemId, predicate: "save") {
            self.savedItemIds.contains($0)
        } activate: {
            self.savedItemIds.insert($0)
        } deactivate: {
            self.savedItemIds.remove($0)
        }
    }

    func toggleHide(rssFeedItemId: String) async {
        let wasHidden = hiddenItemIds.contains(rssFeedItemId)
        var removedItem: RemovedArticle?
        var removedStoryGroup: (id: String, group: StoryRelatedArticles)?
        await toggleBookmark(
            rssFeedItemId: rssFeedItemId,
            predicate: "hide",
            isActive: { self.hiddenItemIds.contains($0) },
            activate: {
                self.hiddenItemIds.insert($0)
                if !wasHidden {
                    if let storyId = self.storyIdsByItemId[$0],
                       let group = self.storyRelatedArticlesByStoryId[storyId],
                       group.primaryItemId == $0 {
                        removedStoryGroup = (storyId, group)
                        group.pagination.invalidateRequestsPreservingPage()
                        self.storyRelatedArticlesByStoryId.removeValue(forKey: storyId)
                    }
                    removedItem = self.removeItem(id: $0)
                }
            },
            deactivate: { self.hiddenItemIds.remove($0) },
            onRollbackActivate: {
                self.restoreItem(removedItem)
                if let removedStoryGroup {
                    self.restoreStoryGroup(removedStoryGroup)
                }
            }
        )
    }

    private func restoreStoryGroup(_ snapshot: (id: String, group: StoryRelatedArticles)) {
        let original = snapshot.group
        let replacement = storyRelatedArticlesByStoryId[snapshot.id]
        if replacement !== original {
            replacement?.pagination.invalidateRequestsPreservingPage()
        }
        original.pagination.invalidateRequestsPreservingPage()

        let interveningRows = pagination.items.filter {
            $0.showsStory && $0.item.id != original.primaryItemId &&
                storyIdsByItemId[$0.item.id] == snapshot.id
        }
        original.pagination.replaceItems(
            original.pagination.items + interveningRows.map(\.item) +
                (replacement?.pagination.items.filter { $0.id != original.primaryItemId } ?? [])
        )
        if !original.pagination.hasMore, let replacement, replacement.pagination.hasMore {
            original.pagination.restoreContinuation(
                endCursor: replacement.pagination.endCursor, hasMore: true
            )
        }
        pagination.remove {
            $0.showsStory && $0.item.id != original.primaryItemId &&
                storyIdsByItemId[$0.item.id] == snapshot.id
        }
        storyRelatedArticlesByStoryId[snapshot.id] = original
    }

    private func toggleBookmark(
        rssFeedItemId: String,
        predicate: String,
        isActive: @escaping (String) -> Bool,
        activate: @escaping (String) -> Void,
        deactivate: @escaping (String) -> Void,
        onRollbackActivate: @escaping () -> Void = {}
    ) async {
        let key = "\(rssFeedItemId)|\(predicate)"
        guard !inFlightBookmarkKeys.contains(key) else { return }
        let generation = bookmarkMutationGeneration
        inFlightBookmarkKeys.insert(key)
        defer {
            if generation == bookmarkMutationGeneration {
                inFlightBookmarkKeys.remove(key)
            }
        }

        let wasActive = isActive(rssFeedItemId)
        if wasActive {
            deactivate(rssFeedItemId)
        } else {
            activate(rssFeedItemId)
        }
        do {
            let endpoint = wasActive
                ? Endpoint.unbookmark(entityType: "rss_feed_item", entityId: rssFeedItemId, predicate: predicate)
                : Endpoint.bookmark(entityType: "rss_feed_item", entityId: rssFeedItemId, predicate: predicate)
            let _: EmptyResponse = try await client.send(endpoint)
        } catch {
            guard generation == bookmarkMutationGeneration else { return }
            if wasActive {
                activate(rssFeedItemId)
            } else {
                deactivate(rssFeedItemId)
                onRollbackActivate()
            }
        }
    }

    private struct RemovedPeer {
        let group: StoryRelatedArticles
        let offset: Int
        let item: RssFeedItem
    }

    private struct RemovedArticle {
        let peers: [RemovedPeer]
        let feedRows: [(offset: Int, row: RssFeedListRow)]
    }

    private func removeItem(id: String) -> RemovedArticle? {
        var removedPeers: [RemovedPeer] = []
        for group in storyRelatedArticlesByStoryId.values {
            guard let index = group.pagination.items.firstIndex(where: { $0.id == id }) else { continue }
            var peers = group.pagination.items
            let peer = peers.remove(at: index)
            group.pagination.replaceItems(peers)
            removedPeers.append(RemovedPeer(group: group, offset: index, item: peer))
        }
        let matches = pagination.items.enumerated().filter { $0.element.item.id == id }
        guard !removedPeers.isEmpty || !matches.isEmpty else { return nil }
        if !matches.isEmpty {
            pagination.replaceItems(pagination.items.filter { $0.item.id != id })
        }
        return RemovedArticle(peers: removedPeers, feedRows: matches.map { ($0.offset, $0.element) })
    }

    private func restoreItem(_ snapshot: RemovedArticle?) {
        guard let snapshot else { return }
        for peer in snapshot.peers {
            let isAttached = storyRelatedArticlesByStoryId.values.contains { $0 === peer.group }
            let isDetachedForPrimaryHide = inFlightBookmarkKeys.contains("\(peer.group.primaryItemId)|hide") &&
                storyIdsByItemId[peer.group.primaryItemId] != nil
            guard isAttached || isDetachedForPrimaryHide,
                  !peer.group.pagination.items.contains(where: { $0.id == peer.item.id }) else { continue }
            var peers = peer.group.pagination.items
            peers.insert(peer.item, at: min(peer.offset, peers.count))
            peer.group.pagination.replaceItems(peers)
        }
        let present = Set(pagination.items.map(\.deliveryId))
        let missing = snapshot.feedRows.filter { !present.contains($0.row.deliveryId) }
        guard !missing.isEmpty else { return }
        var updated = pagination.items
        for (offset, row) in missing {
            updated.insert(row, at: min(offset, updated.count))
        }
        pagination.replaceItems(updated)
    }
}
