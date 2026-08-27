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
        var removedItem: (offset: Int, item: RssFeedItem)?
        await toggleBookmark(
            rssFeedItemId: rssFeedItemId,
            predicate: "hide",
            isActive: { self.hiddenItemIds.contains($0) },
            activate: {
                self.hiddenItemIds.insert($0)
                if !wasHidden {
                    removedItem = self.removeItem(id: $0)
                }
            },
            deactivate: { self.hiddenItemIds.remove($0) },
            onRollbackActivate: { self.restoreItem(removedItem) }
        )
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
        inFlightBookmarkKeys.insert(key)
        defer { inFlightBookmarkKeys.remove(key) }

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
            if wasActive {
                activate(rssFeedItemId)
            } else {
                deactivate(rssFeedItemId)
                onRollbackActivate()
            }
        }
    }

    private func removeItem(id: String) -> (offset: Int, item: RssFeedItem)? {
        guard let index = items.firstIndex(where: { $0.id == id }) else { return nil }
        var updatedItems = items
        let item = updatedItems.remove(at: index)
        pagination.replaceItems(updatedItems)
        return (index, item)
    }

    private func restoreItem(_ snapshot: (offset: Int, item: RssFeedItem)?) {
        guard let snapshot, !items.contains(where: { $0.id == snapshot.item.id }) else { return }
        var updatedItems = items
        updatedItems.insert(snapshot.item, at: min(snapshot.offset, items.count))
        pagination.replaceItems(updatedItems)
    }
}
