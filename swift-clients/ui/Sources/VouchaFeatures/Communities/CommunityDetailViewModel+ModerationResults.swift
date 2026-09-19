import Foundation
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadModerationResults(postId: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty, let client else { return }
        moderationResultsRequestRevision += 1
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
            moderationResults = [
                .init(
                    icon: "checkmark.shield",
                    title: UiMessage(.nativeModerationSummaryTitle),
                    detail: UiMessage(platformModerationStatusKey(response.platformModeration.status))
                )
            ]
        } catch {
            guard !Task.isCancelled,
                  requestRevision == moderationResultsRequestRevision,
                  isCurrentCommunityLoad(communityRevision, tab: .moderation)
            else { return }
            moderationResults = []
        }
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
