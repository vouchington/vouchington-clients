import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public extension ModerationReportsViewModel {
    func load() async {
        await reload(reset: true, allowClusterFallback: true)
    }

    func updateStatus(_ value: ModerationReportStatus) async {
        guard !isQueueActionInProgress, value != status else { return }
        status = value
        await resetAndReload()
    }

    func updateMode(_ value: ModerationReportsMode) async {
        guard !isQueueActionInProgress else { return }
        let allowedValue = viewerTier.isStaff ? value : .flat
        guard allowedValue != mode else { return }
        mode = allowedValue
        await resetAndReload()
    }

    func updateSort(_ value: ModerationReportSort) async {
        guard !isQueueActionInProgress else { return }
        let allowedValue = viewerTier.isStaff ? value : .createdAtDesc
        guard allowedValue != sort else { return }
        sort = allowedValue
        await resetAndReload()
    }

    func loadMore() async {
        guard let client, canLoadMore, let request = beginReportPage(reset: false) else { return }
        let requestGeneration = generation
        paginationError = nil
        do {
            if viewerTier.isStaff, mode == .grouped {
                let response: StaffClusteredModerationReportsResponse = try await client.send(
                    .clusteredModerationReports(status: status, after: request.cursor)
                )
                try Task.checkCancellation()
                guard requestGeneration == generation else { return }
                clusters = deduplicated(clusters + response.clusters, id: \.id)
                duplicateClusters = mergedDuplicateClusters(duplicateClusters, response.duplicateClusters)
                groupedLoadedPageCount += 1
                applyPageInfo(response.pageInfo)
            } else if viewerTier.isStaff {
                let response: StaffFlatModerationReportsResponse = try await client.send(
                    .moderationReports(status: status, sort: sort, after: request.cursor)
                )
                guard requestGeneration == generation else { return }
                staffReports = deduplicated(staffReports + response.reports, id: \.id)
                applyPageInfo(response.pageInfo)
            } else {
                let response: MemberFlatModerationReportsResponse = try await client.send(
                    .moderationReports(status: status, sort: .createdAtDesc, after: request.cursor)
                )
                guard requestGeneration == generation else { return }
                memberReports = deduplicated(memberReports + response.reports, id: \.id)
                applyPageInfo(response.pageInfo)
            }
        } catch {
            guard requestGeneration == generation else { return }
            failReportPage(request, error: error)
            paginationError = message(for: error)
        }
    }

    private func resetAndReload() async {
        generation += 1
        selectedReportIds = []
        reportPagination.reset()
        resetReportQueueScope()
        await reload(reset: true, allowClusterFallback: true)
    }

    private func reload(reset: Bool, allowClusterFallback: Bool) async {
        guard let client else {
            loadError = .message(.nativeSwiftModerationReportsSignInReviewReports)
            return
        }
        generation += 1
        let requestGeneration = generation
        guard beginReportPage(reset: reset) != nil else { return }
        isLoading = true
        loadError = nil
        paginationError = nil
        if reset {
            notice = nil
        }
        defer {
            if requestGeneration == generation {
                isLoading = false
            }
        }
        do {
            if viewerTier.isStaff, mode == .grouped {
                let response: StaffClusteredModerationReportsResponse = try await client.send(
                    .clusteredModerationReports(status: status)
                )
                guard requestGeneration == generation else { return }
                clusters = response.clusters
                duplicateClusters = response.duplicateClusters
                staffReports = []
                groupedLoadedPageCount = 1
                applyPageInfo(response.pageInfo)
                acceptAuthoritativeReports()
            } else if viewerTier.isStaff {
                try await loadStaffFlat(client: client, generation: requestGeneration)
            } else {
                let response: MemberFlatModerationReportsResponse = try await client.send(
                    .moderationReports(status: status, sort: .createdAtDesc)
                )
                guard requestGeneration == generation else { return }
                memberReports = response.reports
                applyPageInfo(response.pageInfo)
            }
        } catch {
            guard requestGeneration == generation else { return }
            await handleReloadFailure(error, allowClusterFallback: allowClusterFallback)
        }
    }

    private func handleReloadFailure(_ error: Error, allowClusterFallback: Bool) async {
        guard viewerTier.isStaff, mode == .grouped, allowClusterFallback else {
            failActiveReportPage(error)
            loadError = message(for: error)
            return
        }
        cancelActiveReportPage()
        mode = .flat
        resetReportQueueScope()
        notice = .message(.nativeSwiftModerationReportsGroupedUnavailable)
        await reload(reset: false, allowClusterFallback: false)
    }

    private func loadStaffFlat(client: APIClient, generation requestGeneration: Int) async throws {
        let response: StaffFlatModerationReportsResponse = try await client.send(
            .moderationReports(status: status, sort: sort)
        )
        guard requestGeneration == generation else { return }
        staffReports = response.reports
        clusters = []
        duplicateClusters = []
        groupedLoadedPageCount = 0
        applyPageInfo(response.pageInfo)
        acceptAuthoritativeReports()
    }

    internal func applyPageInfo(_ pageInfo: Page<some Decodable & Sendable>.PageInfo) {
        if let request = activeReportPageRequest, reportPagination.isCurrent(request) {
            reportPagination.complete(
                request,
                items: [.init(id: pageInfo.endCursor ?? "terminal-\(generation)")],
                endCursor: pageInfo.endCursor,
                hasNextPage: pageInfo.hasNextPage
            )
            activeReportPageRequest = nil
        } else {
            reportPagination.restoreContinuation(
                endCursor: pageInfo.endCursor,
                hasMore: pageInfo.hasNextPage
            )
        }
    }

    private func beginReportPage(reset: Bool) -> CursorPageRequest? {
        if reset {
            reportPagination.reset()
        }
        let request = reportPagination.beginNextPage()
        activeReportPageRequest = request
        return request
    }

    private func cancelActiveReportPage() {
        guard let request = activeReportPageRequest else { return }
        reportPagination.cancel(request)
        activeReportPageRequest = nil
    }

    private func failActiveReportPage(_ error: Error) {
        guard let request = activeReportPageRequest else { return }
        failReportPage(request, error: error)
    }

    private func failReportPage(_ request: CursorPageRequest, error: Error) {
        let paginationError = (error as? VouchaError) ?? .api(statusCode: 0, preconditionCode: nil)
        reportPagination.fail(request, error: paginationError)
        if activeReportPageRequest == request {
            activeReportPageRequest = nil
        }
    }

    internal func deduplicated<T, ID: Hashable>(_ values: [T], id: KeyPath<T, ID>) -> [T] {
        var seen: Set<ID> = []
        return values.filter { seen.insert($0[keyPath: id]).inserted }
    }
}
