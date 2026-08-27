import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

struct BookmarkDestinationIdentity: Hashable {
    let contextId: UUID
    let rowId: String
}

struct PendingBookmarkDestination {
    let token: UUID
    let task: Task<String?, Never>
}

extension NativeRouteSurfaceViewModel {
    func bookmarkDestinationPath(for row: NativeBookmarkRow) async -> String? {
        let contextId = bookmarkContextId
        guard let currentRow = bookmarkRows.first(where: { $0.id == row.id }) else { return nil }

        switch currentRow.destination {
        case let .path(path):
            return path
        case let .comment(commentId, suppliedRootId):
            let identity = BookmarkDestinationIdentity(contextId: contextId, rowId: currentRow.id)
            if let cached = bookmarkDestinationCache[identity] {
                return cached
            }
            guard let client else { return nil }
            if let pending = pendingBookmarkDestinations[identity] {
                _ = await pending.task.value
                return nil
            }

            let token = UUID()
            let task: Task<String?, Never> = Task { [weak self] in
                guard let self else { return nil }
                return await resolveCommentDestination(
                    client: client,
                    commentId: commentId,
                    suppliedRootId: suppliedRootId
                )
            }
            pendingBookmarkDestinations[identity] = .init(token: token, task: task)
            let path = await task.value
            guard pendingBookmarkDestinations[identity]?.token == token else { return nil }
            pendingBookmarkDestinations.removeValue(forKey: identity)
            guard bookmarkContextId == identity.contextId,
                  bookmarkRows.contains(where: { $0.id == identity.rowId })
            else {
                return nil
            }
            guard let path else {
                bookmarkMutationErrorMessage = .message(.nativeSwiftHouseholdsBookmarksCommentOpenFailed)
                return nil
            }
            bookmarkDestinationCache[identity] = path
            return path
        }
    }

    func resetBookmarkDestinationResolution() {
        pendingBookmarkDestinations.values.forEach { $0.task.cancel() }
        pendingBookmarkDestinations.removeAll()
        bookmarkDestinationCache.removeAll()
        bookmarkContextId = UUID()
    }

    private func resolveCommentDestination(
        client: APIClient,
        commentId: String,
        suppliedRootId: String?
    ) async -> String? {
        do {
            let rootId: String
            if let suppliedRootId {
                rootId = suppliedRootId
            } else {
                let comment: PostEnvelope = try await client.send(.post(idOrSlug: commentId))
                guard let resolvedRootId = comment.post.rootId else { return nil }
                rootId = resolvedRootId
            }
            let root: PostEnvelope = try await client.send(.post(idOrSlug: rootId))
            guard let rootPath = NativeBookmarkRow.postPath(
                type: root.post.postType,
                id: root.post.id,
                slug: root.post.slug
            ) else {
                return nil
            }
            return "\(rootPath)/comment/\(commentId)"
        } catch {
            return nil
        }
    }

    func removeBookmarkRow(_ row: NativeBookmarkRow) async {
        guard let client, !inFlightBookmarkRowIds.contains(row.id) else { return }
        guard let index = bookmarkRows.firstIndex(where: { $0.id == row.id }) else { return }
        guard let inverseAction = bookmarkRows[index].inverseAction else { return }
        let contextId = bookmarkContextId
        let removedRow = bookmarkRows.remove(at: index)
        bookmarkPagination.remove { $0.id == removedRow.id }
        bookmarkMutationErrorMessage = nil
        inFlightBookmarkRowIds.insert(row.id)
        defer { inFlightBookmarkRowIds.remove(row.id) }

        do {
            let _: EmptyResponse = try await client.send(.unbookmark(
                entityType: inverseAction.entityType,
                entityId: removedRow.entityId,
                predicate: inverseAction.predicate
            ))
        } catch {
            guard contextId == bookmarkContextId else { return }
            if !bookmarkRows.contains(where: { $0.id == removedRow.id }) {
                bookmarkRows.append(removedRow)
                bookmarkRows.sort { $0.rank < $1.rank }
                bookmarkPagination.replaceItems(bookmarkRows)
            }
            bookmarkMutationErrorMessage = .message(.nativeSwiftHouseholdsBookmarksCollectionUpdateFailed)
        }
    }

    func isRemovingBookmarkRow(_ row: NativeBookmarkRow) -> Bool {
        inFlightBookmarkRowIds.contains(row.id)
    }
}
