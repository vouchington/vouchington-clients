import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeReferralLinksManagementViewModel {
    func mutate(_ endpoint: Endpoint) async -> Bool {
        await load {
            let _: EmptyResponse = try await client.send(endpoint)
            try await loadLinksPage(after: nil, append: false)
        }
    }

    func loadLinksPage(after: String?, append: Bool) async throws {
        if !append {
            linkPagination.reset()
        } else if linkPagination.endCursor != after {
            linkPagination.restoreContinuation(endCursor: after, hasMore: true)
        }
        guard let request = linkPagination.beginNextPage() else { return }
        do {
            let response: ReferralLinkFeedResponse = try await client.send(
                .referralLinks(after: request.cursor, limit: 25)
            )
            linkPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo?.endCursor,
                hasNextPage: response.pageInfo?.hasNextPage ?? false
            )
        } catch {
            linkPagination.fail(request, error: vouchaError(from: error))
            throw error
        }
    }

    func loadClicksPage(after: String?) async throws {
        if after == nil {
            clickPagination.reset()
        } else if clickPagination.endCursor != after {
            clickPagination.restoreContinuation(endCursor: after, hasMore: true)
        }
        guard let request = clickPagination.beginNextPage() else { return }
        do {
            let response: ReferralClickLogResponse = try await client.send(
                .myReferralClicks(after: request.cursor, limit: 25)
            )
            let newClicks = response.results.compactMap { response.clicks[$0.id] }
            guard clickPagination.isCurrent(request) else { return }
            if request.cursor == nil {
                clickUsers = response.users
            } else {
                clickUsers.merge(response.users) { _, new in new }
            }
            clickPagination.complete(
                request,
                items: newClicks,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch {
            clickPagination.fail(request, error: vouchaError(from: error))
            throw error
        }
    }

    func load(_ operation: () async throws -> Void) async -> Bool {
        state = .loading
        do {
            try await operation()
            state = .loaded
            return true
        } catch let error as VouchaError {
            state = .error(error)
            return false
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
            return false
        }
    }

    private func vouchaError(from error: Error) -> VouchaError {
        (error as? VouchaError) ?? .api(statusCode: 0, preconditionCode: nil)
    }
}

struct NativeReferralLinksManagementSnapshot {
    let linkPagination: CursorPaginationState<NativeReferralLink>
    let clickPagination: CursorPaginationState<ReferralClickLogEntry>
    let clickUsers: [String: ReferralClickLogUser]

    @MainActor
    init(viewModel: NativeReferralLinksManagementViewModel) {
        (linkPagination, clickPagination) = (viewModel.linkPagination, viewModel.clickPagination)
        clickUsers = viewModel.clickUsers
    }

    @MainActor
    func restore(to viewModel: NativeReferralLinksManagementViewModel) {
        (viewModel.linkPagination, viewModel.clickPagination) = (linkPagination, clickPagination)
        viewModel.clickUsers = clickUsers
    }
}
