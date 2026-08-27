import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func load() async {
        let revision = beginCommunityLoad()
        let tab = selectedTab
        guard let client else {
            finishLoadWithoutClient(revision: revision, tab: tab)
            return
        }
        do {
            if isApplicationFormRoute {
                try await loadApplicationRoute(client: client, revision: revision, tab: tab)
            } else {
                try await loadDetailRoute(client: client, revision: revision, tab: tab)
            }
        } catch {
            finishLoad(error, revision: revision, tab: tab)
        }
    }

    func isCurrentCommunityLoad(_ revision: Int, tab: CommunitySurfaceTab) -> Bool {
        communityLoadRevision == revision && selectedTab == tab
    }
}

private extension CommunityDetailViewModel {
    func loadApplicationRoute(client: APIClient, revision: Int, tab: CommunitySurfaceTab) async throws {
        let questions = try await loadApplicationQuestionsIfNeeded(client: client, tab: tab)
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        applicationQuestions = questions
        summary = CommunityWorkspaceSummary(
            title: .message(.nativeSwiftCommunitiesCommunityApplication),
            detail: .message(.nativeSwiftCommunitiesCommunityApplicationInstructions),
            rows: []
        )
        state = .loaded
    }

    func loadDetailRoute(client: APIClient, revision: Int, tab: CommunitySurfaceTab) async throws {
        let detail: CommunityResponse = try await client.send(.community(idOrSlug: slug))
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        communityDetail = detail
        let counts = try await loadVisibleListItemCounts(client: client, detail: detail)
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        listItemCounts = counts
        let tabs = visibleTabs(for: detail.community, membership: detail.membership, counts: counts)
        if !tabs.isEmpty, !tabs.contains(tab) {
            selectedTab = tabs.first ?? .posts
            await load()
            return
        }
        let questions = try await loadApplicationQuestionsIfNeeded(client: client, tab: tab)
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        applicationQuestions = questions
        try await hydrateVacationPreferenceIfControlsVisible(client: client, tab: tab, revision: revision)
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        let rows = canLoadSelectedRows(community: detail.community, membership: detail.membership)
            ? try await loadInitialRows(client: client, tab: tab, revision: revision)
            : []
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        applyLoadedDetail(detail, tabs: tabs, rows: rows)
    }

    func applyLoadedDetail(
        _ detail: CommunityResponse,
        tabs: [CommunitySurfaceTab],
        rows: [NativeRouteDestinationRow]
    ) {
        summary = CommunityWorkspaceSummary(
            title: .verbatim(detail.community.name),
            detail: .verbatim(detail.community.markdown ?? detail.community.slug),
            activity: activityText(detail.communityMetrics),
            isMember: detail.membership != nil,
            isOwner: detail.membership?.role == .owner,
            isArchived: detail.community.archivedAt != nil,
            hasPendingApplication: detail.hasPendingApplication == true,
            tabs: tabs,
            rows: rows
        )
        state = .loaded
    }

    func finishLoadWithoutClient(revision: Int, tab: CommunitySurfaceTab) {
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        state = .loaded
    }

    func finishLoad(_ error: Error, revision: Int, tab: CommunitySurfaceTab) {
        guard isCurrentCommunityLoad(revision, tab: tab) else { return }
        state = error is CancellationError || Task.isCancelled
            ? .idle
            : .error(UiMessage(.nativeSwiftCommunityStatusUnableToLoadCommunity))
    }

    func beginCommunityLoad() -> Int {
        communityLoadRevision += 1
        modmailLoadRevision += 1
        modmailPageRequestRevision += 1
        isLoadingMoreModmail = false
        modmailPaginationError = nil
        modmailEndCursor = nil
        modmailRowIds = []
        modmailThreadPagination.reset()
        rowPagination.reset()
        automodPagination.reset()
        pendingReportPagination.reset()
        moderationTransparencyContinuationToken += 1
        moderationTransparencyBuckets = []
        moderationTransparencyNextCursor = nil
        moderationTransparencyIsLoadingOlder = false
        moderationTransparencyLoadMoreError = nil
        moderationAnalyticsRows = []
        state = .loading
        return communityLoadRevision
    }
}
