import VouchaAPI
import VouchaModels

extension MemberAppealsViewModel {
    func loadWarnings(_ client: APIClient) async throws {
        async let pending: ModerationAppealListResponse = pendingAppeals(client)
        async let warnings: MemberWarningNoticesResponse = client.send(.memberWarningNotices())
        var firstError: Error?
        do {
            try await apply(pending: pending)
            recordSuccess(.pendingAppeals)
        } catch {
            recordFailure(.pendingAppeals, error: error, firstError: &firstError)
        }
        do {
            try await apply(warnings: warnings)
            recordSuccess(.warnings)
        } catch {
            recordFailure(.warnings, error: error, firstError: &firstError)
        }
        try throwIfNeeded(firstError)
    }

    func loadBans(_ client: APIClient) async throws {
        async let pending: ModerationAppealListResponse = pendingAppeals(client)
        async let bans: MemberCommunityBanNoticesResponse = client.send(.memberCommunityBanNotices())
        var firstError: Error?
        do {
            try await apply(pending: pending)
            recordSuccess(.pendingAppeals)
        } catch {
            recordFailure(.pendingAppeals, error: error, firstError: &firstError)
        }
        do {
            try await apply(bans: bans)
            recordSuccess(.bans)
        } catch {
            recordFailure(.bans, error: error, firstError: &firstError)
        }
        try throwIfNeeded(firstError)
    }

    func loadRemovedPosts(_ client: APIClient) async throws {
        async let pending: ModerationAppealListResponse = pendingAppeals(client)
        async let removals: MemberRemovedPostNoticesResponse = client.send(.memberRemovedPostNotices())
        var firstError: Error?
        do {
            try await apply(pending: pending)
            recordSuccess(.pendingAppeals)
        } catch {
            recordFailure(.pendingAppeals, error: error, firstError: &firstError)
        }
        do {
            try await apply(removals: removals)
            recordSuccess(.removedPosts)
        } catch {
            recordFailure(.removedPosts, error: error, firstError: &firstError)
        }
        try throwIfNeeded(firstError)
    }

    func loadSuspension(_ client: APIClient) async throws {
        async let pending: ModerationAppealListResponse = pendingAppeals(client)
        async let identity: NativeIdentityResponse = client.send(.myIdentity)
        var firstError: Error?
        do {
            try await apply(pending: pending)
            recordSuccess(.pendingAppeals)
        } catch {
            recordFailure(.pendingAppeals, error: error, firstError: &firstError)
        }
        do {
            try await apply(identity: identity)
            recordSuccess(.identity)
        } catch {
            recordFailure(.identity, error: error, firstError: &firstError)
        }
        try throwIfNeeded(firstError)
    }

    func apply(
        pending: ModerationAppealListResponse? = nil,
        resolved: ModerationAppealListResponse? = nil,
        dismissed: ModerationAppealListResponse? = nil,
        warnings: MemberWarningNoticesResponse? = nil,
        bans: MemberCommunityBanNoticesResponse? = nil,
        removals: MemberRemovedPostNoticesResponse? = nil,
        identity: NativeIdentityResponse? = nil
    ) {
        apply(pending, to: \.pendingAppealPagination)
        apply(resolved, to: \.resolvedAppealPagination)
        apply(dismissed, to: \.dismissedAppealPagination)
        if let warnings {
            warningPagination.reset(items: warnings.warnings)
            warningPagination.restoreContinuation(
                endCursor: warnings.pageInfo.endCursor,
                hasMore: warnings.pageInfo.hasNextPage
            )
        }
        if let bans {
            banPagination.reset(items: bans.bans)
            banPagination.restoreContinuation(endCursor: bans.pageInfo.endCursor, hasMore: bans.pageInfo.hasNextPage)
        }
        if let removals {
            removalPagination.reset(items: removals.removedPosts)
            removalPagination.restoreContinuation(
                endCursor: removals.pageInfo.endCursor,
                hasMore: removals.pageInfo.hasNextPage
            )
        }
        if let identity {
            suspensionDate = identity.identity.suspendedAt
        }
    }

    private func apply(
        _ response: ModerationAppealListResponse?,
        to keyPath: ReferenceWritableKeyPath<MemberAppealsViewModel, CursorPaginationState<ModerationAppeal>>
    ) {
        guard let response else { return }
        self[keyPath: keyPath].reset(items: response.appeals)
        self[keyPath: keyPath].restoreContinuation(
            endCursor: response.pageInfo.endCursor,
            hasMore: response.pageInfo.hasNextPage
        )
    }

    func throwIfNeeded(_ error: Error?) throws {
        if let error {
            throw error
        }
    }

    func recordSuccess(_ stream: MemberAppealInitialStream) {
        failedInitialStreams.remove(stream)
    }

    func recordFailure(
        _ stream: MemberAppealInitialStream,
        error: Error,
        firstError: inout Error?
    ) {
        failedInitialStreams.insert(stream)
        firstError = firstError ?? error
    }

    func captureInitialResponse<T>(
        operation: () async throws -> T
    ) async -> Result<T, Error> {
        do {
            let response = try await operation()
            return .success(response)
        } catch {
            return .failure(error)
        }
    }

    func applyInitialResponse<T>(
        _ result: Result<T, Error>,
        _ stream: MemberAppealInitialStream,
        apply: (T) -> Void
    ) -> Error? {
        switch result {
        case let .success(response):
            apply(response)
            recordSuccess(stream)
            return nil
        case let .failure(error):
            failedInitialStreams.insert(stream)
            return error
        }
    }

    private func pendingAppeals(_ client: APIClient) async throws -> ModerationAppealListResponse {
        try await client.send(.appeals(status: .pending, limit: 25, mine: true))
    }
}
