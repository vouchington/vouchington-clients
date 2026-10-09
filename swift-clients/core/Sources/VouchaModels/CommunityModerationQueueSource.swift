public enum CommunityModerationQueueSource: String, Sendable {
    case report
    case communityReview = "community_review"
    case automodFlag = "automod_flag"
}

public extension CommunityModerationQueueEntry {
    var automodFlagPostId: String? {
        guard queueSource == CommunityModerationQueueSource.automodFlag.rawValue,
              entityType == "post" else { return nil }
        let identifier = postId ?? entityId
        return identifier.isEmpty ? nil : identifier
    }
}
