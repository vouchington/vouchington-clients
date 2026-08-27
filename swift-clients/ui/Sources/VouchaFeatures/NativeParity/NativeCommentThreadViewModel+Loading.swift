import VouchaAPI
import VouchaCore
import VouchaModels

public extension NativeCommentThreadViewModel {
    func loadThread() async {
        guard let client else {
            setMissingClientError(includePermalinkStates: false)
            return
        }
        threadState = .loading
        descendantPagination.reset()
        guard let request = descendantPagination.beginInitialPageIfNeeded() else { return }
        do {
            async let detail: PostEnvelope = client.send(.post(idOrSlug: rootPostId))
            async let descendants: PostThreadEnvelope = client.send(
                .postDescendants(postId: rootPostId, after: request.cursor)
            )
            let (detailResponse, descendantsResponse) = try await (detail, descendants)
            guard descendantPagination.isCurrent(request) else { return }
            guard !Task.isCancelled else {
                finishInitialLoadError(CancellationError(), request: request, includePermalinkStates: false)
                return
            }
            apply(detailResponse: detailResponse)
            apply(descendantsResponse: descendantsResponse, request: request)
            rebuildCommentTree()
            threadState = .loaded
        } catch {
            finishInitialLoadError(error, request: request, includePermalinkStates: false)
        }
    }

    func loadPermalink(targetCommentId: String) async {
        guard let client else {
            setMissingClientError(includePermalinkStates: true)
            return
        }
        guard let target = normalizedPermalinkTarget(targetCommentId) else {
            await loadThreadForBlankPermalinkTarget()
            return
        }
        focusedCommentId = target
        ancestorState = .loading
        focusedState = .loading
        threadState = .loading
        descendantPagination.reset()
        guard let request = descendantPagination.beginInitialPageIfNeeded() else { return }
        do {
            async let detail: PostEnvelope = client.send(.post(idOrSlug: rootPostId))
            async let descendants: PostThreadEnvelope = client.send(
                .postDescendants(postId: rootPostId, after: request.cursor)
            )
            async let ancestors: PostThreadEnvelope = client.send(.postAncestors(postId: target))
            let responses = try await (detail, descendants, ancestors)
            guard descendantPagination.isCurrent(request) else { return }
            guard !Task.isCancelled else {
                finishInitialLoadError(CancellationError(), request: request, includePermalinkStates: true)
                return
            }
            apply(detailResponse: responses.0)
            apply(descendantsResponse: responses.1, request: request)
            apply(ancestorsResponse: responses.2)
            rebuildCommentTree()
            ancestorState = .loaded
            focusedState = .loaded
            threadState = .loaded
        } catch {
            finishInitialLoadError(error, request: request, includePermalinkStates: true)
        }
    }
}

private extension NativeCommentThreadViewModel {
    func finishInitialLoadError(
        _ error: Error,
        request: CursorPageRequest,
        includePermalinkStates: Bool
    ) {
        guard descendantPagination.isCurrent(request) else { return }
        if error is CancellationError || Task.isCancelled {
            descendantPagination.cancel(request)
            let restoredState: LoadState = rootPost == nil ? .idle : .loaded
            threadState = restoredState
            if includePermalinkStates {
                ancestorState = restoredState
                focusedState = restoredState
            }
            return
        }
        let vouchaError = error as? VouchaError ?? .api(statusCode: 0, preconditionCode: nil)
        descendantPagination.fail(request, error: vouchaError)
        threadState = .error(vouchaError)
        if includePermalinkStates {
            ancestorState = .error(vouchaError)
            focusedState = .error(vouchaError)
        }
    }
}
