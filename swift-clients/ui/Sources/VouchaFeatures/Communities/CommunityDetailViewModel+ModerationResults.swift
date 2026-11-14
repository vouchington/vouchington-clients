import Foundation
import VouchaLocalization
import VouchaModels

private struct ModerationResultsSummary {
    let count: Int
    let status: AdminReviewQueueClearanceStatus
}

private enum ModerationResultsPagingError: Error {
    case brokenCursor
}

extension CommunityDetailViewModel {
    static let moderationResultsPageLimit = 100

    func discardModerationResults() {
        moderationResultsRequestRevision += 1
        moderationResults = []
        moderationResultsError = nil
    }

    func loadModerationResults(postId: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty else {
            discardModerationResults()
            return
        }
        guard let client else { return }
        discardModerationResults()
        let requestRevision = moderationResultsRequestRevision
        let communityRevision = communityLoadRevision
        let tab = selectedTab
        do {
            let summary = try await fetchAllModerationResultPages(postId: postId) {
                !Task.isCancelled
                    && requestRevision == self.moderationResultsRequestRevision
                    && self.isCurrentCommunityLoad(communityRevision, tab: tab)
            }
            guard let summary else { return }
            moderationResults = moderationResultRows(for: summary)
            moderationResultsError = nil
        } catch {
            guard !Task.isCancelled,
                  requestRevision == moderationResultsRequestRevision,
                  isCurrentCommunityLoad(communityRevision, tab: tab)
            else { return }
            moderationResults = []
            moderationResultsError = UiMessage(.nativeSwiftEmptyStateUnableToLoad)
        }
    }

    /// Follows every page so the count is the full total, never the default page size.
    /// Returns nil when the load went stale; throws when the cursor chain cannot be followed.
    private func fetchAllModerationResultPages(
        postId: String,
        isCurrent: () -> Bool
    ) async throws -> ModerationResultsSummary? {
        guard let client else { return nil }
        var after: String?
        var seenCursors = Set<String>()
        var total = 0
        while true {
            let response: CommunityModerationResultsResponse = try await client.send(
                .communityModerationResults(
                    idOrSlug: slug,
                    postId: postId,
                    after: after,
                    limit: Self.moderationResultsPageLimit
                )
            )
            guard isCurrent() else { return nil }
            total += response.results.count
            guard response.pageInfo.hasNextPage else {
                return ModerationResultsSummary(count: total, status: response.platformModeration.status)
            }
            guard let cursor = response.pageInfo.endCursor, seenCursors.insert(cursor).inserted else {
                throw ModerationResultsPagingError.brokenCursor
            }
            after = cursor
        }
    }

    private func moderationResultRows(for summary: ModerationResultsSummary) -> [NativeRouteDestinationRow] {
        [
            NativeRouteDestinationRow(
                id: "community-agent-moderations",
                icon: "sparkles",
                title: .app(UiMessage(.nativeSwiftCommunitiesAiAgents)),
                detail: .count(summary.count, item: "result")
            ),
            NativeRouteDestinationRow(
                id: "platform-moderation",
                icon: "checkmark.shield",
                title: .app(UiMessage(.nativeModerationSummaryTitle)),
                detail: .app(UiMessage(platformModerationStatusKey(summary.status)))
            )
        ]
    }

    private func platformModerationStatusKey(_ status: AdminReviewQueueClearanceStatus) -> UiMessageKey {
        switch status {
        case .approved:
            .nativeTaxonomyModerationApproved
        case .inReview:
            .nativeTaxonomyModerationInReview
        case .pending:
            .nativeTaxonomyModerationPending
        case .rejected:
            .nativeTaxonomyModerationRejected
        }
    }
}
