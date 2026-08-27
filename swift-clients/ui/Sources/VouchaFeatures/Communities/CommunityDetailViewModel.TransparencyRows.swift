import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

struct CommunityTransparencyContext {
    let revision: Int
    let tab: CommunitySurfaceTab
    let range: ModerationTransparencyRange
}

private struct CommunityTransparencyContinuation {
    let context: CommunityTransparencyContext
    let token: Int
    let after: String
}

extension CommunityDetailViewModel {
    func loadCommunityModerationTransparencyRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let context = CommunityTransparencyContext(
            revision: communityLoadRevision,
            tab: selectedTab,
            range: moderationTransparencyRange
        )
        let rows = try await loadModerationTransparencyRows(
            client: client,
            context: context
        )
        guard ownsCommunityModerationTransparencyRequest(context) else { return [] }
        moderationAnalyticsRows = []
        return rows
    }

    func loadInitialModerationTransparencyRows(
        client: APIClient,
        context: CommunityTransparencyContext
    ) async throws -> [NativeRouteDestinationRow] {
        try await loadModerationTransparencyRows(client: client, context: context)
    }

    func loadOlderCommunityModerationTransparency() async {
        let context = CommunityTransparencyContext(
            revision: communityLoadRevision,
            tab: selectedTab,
            range: moderationTransparencyRange
        )
        guard selectedTab == .moderationAnalytics,
              context.range == .all,
              let after = moderationTransparencyNextCursor,
              let client,
              !moderationTransparencyIsLoadingOlder
        else { return }
        moderationTransparencyContinuationToken += 1
        let request = CommunityTransparencyContinuation(
            context: context,
            token: moderationTransparencyContinuationToken,
            after: after
        )
        moderationTransparencyIsLoadingOlder = true
        moderationTransparencyLoadMoreError = nil
        defer {
            if ownsCommunityModerationTransparencyContinuationLifecycle(request) {
                moderationTransparencyIsLoadingOlder = false
            }
        }
        do {
            let transparency: ModerationTransparency = try await client.send(
                .communityModerationTransparency(
                    idOrSlug: slug,
                    range: context.range.rawValue,
                    after: after
                )
            )
            applyCommunityModerationTransparencyContinuation(transparency, request: request)
        } catch let error as VouchaError {
            guard ownsCommunityModerationTransparencyContinuationResponse(request) else { return }
            switch error {
            case .forbidden, .notFound:
                applyCommunityModerationTransparencyDenial(error)
                summary.rows = moderationAnalyticsRows + communityModerationTransparencyLockedRows()
            default:
                moderationTransparencyLoadMoreError = UiMessage(.nativeSwiftRouteSurfaceLoadMoreFailed)
            }
        } catch {
            guard ownsCommunityModerationTransparencyContinuationResponse(request) else { return }
            moderationTransparencyLoadMoreError = UiMessage(.nativeSwiftRouteSurfaceLoadMoreFailed)
        }
    }

    private func loadModerationTransparencyRows(
        client: APIClient,
        context: CommunityTransparencyContext
    ) async throws -> [NativeRouteDestinationRow] {
        moderationTransparencyContinuationToken += 1
        moderationTransparencyIsLoadingOlder = false
        do {
            let transparency: ModerationTransparency = try await client.send(
                .communityModerationTransparency(idOrSlug: slug, range: context.range.rawValue)
            )
            guard ownsCommunityModerationTransparencyRequest(context) else { return [] }
            moderationTransparencyBuckets = transparency.buckets
            moderationTransparencyNextCursor = transparency.nextCursor
            moderationTransparencyLoadMoreError = nil
            return moderationTransparencyRows(moderationTransparencyBuckets, range: context.range)
        } catch let error as VouchaError {
            guard ownsCommunityModerationTransparencyRequest(context) else { return [] }
            switch error {
            case .forbidden, .notFound: break
            default: throw error
            }
            applyCommunityModerationTransparencyDenial(error)
            return communityModerationTransparencyLockedRows()
        }
    }

    func ownsCommunityModerationTransparencyRequest(
        _ context: CommunityTransparencyContext
    ) -> Bool {
        isCurrentCommunityLoad(context.revision, tab: context.tab)
            && moderationTransparencyRange == context.range
    }

    private func ownsCommunityModerationTransparencyContinuationLifecycle(
        _ request: CommunityTransparencyContinuation
    ) -> Bool {
        ownsCommunityModerationTransparencyRequest(request.context)
            && request.token == moderationTransparencyContinuationToken
    }

    private func ownsCommunityModerationTransparencyContinuationResponse(
        _ request: CommunityTransparencyContinuation
    ) -> Bool {
        ownsCommunityModerationTransparencyContinuationLifecycle(request)
            && moderationTransparencyNextCursor == request.after
    }

    private func applyCommunityModerationTransparencyContinuation(
        _ transparency: ModerationTransparency,
        request: CommunityTransparencyContinuation
    ) {
        guard ownsCommunityModerationTransparencyContinuationResponse(request) else { return }
        moderationTransparencyBuckets.append(contentsOf: transparency.buckets)
        moderationTransparencyNextCursor = transparency.nextCursor
        summary.rows = moderationAnalyticsRows + moderationTransparencyRows(
            moderationTransparencyBuckets,
            range: request.context.range
        )
    }

    private func applyCommunityModerationTransparencyDenial(_ error: VouchaError) {
        moderationTransparencyBuckets = []
        moderationTransparencyNextCursor = nil
        moderationTransparencyLoadMoreError = nil
        switch error {
        case .notFound:
            moderationAnalyticsRows = []
        case .forbidden where !hasDurableSiteModerationAccess:
            moderationAnalyticsRows = []
        default:
            break
        }
    }

    private func communityModerationTransparencyLockedRows() -> [NativeRouteDestinationRow] {
        [.init(
            icon: "lock",
            title: .message(.nativeSwiftCommunityRowsModerationTransparency),
            detail: .message(.nativeSwiftCommunityRowsTransparencyLocked)
        )]
    }

    func selectModerationTransparencyRange(_ range: ModerationTransparencyRange) async {
        guard moderationTransparencyRange != range else { return }
        moderationTransparencyRange = range
        await load()
    }
}
