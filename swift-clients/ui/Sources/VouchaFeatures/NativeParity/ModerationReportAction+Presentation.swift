import VouchaLocalization

extension ModerationReportAction {
    var confirmationTitleKey: UiMessageKey {
        switch self {
        case .review: .nativeSwiftModerationReportsMarkReviewed
        case .dismiss: .nativeSwiftModerationReportsDismissReport
        case .rerunJudgement: .nativeSwiftModerationReportsRerunJudgement
        case .warn: .nativeSwiftModerationReportsIssueWarning
        case .confirmBanEvasion: .nativeSwiftModerationReportsConfirmBanEvasion
        case .dismissBanEvasion: .nativeSwiftModerationReportsDismissBanEvasion
        case .removeTarget: .nativeSwiftModerationReportsRemoveContent
        }
    }
}
