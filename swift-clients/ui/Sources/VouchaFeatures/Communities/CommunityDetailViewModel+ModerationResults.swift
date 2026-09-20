import Foundation
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
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
        do {
            let response: CommunityModerationResultsResponse = try await client.send(
                .communityModerationResults(idOrSlug: slug, postId: postId)
            )
            guard !Task.isCancelled,
                  requestRevision == moderationResultsRequestRevision,
                  isCurrentCommunityLoad(communityRevision, tab: .moderation)
            else { return }
            moderationResults = moderationResultRows(for: response)
            moderationResultsError = nil
        } catch {
            guard !Task.isCancelled,
                  requestRevision == moderationResultsRequestRevision,
                  isCurrentCommunityLoad(communityRevision, tab: .moderation)
            else { return }
            moderationResults = []
            moderationResultsError = UiMessage(.nativeSwiftEmptyStateUnableToLoad)
        }
    }

    private func moderationResultRows(
        for response: CommunityModerationResultsResponse
    ) -> [NativeRouteDestinationRow] {
        [
            NativeRouteDestinationRow(
                id: "community-agent-moderations",
                icon: "sparkles",
                title: .app(UiMessage(.nativeSwiftCommunitiesAiAgents)),
                detail: .count(response.communityAgentModerations.count, item: "result")
            ),
            NativeRouteDestinationRow(
                id: "platform-moderation",
                icon: "checkmark.shield",
                title: .app(UiMessage(.nativeModerationSummaryTitle)),
                detail: .app(UiMessage(platformModerationStatusKey(response.platformModeration.status)))
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
