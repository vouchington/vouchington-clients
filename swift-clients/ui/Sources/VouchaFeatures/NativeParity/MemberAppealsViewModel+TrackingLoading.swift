import VouchaAPI
import VouchaModels

extension MemberAppealsViewModel {
    func loadTracking(_ client: APIClient) async throws {
        async let pending: Result<ModerationAppealListResponse, Error> = captureInitialResponse {
            try await client.send(.appeals(status: .pending, limit: 25, mine: true))
        }
        async let resolved: Result<ModerationAppealListResponse, Error> = captureInitialResponse {
            try await client.send(.appeals(status: .resolved, limit: 25, mine: true))
        }
        async let dismissed: Result<ModerationAppealListResponse, Error> = captureInitialResponse {
            try await client.send(.appeals(status: .dismissed, limit: 25, mine: true))
        }
        async let warnings: Result<MemberWarningNoticesResponse, Error> = captureInitialResponse {
            try await client.send(.memberWarningNotices())
        }
        async let bans: Result<MemberCommunityBanNoticesResponse, Error> = captureInitialResponse {
            try await client.send(.memberCommunityBanNotices())
        }
        async let removals: Result<MemberRemovedPostNoticesResponse, Error> = captureInitialResponse {
            try await client.send(.memberRemovedPostNotices())
        }
        async let identity: Result<NativeIdentityResponse, Error> = captureInitialResponse {
            try await client.send(.myIdentity)
        }
        let responses = await (pending, resolved, dismissed, warnings, bans, removals, identity)
        let errors = [
            applyInitialResponse(responses.0, .pendingAppeals) { apply(pending: $0) },
            applyInitialResponse(responses.1, .resolvedAppeals) { apply(resolved: $0) },
            applyInitialResponse(responses.2, .dismissedAppeals) { apply(dismissed: $0) },
            applyInitialResponse(responses.3, .warnings) { apply(warnings: $0) },
            applyInitialResponse(responses.4, .bans) { apply(bans: $0) },
            applyInitialResponse(responses.5, .removedPosts) { apply(removals: $0) },
            applyInitialResponse(responses.6, .identity) { apply(identity: $0) }
        ]
        try throwIfNeeded(errors.compactMap { $0 }.first)
    }
}
