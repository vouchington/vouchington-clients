import VouchaCore

extension IntegrityPenaltyLedgerViewModel {
    func load() async {
        guard canAccess, let service else { return }
        loadGeneration += 1
        let generation = loadGeneration
        isLoading = true
        isLoadingMore = false
        initialError = nil
        continuationError = nil
        do {
            let response = try await service.penalties(
                domain: domain, status: selectedStatus.apiStatus, after: nil, limit: 25
            )
            guard generation == loadGeneration else { return }
            penalties = response.results
            endCursor = response.pageInfo.endCursor
            hasMore = response.pageInfo.hasNextPage
        } catch is CancellationError {
            guard generation == loadGeneration else { return }
        } catch {
            guard generation == loadGeneration else { return }
            initialError = initialError(for: error)
        }
        guard generation == loadGeneration else { return }
        isLoading = false
    }

    func selectStatus(_ status: IntegrityPenaltyStatusFilter) async {
        guard selectedStatus != status else { return }
        selectedStatus = status
        penalties = []
        endCursor = nil
        hasMore = false
        await load()
    }

    func loadMore() async {
        guard canAccess, let service, let endCursor, hasMore,
              !isLoading, !isLoadingMore else { return }
        let generation = loadGeneration
        let status = selectedStatus
        isLoadingMore = true
        continuationError = nil
        do {
            let response = try await service.penalties(
                domain: domain, status: status.apiStatus, after: endCursor, limit: 25
            )
            guard generation == loadGeneration, status == selectedStatus else { return }
            appendUnique(response.results)
            self.endCursor = response.pageInfo.endCursor
            hasMore = response.pageInfo.hasNextPage
        } catch is CancellationError {
            guard generation == loadGeneration else { return }
        } catch {
            guard generation == loadGeneration, status == selectedStatus else { return }
            continuationError = error is IntegrityPenaltyServiceError ? .scopeUnavailable : .loadMoreFailed
        }
        guard generation == loadGeneration else { return }
        isLoadingMore = false
    }

    private func appendUnique(_ additions: [IntegrityPenaltyRow]) {
        var ids = Set(penalties.map(\.id))
        penalties.append(contentsOf: additions.filter { ids.insert($0.id).inserted })
    }

    private func initialError(for error: Error) -> IntegrityLedgerError {
        if error is IntegrityPenaltyServiceError {
            return .scopeUnavailable
        }
        if domain == .report, error.isHTTPStatus(404) {
            return .reportUnavailable
        }
        return .loadFailed
    }
}

private extension Error {
    func isHTTPStatus(_ expected: Int) -> Bool {
        guard let error = self as? VouchaError else { return false }
        switch error {
        case let .api(statusCode, _), let .apiMessage(statusCode, _, _): return statusCode == expected
        default: return false
        }
    }
}
