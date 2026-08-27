extension CommunitySurfaceTab {
    var isParticipationTab: Bool {
        switch self {
        case .pinnedPosts, .applications, .invites:
            true
        default:
            false
        }
    }

    var isManagementTab: Bool {
        switch self {
        case .settings, .moderation, .modlog, .modmail, .moderatorVacation, .bans, .restrictions,
             .moderationAnalytics:
            true
        default:
            false
        }
    }
}
