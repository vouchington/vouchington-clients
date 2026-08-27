extension String {
    var communityRouteSegments: [String] {
        let segments = split(separator: "/", omittingEmptySubsequences: true).map(String.init)
        if segments.first == "communities", segments.count >= 2 {
            return Array(segments.dropFirst(2))
        }
        return segments
    }
}

extension [String] {
    var isCommunityModerationRoute: Bool {
        if containsUnsupportedNativeModerationRoute {
            return false
        }
        return contains("moderation")
            || contains("moderation-queue")
            || contains("moderator-stats")
            || contains("ai-agents")
            || contains("agent-prompts")
            || contains("automod")
            || Array(suffix(2)) == ["posts", "pending"]
    }

    private var containsUnsupportedNativeModerationRoute: Bool {
        contains("modlog")
            || contains("modmail")
            || contains("bans")
            || contains("restrictions")
    }
}
