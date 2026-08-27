import VouchaAPI
import VouchaModels

extension NativeReviewQueueViewModel {
    func load() async {
        switch state {
        case .idle, .error:
            await replaceItems(isInitial: items.isEmpty)
        case .loaded, .empty:
            break
        case .signInRequired, .administratorRequired, .loading:
            return
        }
        await refetchExposure()
    }

    func refresh() async {
        guard canRefresh else { return }
        await replaceItems(isInitial: false)
        await refetchExposure()
    }

    func loadMore() async {
        guard canLoadMore, let client, let request = pagination.beginNextPage() else { return }
        let generation = beginListOperation()
        defer { finishListOperation(generation: generation) }
        do {
            let response: AdminReviewQueueResponse = try await client.send(
                .adminReviewQueue(after: request.cursor, limit: 25)
            )
            guard accepts(generation: generation), pagination.isCurrent(request) else { return }
            pagination.complete(
                request,
                items: supportedItems(response.results),
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            errorMessage = nil
            applyContentState()
        } catch {
            guard accepts(generation: generation) else { return }
            pagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            errorMessage = .message(.nativeSwiftModerationReportsReviewQueueLoadMoreFailed)
        }
    }

    func perform(_ action: NativeReviewQueueAction, postId: String) async {
        guard isAuthorized, !isListLoading, !inFlightPostIds.contains(postId),
              let item = items.first(where: { $0.id == postId }),
              allows(action, for: item), let client
        else { return }
        inFlightPostIds.insert(postId)
        defer { inFlightPostIds.remove(postId) }
        do {
            let response: ClearanceUpdateResponse = try await client.send(
                .updatePostClearance(postId: postId, status: action.status)
            )
            guard !Task.isCancelled else { return }
            if response.clearanceStatus == .approved {
                items.removeAll { $0.id == postId }
                applyContentState()
                errorMessage = nil
            } else if response.clearanceStatus == .rejected || response.clearanceStatus == .inReview,
                      let index = items.firstIndex(where: { $0.id == postId }) {
                items[index].clearanceStatus = response.clearanceStatus
                errorMessage = nil
            } else {
                errorMessage = .message(.nativeSwiftModerationReportsReviewQueueUnsupportedStatus)
            }
        } catch {
            guard !Task.isCancelled else { return }
            errorMessage = .message(.nativeSwiftModerationReportsReviewQueueUpdateFailed)
        }
    }

    func cancelListOperations() {
        listGeneration += 1
        exposureRequestRevision += 1
        isListLoading = false
        exposureIsStale = true
        cancelScheduledCooldownRefetch()
        pagination.invalidateRequestsPreservingPage()
        state = if !isSignedIn {
            .signInRequired
        } else if !isAdministrator {
            .administratorRequired
        } else {
            items.isEmpty ? .idle : .loaded
        }
    }

    private func replaceItems(isInitial: Bool) async {
        guard isAuthorized, inFlightPostIds.isEmpty, let client else { return }
        let existingPagination = pagination
        pagination.reset()
        guard let request = pagination.beginNextPage() else { return }
        let generation = beginListOperation()
        if isInitial {
            state = .loading
        }
        defer { finishListOperation(generation: generation) }
        do {
            let response: AdminReviewQueueResponse = try await client.send(.adminReviewQueue(limit: 25))
            guard accepts(generation: generation), pagination.isCurrent(request) else {
                pagination = existingPagination
                return
            }
            pagination.complete(
                request,
                items: supportedItems(response.results),
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            errorMessage = nil
            applyContentState()
        } catch {
            guard accepts(generation: generation) else {
                pagination = existingPagination
                return
            }
            pagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            if !isInitial {
                pagination = existingPagination
            }
            errorMessage = .message(.nativeSwiftModerationReportsReviewQueueLoadFailed)
            if isInitial {
                state = .error
            }
        }
    }

    private func beginListOperation() -> Int {
        listGeneration += 1
        isListLoading = true
        errorMessage = nil
        return listGeneration
    }

    private func finishListOperation(generation: Int) {
        guard listGeneration == generation else { return }
        isListLoading = false
        if Task.isCancelled, state == .loading {
            state = items.isEmpty ? .idle : .loaded
        }
    }

    private func accepts(generation: Int) -> Bool {
        !Task.isCancelled && generation == listGeneration
    }

    private func supportedItems(_ posts: [AdminReviewQueuePost]) -> [NativeReviewQueueItem] {
        posts.compactMap { post in
            switch post.clearanceStatus {
            case .rejected, .inReview:
                NativeReviewQueueItem(post: post, clearanceStatus: post.clearanceStatus)
            case .approved, .pending:
                nil
            }
        }
    }

    private func allows(_ action: NativeReviewQueueAction, for item: NativeReviewQueueItem) -> Bool {
        switch action {
        case .approve, .reject:
            item.clearanceStatus == .rejected || item.clearanceStatus == .inReview
        case .reReview:
            item.clearanceStatus == .rejected
        }
    }

    private func applyContentState() {
        state = items.isEmpty && !hasNextPage ? .empty : .loaded
    }
}
