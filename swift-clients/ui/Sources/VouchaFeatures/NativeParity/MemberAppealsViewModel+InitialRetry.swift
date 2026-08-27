import VouchaAPI
import VouchaCore
import VouchaModels

extension MemberAppealsViewModel {
    func retryFailedInitialStreams(_ client: APIClient) async throws {
        let streams = orderedInitialStreams.filter(failedInitialStreams.contains)
        var firstError: Error?
        for stream in streams {
            do {
                try await retry(stream, client: client)
                failedInitialStreams.remove(stream)
            } catch {
                firstError = firstError ?? error
            }
        }
        if let firstError {
            throw firstError
        }
    }

    private func retry(_ stream: MemberAppealInitialStream, client: APIClient) async throws {
        switch stream {
        case .pendingAppeals:
            if pendingAppealPagination.hasLoadedPage {
                guard await reconcilePendingAppeals(client) else {
                    if Task.isCancelled {
                        throw CancellationError()
                    }
                    throw pendingAppealPagination.lastError
                        ?? VouchaError.api(statusCode: 0, preconditionCode: nil)
                }
                return
            }
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: .pending, limit: 25, mine: true)
            )
            apply(pending: response)
        case .resolvedAppeals:
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: .resolved, limit: 25, mine: true)
            )
            apply(resolved: response)
        case .dismissedAppeals:
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: .dismissed, limit: 25, mine: true)
            )
            apply(dismissed: response)
        case .warnings:
            let response: MemberWarningNoticesResponse = try await client.send(.memberWarningNotices())
            apply(warnings: response)
        case .bans:
            let response: MemberCommunityBanNoticesResponse = try await client.send(.memberCommunityBanNotices())
            apply(bans: response)
        case .removedPosts:
            let response: MemberRemovedPostNoticesResponse = try await client.send(.memberRemovedPostNotices())
            apply(removals: response)
        case .identity:
            let response: NativeIdentityResponse = try await client.send(.myIdentity)
            apply(identity: response)
        }
    }

    private var orderedInitialStreams: [MemberAppealInitialStream] {
        switch route {
        case .tracking:
            [.pendingAppeals, .resolvedAppeals, .dismissedAppeals, .warnings, .bans, .removedPosts, .identity]
        case .warnings:
            [.pendingAppeals, .warnings]
        case .bans:
            [.pendingAppeals, .bans]
        case .removedPosts:
            [.pendingAppeals, .removedPosts]
        case .suspension:
            [.pendingAppeals, .identity]
        }
    }
}
