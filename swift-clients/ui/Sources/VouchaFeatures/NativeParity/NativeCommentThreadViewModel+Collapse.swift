import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension NativeCommentThreadViewModel {
    func toggleCollapse(commentId: String) async {
        guard sessionIsActive, !inFlightCollapseIds.contains(commentId) else { return }
        let shouldCollapse = !collapsedCommentIds.contains(commentId)
        guard isSignedIn else {
            setCollapsed(commentId, active: shouldCollapse)
            if let collapseStorageKey {
                collapseDefaults.set(collapsedCommentIds.sorted(), forKey: collapseStorageKey)
            }
            return
        }
        guard let client else { return }
        let generation = collapseGeneration
        inFlightCollapseIds.insert(commentId)
        mutationState = .loading
        defer {
            if generation == collapseGeneration { inFlightCollapseIds.remove(commentId) }
        }
        do {
            try await emailVerificationGate.perform(rollbackOnFailure: {}, {
                let endpoint: Endpoint = shouldCollapse
                    ? .bookmark(entityType: "post", entityId: commentId, predicate: "hide")
                    : .unbookmark(entityType: "post", entityId: commentId, predicate: "hide")
                let _: EmptyResponse = try await client.send(endpoint)
            })
            guard sessionIsActive, generation == collapseGeneration, !Task.isCancelled else { return }
            collapseOverrides[commentId] = shouldCollapse
            bookmarksByPostId[commentId, default: [:]]["hide"] = shouldCollapse
            setCollapsed(commentId, active: shouldCollapse)
            mutationState = .loaded
            if !shouldCollapse { await loadMoreHiddenDescendants(commentId: commentId) }
        } catch {
            guard sessionIsActive, generation == collapseGeneration else { return }
            mutationState = error is CancellationError ? .idle : .error(
                error as? VouchaError ?? .api(statusCode: 0, preconditionCode: nil)
            )
        }
    }

    func loadMoreHiddenDescendants(commentId: String) async {
        guard sessionIsActive, isSignedIn, let client,
              !collapsedCommentIds.contains(commentId) else { return }
        var pagination = subtreePagination[commentId] ?? CursorPaginationState<Post>()
        guard let request = pagination.beginNextPage() else { return }
        subtreePagination[commentId] = pagination
        let generation = collapseGeneration
        do {
            let response: PostThreadEnvelope = try await client.send(
                .postDescendants(postId: commentId, after: request.cursor)
            )
            guard sessionIsActive, generation == collapseGeneration,
                  subtreePagination[commentId]?.isCurrent(request) == true else { return }
            guard !Task.isCancelled else {
                subtreePagination[commentId]?.cancel(request)
                return
            }
            let page = response.results.compactMap {
                response.posts[$0.entityId]?.withRenderedHtml(response.markdownToHtml?[$0.entityId])
            }
            mergeThreadMetadata(from: response)
            subtreePagination[commentId]?.complete(
                request, items: page, endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            var known = Set(descendantPosts.map(\.id))
            descendantPosts.append(contentsOf: page.filter { known.insert($0.id).inserted })
            descendantPagination.replaceItems(descendantPosts)
            rebuildCommentTree()
        } catch {
            guard sessionIsActive, generation == collapseGeneration else { return }
            if error is CancellationError || Task.isCancelled {
                subtreePagination[commentId]?.cancel(request)
            } else {
                subtreePagination[commentId]?.fail(
                    request, error: error as? VouchaError ?? .api(statusCode: 0, preconditionCode: nil)
                )
            }
        }
    }

    func invalidateSession() {
        sessionIsActive = false
        descendantPagination.reset()
        ancestorPagination.reset()
        resetCollapseRequests()
        collapsedCommentIds = []
        descendantPosts = []
        ancestorPosts = []
        commentTree = []
        rootPost = nil
        bookmarksByPostId = [:]
    }
}

extension NativeCommentThreadViewModel {
    func resetCollapseRequests() {
        collapseGeneration += 1
        inFlightCollapseIds = []
        subtreePagination = [:]
        collapseOverrides = [:]
        if isSignedIn { collapsedCommentIds = [] }
    }

    private func setCollapsed(_ id: String, active: Bool) {
        if active {
            collapsedCommentIds.insert(id)
        } else {
            collapsedCommentIds.remove(id)
        }
    }
}
