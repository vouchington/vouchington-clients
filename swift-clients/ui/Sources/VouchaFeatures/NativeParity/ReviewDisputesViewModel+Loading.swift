import VouchaCore
import VouchaModels

extension ReviewDisputesViewModel {
    func load() async {
        guard canAccess, let client else { return }
        loadRevision += 1
        let revision = loadRevision
        let expectedOutcomeRevision = targetedOutcomeRevision
        let expectedStatus = selectedStatus
        summaryPagination.reset()
        guard let request = summaryPagination.beginNextPage() else { return }
        isLoading = true
        defer {
            if revision == loadRevision { isLoading = false }
        }
        errorMessage = nil
        loadMoreErrorMessage = nil
        do {
            let response: ReviewDisputeListResponse = try await client.send(
                .disputes(status: expectedStatus.listStatus, limit: 25)
            )
            guard revision == loadRevision, expectedStatus == selectedStatus,
                  expectedOutcomeRevision == targetedOutcomeRevision
            else {
                summaryPagination.cancel(request)
                return
            }
            summaryPagination.complete(
                request,
                items: response.disputes,
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

    func selectStatus(_ status: ReviewDisputeStatus) async {
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
        let expectedOutcomeRevision = targetedOutcomeRevision
        let expectedStatus = selectedStatus
        loadMoreErrorMessage = nil
        do {
            let response: ReviewDisputeListResponse = try await client.send(
                .disputes(status: expectedStatus.listStatus, limit: 25, after: request.cursor)
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
                items: response.disputes,
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

private extension ReviewDisputeStatus {
    var listStatus: ModerationDisputeStatus {
        switch self {
        case .pending: .pending
        case .resolved: .resolved
        case .dismissed: .dismissed
        }
    }
}
