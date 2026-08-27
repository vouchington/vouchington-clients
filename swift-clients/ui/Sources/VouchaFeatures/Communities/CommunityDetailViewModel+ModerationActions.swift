import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func approvePendingPost(postId: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty else { return }
        await perform(
            .approveCommunityPostReview(idOrSlug: slug, postId: postId),
            success: UiMessage(.nativeSwiftCommunityStatusApprovedPost)
        )
    }

    func rejectPendingPost(postId: String, reason: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty else { return }
        await perform(
            .rejectCommunityPostReview(
                idOrSlug: slug,
                postId: postId,
                rejectionReason: reason.trimmedOrNil ?? ""
            ),
            success: UiMessage(.nativeSwiftCommunityStatusRejectedPost)
        )
    }

    func unpublishPendingPost(postId: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty else { return }
        await perform(
            .unpublishCommunityPostReview(idOrSlug: slug, postId: postId),
            success: UiMessage(.nativeSwiftCommunityStatusUnpublishedPost)
        )
    }

    func claimModerationReport(reportId: String) async {
        let reportId = reportId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !reportId.isEmpty else { return }
        await perform(
            .claimCommunityModerationReport(idOrSlug: slug, reportId: reportId),
            success: UiMessage(.nativeSwiftCommunityStatusClaimedReport)
        )
    }

    func releaseModerationReport(reportId: String) async {
        let reportId = reportId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !reportId.isEmpty else { return }
        await perform(
            .releaseCommunityModerationReport(idOrSlug: slug, reportId: reportId),
            success: UiMessage(.nativeSwiftCommunityStatusReleasedReport)
        )
    }

    func escalateModerationReport(reportId: String) async {
        let reportId = reportId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !reportId.isEmpty else { return }
        await perform(
            .escalateCommunityModerationReport(idOrSlug: slug, reportId: reportId),
            success: UiMessage(.nativeSwiftCommunityStatusEscalatedReport)
        )
    }

    func issueCommunityWarning(
        userId: String,
        reason: String,
        publicMessage: String? = nil,
        reportId: String? = nil,
        resolveReport: Bool? = nil
    ) async {
        let userId = userId.trimmingCharacters(in: .whitespacesAndNewlines)
        let reason = reason.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !userId.isEmpty, !reason.isEmpty else { return }
        await perform(
            .createCommunityWarning(
                idOrSlug: slug,
                userId: userId,
                reason: reason,
                publicMessage: publicMessage?.trimmedOrNil,
                reportId: reportId?.trimmedOrNil,
                resolveReport: resolveReport
            ),
            success: UiMessage(.nativeSwiftCommunityStatusIssuedWarning)
        )
    }

    func claimPendingPost(postId: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty else { return }
        await perform(
            .claimCommunityPendingPost(idOrSlug: slug, postId: postId),
            success: UiMessage(.nativeSwiftCommunityStatusClaimedPost)
        )
    }

    func releasePendingPost(postId: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty else { return }
        await perform(
            .releaseCommunityPendingPost(idOrSlug: slug, postId: postId),
            success: UiMessage(.nativeSwiftCommunityStatusReleasedPost)
        )
    }

    func escalatePendingPost(postId: String) async {
        let postId = postId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !postId.isEmpty else { return }
        await perform(
            .escalateCommunityPendingPost(idOrSlug: slug, postId: postId),
            success: UiMessage(.nativeSwiftCommunityStatusEscalatedPost)
        )
    }

    func banMember(userId: String, reason: String, expiresAt: Date?) async {
        let userId = userId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !userId.isEmpty else { return }
        await perform(
            .banCommunityMember(idOrSlug: slug, userId: userId, reason: reason.trimmedOrNil, expiresAt: expiresAt),
            success: UiMessage(.nativeSwiftCommunityStatusBannedMember)
        )
    }

    func liftBan(userId: String) async {
        let userId = userId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !userId.isEmpty else { return }
        await perform(
            .liftCommunityBan(idOrSlug: slug, userId: userId),
            success: UiMessage(.nativeSwiftCommunityStatusLiftedBan)
        )
    }

    func activateRestrictions(
        restrictionTypes: [CommunityRestrictionType],
        expiresAt: Date? = nil,
        reason: String? = nil
    ) async {
        guard !restrictionTypes.isEmpty else { return }
        await perform(
            .activateCommunityRestrictions(
                idOrSlug: slug,
                restrictionTypes: restrictionTypes,
                expiresAt: expiresAt,
                reason: reason?.trimmedOrNil
            ),
            success: UiMessage(.nativeSwiftCommunityStatusActivatedRestrictions)
        )
    }

    func liftRestriction(restrictionId: String) async {
        let restrictionId = restrictionId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !restrictionId.isEmpty else { return }
        await perform(
            .liftCommunityRestriction(idOrSlug: slug, restrictionId: restrictionId),
            success: UiMessage(.nativeSwiftCommunityStatusLiftedRestriction)
        )
    }

    func sendModmailMessage(conversationId: String, text: String) async {
        let conversationId = conversationId.trimmingCharacters(in: .whitespacesAndNewlines)
        let text = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !conversationId.isEmpty, !text.isEmpty else { return }
        await perform(
            .sendCommunityModmailMessage(idOrSlug: slug, conversationId: conversationId, text: text),
            success: UiMessage(.nativeSwiftCommunityStatusSentModmailMessage)
        )
    }

    func openModmailThread(subjectUserId: String? = nil) async {
        guard let client else { return }
        state = .loading
        do {
            let response: CommunityModmailConversationResponse = try await client.send(
                .openCommunityModmailThread(idOrSlug: slug, subjectUserId: subjectUserId?.trimmedOrNil)
            )
            statusMessage = UiMessage(
                .nativeSwiftCommunityStatusOpenedModmailThread,
                parameters: ["id": response.conversation.id]
            )
            await load()
        } catch {
            state = .error(UiMessage(.nativeSwiftCommunityStatusUnableToOpenModmailThread))
        }
    }

    func resolveModmailThread(conversationId: String, resolved: Bool = true) async {
        let conversationId = conversationId.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !conversationId.isEmpty else { return }
        await perform(
            .updateCommunityModmailThread(idOrSlug: slug, conversationId: conversationId, resolved: resolved),
            success: UiMessage(
                resolved
                    ? .nativeSwiftCommunityStatusResolvedThread
                    : .nativeSwiftCommunityStatusReopenedThread
            )
        )
    }
}
