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

        let interveningRows = items.filter {
            $0.id != original.primaryItemId && storyIdsByItemId[$0.id] == snapshot.id
        }
        original.pagination.replaceItems(
            original.pagination.items + interveningRows +
                (replacement?.pagination.items.filter { $0.id != original.primaryItemId } ?? [])
        )
        if !original.pagination.hasMore, let replacement, replacement.pagination.hasMore {
            original.pagination.restoreContinuation(
                endCursor: replacement.pagination.endCursor, hasMore: true
            )
        }
        pagination.remove {
            $0.id != original.primaryItemId && storyIdsByItemId[$0.id] == snapshot.id
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

    private struct RemovedArticle {
        let group: StoryRelatedArticles?
        let offset: Int
        let item: RssFeedItem
    }

    private func removeItem(id: String) -> RemovedArticle? {
        if let group = storyRelatedArticlesByStoryId.values
            .first(where: { $0.pagination.items.contains { $0.id == id } }),
            let index = group.pagination.items.firstIndex(where: { $0.id == id }) {
            var peers = group.pagination.items
            let peer = peers.remove(at: index)
            group.pagination.replaceItems(peers)
            return RemovedArticle(group: group, offset: index, item: peer)
        }
        guard let index = items.firstIndex(where: { $0.id == id }) else { return nil }
        var updatedItems = items
        let item = updatedItems.remove(at: index)
        pagination.replaceItems(updatedItems)
        return RemovedArticle(group: nil, offset: index, item: item)
    }

    private func restoreItem(_ snapshot: RemovedArticle?) {
        guard let snapshot else { return }
        if let group = snapshot.group {
            let isAttached = storyRelatedArticlesByStoryId.values.contains { $0 === group }
            let isDetachedForPrimaryHide = inFlightBookmarkKeys.contains("\(group.primaryItemId)|hide") &&
                storyIdsByItemId[group.primaryItemId] != nil
            guard isAttached || isDetachedForPrimaryHide,
                  !group.pagination.items.contains(where: { $0.id == snapshot.item.id }) else { return }
            var peers = group.pagination.items
            peers.insert(snapshot.item, at: min(snapshot.offset, peers.count))
            group.pagination.replaceItems(peers)
            return
        }
        guard !items.contains(where: { $0.id == snapshot.item.id }) else { return }
        var updatedItems = items
        updatedItems.insert(snapshot.item, at: min(snapshot.offset, items.count))
        pagination.replaceItems(updatedItems)
    }
}
