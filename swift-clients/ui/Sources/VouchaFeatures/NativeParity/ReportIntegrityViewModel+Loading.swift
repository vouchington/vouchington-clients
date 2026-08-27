import VouchaModels

extension ReportIntegrityViewModel {
    func load() async {
        guard canAccess, let service else { return }
        loadGeneration += 1
        let generation = loadGeneration
        isLoading = true
        isLoadingMore = false
        initialErrorMessage = nil
        continuationErrorMessage = nil
        do {
            let response = try await service.flags(status: selectedStatus.apiStatus, after: nil, limit: 25)
            guard generation == loadGeneration else { return }
            flags = response.results
            endCursor = response.pageInfo.endCursor
            hasMore = response.pageInfo.hasNextPage
        } catch is CancellationError {
            guard generation == loadGeneration else { return }
        } catch {
            guard generation == loadGeneration else { return }
            initialErrorMessage = integrityErrorMessage(error)
        }
        guard generation == loadGeneration else { return }
        isLoading = false
    }

    func selectStatus(_ status: IntegrityStatusFilter) async {
        guard selectedStatus != status else { return }
        selectedStatus = status
        flags = []
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
        continuationErrorMessage = nil
        do {
            let response = try await service.flags(status: status.apiStatus, after: endCursor, limit: 25)
            guard generation == loadGeneration, status == selectedStatus else { return }
            appendUnique(response.results)
            self.endCursor = response.pageInfo.endCursor
            hasMore = response.pageInfo.hasNextPage
        } catch is CancellationError {
            guard generation == loadGeneration else { return }
        } catch {
            guard generation == loadGeneration, status == selectedStatus else { return }
            continuationErrorMessage = integrityErrorMessage(error)
        }
        guard generation == loadGeneration else { return }
        isLoadingMore = false
    }

    private func appendUnique(_ additions: [ReportIntegrityFlag]) {
        var ids = Set(flags.map(\.id))
        flags.append(contentsOf: additions.filter { ids.insert($0.id).inserted })
    }
}
