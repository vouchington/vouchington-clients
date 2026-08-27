import VouchaAPI
import VouchaCore
import VouchaModels

extension MemberAppealsViewModel {
    func loadMoreWarnings(_ client: APIClient) async {
        guard let request = warningPagination.beginNextPage() else { return }
        do {
            let response: MemberWarningNoticesResponse = try await client.send(
                .memberWarningNotices(after: request.cursor)
            )
            warningPagination.complete(
                request,
                items: response.warnings,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch {
            warningPagination.fail(request, error: vouchaError(from: error))
            loadMoreErrorMessage = message(for: error)
        }
    }

    func loadMoreBans(_ client: APIClient) async {
        guard let request = banPagination.beginNextPage() else { return }
        do {
            let response: MemberCommunityBanNoticesResponse = try await client.send(
                .memberCommunityBanNotices(after: request.cursor)
            )
            banPagination.complete(
                request,
                items: response.bans,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch {
            banPagination.fail(request, error: vouchaError(from: error))
            loadMoreErrorMessage = message(for: error)
        }
    }

    func loadMoreRemovals(_ client: APIClient) async {
        guard let request = removalPagination.beginNextPage() else { return }
        do {
            let response: MemberRemovedPostNoticesResponse = try await client.send(
                .memberRemovedPostNotices(after: request.cursor)
            )
            removalPagination.complete(
                request,
                items: response.removedPosts,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch {
            removalPagination.fail(request, error: vouchaError(from: error))
            loadMoreErrorMessage = message(for: error)
        }
    }

    func vouchaError(from error: Error) -> VouchaError {
        (error as? VouchaError) ?? .api(statusCode: 0, preconditionCode: nil)
    }
}
