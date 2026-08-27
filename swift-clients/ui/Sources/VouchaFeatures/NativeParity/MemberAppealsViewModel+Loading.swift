import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

enum MemberAppealNoticeKind {
    case warnings
    case bans
    case removedPosts
}

extension MemberAppealsViewModel {
    func load() async {
        guard isSignedIn, let client else { return }
        isLoading = true
        errorMessage = nil
        loadMoreErrorMessage = nil
        let isRetry = !failedInitialStreams.isEmpty
        if !isRetry {
            resetPages()
        }
        do {
            try await loadInitialPages(client)
        } catch is CancellationError {
            // A route transition owns the next load.
        } catch {
            errorMessage = message(for: error)
        }
        isLoading = false
    }

    private func loadInitialPages(_ client: APIClient) async throws {
        if failedInitialStreams.isEmpty {
            switch route {
            case .tracking:
                try await loadTracking(client)
            case .warnings:
                try await loadWarnings(client)
            case .bans:
                try await loadBans(client)
            case .removedPosts:
                try await loadRemovedPosts(client)
            case .suspension:
                try await loadSuspension(client)
            }
        } else {
            try await retryFailedInitialStreams(client)
        }
        guard await reconcilePendingAppeals(client) else {
            if Task.isCancelled {
                throw CancellationError()
            }
            if let error = pendingAppealPagination.lastError {
                failedInitialStreams.insert(.pendingAppeals)
                throw error
            }
            return
        }
        failedInitialStreams.remove(.pendingAppeals)
    }

    func loadMoreAppeals(status: ModerationAppealStatus) async {
        guard isSignedIn, let client else { return }
        switch status {
        case .pending:
            await loadMoreAppeals(client, status: status, pagination: \.pendingAppealPagination)
        case .resolved:
            await loadMoreAppeals(client, status: status, pagination: \.resolvedAppealPagination)
        case .dismissed:
            await loadMoreAppeals(client, status: status, pagination: \.dismissedAppealPagination)
        }
    }

    private func loadMoreAppeals(
        _ client: APIClient,
        status: ModerationAppealStatus,
        pagination keyPath: ReferenceWritableKeyPath<MemberAppealsViewModel, CursorPaginationState<ModerationAppeal>>
    ) async {
        guard let request = self[keyPath: keyPath].beginNextPage() else { return }
        loadMoreErrorMessage = nil
        do {
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: status, limit: 25, after: request.cursor, mine: true)
            )
            self[keyPath: keyPath].complete(
                request,
                items: response.appeals,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            self[keyPath: keyPath].cancel(request)
        } catch {
            self[keyPath: keyPath].fail(request, error: vouchaError(from: error))
            loadMoreErrorMessage = message(for: error)
        }
    }

    func loadMoreNotices(_ kind: MemberAppealNoticeKind) async {
        guard isSignedIn, let client else { return }
        loadMoreErrorMessage = nil
        guard await reconcilePendingAppeals(client) else { return }
        switch kind {
        case .warnings: await loadMoreWarnings(client)
        case .bans: await loadMoreBans(client)
        case .removedPosts: await loadMoreRemovals(client)
        }
    }

    func reconcilePendingAppeals(_ client: APIClient) async -> Bool {
        guard !pendingAppealPagination.isLoading else { return false }
        while !pendingAppealPagination.hasLoadedPage
            || pendingAppealPagination.hasMore
            || pendingAppealPagination.lastError != nil {
            guard !Task.isCancelled, !pendingAppealPagination.isLoading else { return false }
            await loadMoreAppeals(client, status: .pending, pagination: \.pendingAppealPagination)
            guard pendingAppealPagination.hasLoadedPage,
                  pendingAppealPagination.lastError == nil
            else { return false }
        }
        return true
    }

    private func resetPages() {
        pendingAppealPagination.reset()
        resolvedAppealPagination.reset()
        dismissedAppealPagination.reset()
        warningPagination.reset()
        banPagination.reset()
        removalPagination.reset()
        suspensionDate = nil
    }
}
