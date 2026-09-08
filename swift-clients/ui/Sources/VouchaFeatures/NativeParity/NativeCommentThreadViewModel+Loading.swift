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
        ancestorPagination.reset()
        ancestorPosts = []
        resetThreadMetadata()
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
        resetPaginationForPermalink()
        guard let request = descendantPagination.beginInitialPageIfNeeded() else { return }
        guard let ancestorRequest = ancestorPagination.beginInitialPageIfNeeded() else { return }
        do {
            async let detail: PostEnvelope = client.send(.post(idOrSlug: rootPostId))
            async let descendants: PostThreadEnvelope = client.send(
                .postDescendants(postId: rootPostId, after: request.cursor)
            )
            async let ancestors: PostThreadEnvelope = client.send(
                .postAncestors(postId: target, after: ancestorRequest.cursor, limit: 5)
            )
            let responses = try await (detail, descendants, ancestors)
            guard descendantPagination.isCurrent(request), ancestorPagination.isCurrent(ancestorRequest) else { return }
            guard !Task.isCancelled else {
                finishInitialLoadError(
                    CancellationError(),
                    request: request,
                    includePermalinkStates: true,
                    ancestorRequest: ancestorRequest
                )
                return
            }
            apply(detailResponse: responses.0)
            apply(descendantsResponse: responses.1, request: request)
            apply(ancestorsResponse: responses.2, request: ancestorRequest)
            rebuildCommentTree()
            ancestorState = .loaded
            focusedState = .loaded
            threadState = .loaded
        } catch {
            finishInitialLoadError(
                error,
                request: request,
                includePermalinkStates: true,
                ancestorRequest: ancestorRequest
            )
        }
    }
}

private extension NativeCommentThreadViewModel {
    func resetPaginationForPermalink() {
        descendantPagination.reset()
        ancestorPagination.reset()
        ancestorPosts = []
        resetThreadMetadata()
    }

    func finishInitialLoadError(
        _ error: Error,
        request: CursorPageRequest,
        includePermalinkStates: Bool,
        ancestorRequest: CursorPageRequest? = nil
    ) {
        guard descendantPagination.isCurrent(request) else { return }
        if error is CancellationError || Task.isCancelled {
            descendantPagination.cancel(request)
            if let ancestorRequest {
                ancestorPagination.cancel(ancestorRequest)
            }
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
        if let ancestorRequest {
            ancestorPagination.fail(ancestorRequest, error: vouchaError)
        }
        threadState = .error(vouchaError)
        if includePermalinkStates {
            ancestorState = .error(vouchaError)
            focusedState = .error(vouchaError)
        }
    }
}
