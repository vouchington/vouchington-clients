import VouchaAPI
import VouchaCore
import VouchaModels

extension ModerationAppealsViewModel {
    func load() async {
        await load(retryAfterOutcome: true)
    }

    private func load(retryAfterOutcome: Bool) async {
        guard canAccess, let client else { return }
        loadRevision += 1
        let revision = loadRevision
        let expectedStatus = selectedStatus
        let expectedOutcomeRevision = targetedOutcomeRevision
        summaryPagination.reset()
        guard let request = summaryPagination.beginNextPage() else { return }
        isLoading = true
        defer {
            if revision == loadRevision { isLoading = false }
        }
        errorMessage = nil
        loadMoreErrorMessage = nil
        do {
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: expectedStatus, limit: 25)
            )
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            guard expectedOutcomeRevision == targetedOutcomeRevision else {
                if retryAfterOutcome {
                    summaryPagination.cancel(request)
                    await load(retryAfterOutcome: false)
                } else {
                    summaryPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
                    errorMessage = .message(.nativeSwiftModerationAppealsGenericError)
                }
                return
            }
            summaryPagination.complete(
                request,
                items: response.appeals,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            guard revision == loadRevision else { return }
            summaryPagination.cancel(request)
        } catch {
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            summaryPagination.fail(request, error: vouchaError(from: error))
            errorMessage = message(for: error)
        }
    }

    func selectStatus(_ status: ModerationAppealStatus) async {
        guard status != selectedStatus else { return }
        selectedStatus = status
        summaryPagination.reset()
        await load()
    }

    func loadMore() async {
        guard canAccess, let client, !isLoading,
              let request = summaryPagination.beginNextPage()
        else { return }
        let revision = loadRevision
        let expectedStatus = selectedStatus
        let expectedOutcomeRevision = targetedOutcomeRevision
        loadMoreErrorMessage = nil
        do {
            let response: ModerationAppealListResponse = try await client.send(
                .appeals(status: expectedStatus, limit: 25, after: request.cursor)
            )
            guard revision == loadRevision, expectedStatus == selectedStatus,
                  expectedOutcomeRevision == targetedOutcomeRevision,
                  summaryPagination.isCurrent(request)
            else {
                summaryPagination.cancel(request)
                return
            }
            summaryPagination.complete(
                request,
                items: response.appeals,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            guard revision == loadRevision else { return }
            summaryPagination.cancel(request)
        } catch {
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            summaryPagination.fail(request, error: vouchaError(from: error))
            loadMoreErrorMessage = message(for: error)
        }
    }

    private func vouchaError(from error: Error) -> VouchaError {
        (error as? VouchaError) ?? .api(statusCode: 0, preconditionCode: nil)
    }
}
