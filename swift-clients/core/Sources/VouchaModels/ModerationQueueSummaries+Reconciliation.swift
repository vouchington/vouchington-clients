public extension ModerationAppealSummary {
    func updating(from detail: ModerationAppeal) -> Self {
        guard detail.id == id else { return self }
        return Self(
            id: id, appellantUserId: appellantUserId, appellant: appellant,
            hasDraft: detail.publicResponse != nil, hasAiDraft: detail.aiDraftedAt != nil,
            recommendedAction: detail.recommendedAction,
            userWarningId: userWarningId, userSuspensionId: userSuspensionId,
            communityBanId: communityBanId, postId: postId, communityId: communityId,
            postRemovalKind: postRemovalKind, status: detail.status,
            publicResponse: detail.sentAt == nil ? nil : detail.publicResponse,
            approvedAt: detail.approvedAt, sentAt: detail.sentAt, resolvedAt: detail.resolvedAt,
            resolutionAction: detail.resolutionAction, createdAt: createdAt, updatedAt: detail.updatedAt,
            isOverdue: detail.isOverdue, targetContext: targetContext
        )
    }
}

public extension ReviewDisputeSummary {
    func updating(from detail: ReviewDispute) -> Self {
        guard detail.id == id else { return self }
        return Self(
            id: id, postId: postId, topicId: topicId, disputantUserId: disputantUserId,
            disputant: disputant, reason: reason, status: detail.status,
            hasDraft: detail.publicResponse != nil, hasAiDraft: detail.aiDraftedAt != nil,
            postLabel: postLabel, postSlug: postSlug, recommendedAction: detail.recommendedAction,
            isOverdue: detail.isOverdue == true,
            publicResponse: detail.sentAt == nil ? nil : detail.publicResponse,
            approvedAt: detail.approvedAt, sentAt: detail.sentAt, resolvedAt: detail.resolvedAt,
            resolutionAction: detail.resolutionAction, createdAt: createdAt, updatedAt: detail.updatedAt,
            postContent: postContent
        )
    }
}
