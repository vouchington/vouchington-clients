import VouchaAPI
import VouchaModels

public extension PostsListViewModel {
    var isLoading: Bool {
        pagination.isLoading
    }
}

@MainActor
public extension PostsListViewModel {
    func isSaved(postId: String) -> Bool {
        savedPostIds.contains(postId)
    }

    func isHidden(postId: String) -> Bool {
        hiddenPostIds.contains(postId)
    }

    func toggleSave(postId: String) async {
        await toggleBookmark(
            postId: postId,
            predicate: "save",
            isActive: { self.savedPostIds.contains($0) },
            activate: { self.savedPostIds.insert($0) },
            deactivate: { self.savedPostIds.remove($0) }
        )
    }

    func toggleHide(postId: String) async {
        let wasHidden = hiddenPostIds.contains(postId)
        var removedPost: (offset: Int, post: Post)?
        await toggleBookmark(
            postId: postId,
            predicate: "hide",
            isActive: { self.hiddenPostIds.contains($0) },
            activate: {
                self.hiddenPostIds.insert($0)
                if !wasHidden {
                    removedPost = self.removePost(id: $0)
                }
            },
            deactivate: { self.hiddenPostIds.remove($0) },
            onRollbackActivate: { self.restorePost(removedPost) }
        )
    }

    private func toggleBookmark(
        postId: String,
        predicate: String,
        isActive: @escaping (String) -> Bool,
        activate: @escaping (String) -> Void,
        deactivate: @escaping (String) -> Void,
        onRollbackActivate: @escaping () -> Void = {}
    ) async {
        let key = "\(postId)|\(predicate)"
        guard !inFlightBookmarkKeys.contains(key) else { return }
        inFlightBookmarkKeys.insert(key)
        defer { inFlightBookmarkKeys.remove(key) }

        let wasActive = isActive(postId)
        if wasActive {
            deactivate(postId)
        } else {
            activate(postId)
        }
        do {
            let endpoint = wasActive
                ? Endpoint.unbookmark(entityType: "post", entityId: postId, predicate: predicate)
                : Endpoint.bookmark(entityType: "post", entityId: postId, predicate: predicate)
            let _: EmptyResponse = try await client.send(endpoint)
        } catch {
            if wasActive {
                activate(postId)
            } else {
                deactivate(postId)
                onRollbackActivate()
            }
        }
    }

    private func removePost(id: String) -> (offset: Int, post: Post)? {
        guard let index = items.firstIndex(where: { $0.id == id }) else { return nil }
        var updatedItems = items
        let post = updatedItems.remove(at: index)
        pagination.replaceItems(updatedItems)
        return (index, post)
    }

    private func restorePost(_ snapshot: (offset: Int, post: Post)?) {
        guard let snapshot, !items.contains(where: { $0.id == snapshot.post.id }) else { return }
        var updatedItems = items
        updatedItems.insert(snapshot.post, at: min(snapshot.offset, items.count))
        pagination.replaceItems(updatedItems)
    }
}
