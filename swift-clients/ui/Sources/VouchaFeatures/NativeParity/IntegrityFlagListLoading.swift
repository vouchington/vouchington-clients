import VouchaModels

struct IntegrityFlagPage<Flag> {
    let results: [Flag]
    let endCursor: String?
    let hasNextPage: Bool
}

private enum IntegrityFlagListMetrics {
    static let pageLimit = 25
}

private enum IntegrityFlagListError: Error {
    case missingService
}

@MainActor
protocol IntegrityFlagListLoading: AnyObject {
    associatedtype Flag: Identifiable where Flag.ID == String

    var canAccess: Bool { get }
    var hasFlagService: Bool { get }
    var selectedStatus: IntegrityStatusFilter { get set }
    var flags: [Flag] { get set }
    var hasMore: Bool { get set }
    var isLoading: Bool { get set }
    var isLoadingMore: Bool { get set }
    var initialErrorMessage: String? { get set }
    var continuationErrorMessage: String? { get set }
    var endCursor: String? { get set }
    var loadGeneration: Int { get set }

    func fetchFlagPage(after cursor: String?) async throws -> IntegrityFlagPage<Flag>
}

extension IntegrityFlagListLoading {
    func load() async {
        guard canAccess, hasFlagService else { return }
        loadGeneration += 1
        let generation = loadGeneration
        isLoading = true
        isLoadingMore = false
        initialErrorMessage = nil
        continuationErrorMessage = nil
        do {
            let response = try await fetchFlagPage(after: nil)
            guard generation == loadGeneration else { return }
            flags = response.results
            endCursor = response.endCursor
            hasMore = response.hasNextPage
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
        guard canAccess, hasFlagService, let endCursor, hasMore,
              !isLoading, !isLoadingMore else { return }
        let generation = loadGeneration
        let status = selectedStatus
        isLoadingMore = true
        continuationErrorMessage = nil
        do {
            let response = try await fetchFlagPage(after: endCursor)
            guard generation == loadGeneration, status == selectedStatus else { return }
            appendUnique(response.results)
            self.endCursor = response.endCursor
            hasMore = response.hasNextPage
        } catch is CancellationError {
            guard generation == loadGeneration else { return }
        } catch {
            guard generation == loadGeneration, status == selectedStatus else { return }
            continuationErrorMessage = integrityErrorMessage(error)
        }
        guard generation == loadGeneration else { return }
        isLoadingMore = false
    }

    private func appendUnique(_ additions: [Flag]) {
        var ids = Set(flags.map(\.id))
        flags.append(contentsOf: additions.filter { ids.insert($0.id).inserted })
    }
}

extension ReportIntegrityViewModel: IntegrityFlagListLoading {
    var hasFlagService: Bool {
        service != nil
    }

    func fetchFlagPage(after cursor: String?) async throws -> IntegrityFlagPage<ReportIntegrityFlag> {
        guard let service else { throw IntegrityFlagListError.missingService }
        let response = try await service.flags(
            status: selectedStatus.apiStatus,
            after: cursor,
            limit: IntegrityFlagListMetrics.pageLimit
        )
        return IntegrityFlagPage(
            results: response.results,
            endCursor: response.pageInfo.endCursor,
            hasNextPage: response.pageInfo.hasNextPage
        )
    }
}

extension VoteIntegrityViewModel: IntegrityFlagListLoading {
    var hasFlagService: Bool {
        service != nil
    }

    func fetchFlagPage(after cursor: String?) async throws -> IntegrityFlagPage<VoteIntegrityFlag> {
        guard let service else { throw IntegrityFlagListError.missingService }
        let response = try await service.flags(
            status: selectedStatus.apiStatus,
            after: cursor,
            limit: IntegrityFlagListMetrics.pageLimit
        )
        return IntegrityFlagPage(
            results: response.results,
            endCursor: response.pageInfo.endCursor,
            hasNextPage: response.pageInfo.hasNextPage
        )
    }
}
