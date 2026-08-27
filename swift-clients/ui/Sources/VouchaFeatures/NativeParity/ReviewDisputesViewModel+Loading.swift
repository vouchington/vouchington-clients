import VouchaCore
import VouchaModels

extension ReviewDisputesViewModel {
    func load() async {
        guard canAccess, let client else { return }
        loadRevision += 1
        let revision = loadRevision
        let expectedOutcomeRevision = targetedOutcomeRevision
        let expectedStatus = selectedStatus
        disputePagination.reset()
        guard let request = disputePagination.beginNextPage() else { return }
        isLoading = true
        errorMessage = nil
        loadMoreErrorMessage = nil
        do {
            let response: ReviewDisputeListResponse = try await client.send(
                .disputes(status: expectedStatus.listStatus, limit: 25)
            )
            guard revision == loadRevision, expectedStatus == selectedStatus,
                  expectedOutcomeRevision == targetedOutcomeRevision
            else {
                disputePagination.cancel(request)
                return
            }
            disputePagination.complete(
                request,
                items: response.disputes,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            seedDrafts(from: response.disputes)
        } catch is CancellationError {
            guard revision == loadRevision else { return }
            disputePagination.cancel(request)
        } catch {
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            disputePagination.fail(request, error: vouchaError(from: error))
            errorMessage = message(for: error)
        }
        guard revision == loadRevision else { return }
        isLoading = false
    }

    func selectStatus(_ status: ReviewDisputeStatus) async {
        guard status != selectedStatus else { return }
        selectedStatus = status
        disputePagination.reset()
        await load()
    }

    func loadMore() async {
        guard canAccess, let client, !isLoading,
              let request = disputePagination.beginNextPage()
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
                  disputePagination.isCurrent(request)
            else {
                disputePagination.cancel(request)
                return
            }
            disputePagination.complete(
                request,
                items: response.disputes,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            seedDrafts(from: response.disputes)
        } catch is CancellationError {
            guard revision == loadRevision else { return }
            disputePagination.cancel(request)
        } catch {
            guard revision == loadRevision, expectedStatus == selectedStatus else { return }
            disputePagination.fail(request, error: vouchaError(from: error))
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
