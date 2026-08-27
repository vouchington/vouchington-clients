extension NativeRouteDestinationIdentifier {
    var settingsNativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .accountSettings:
            [
                row(
                    "gearshape",
                    .nativeSwiftRouteMetadataMainAccountSettingsAccountTitle,
                    .nativeSwiftRouteMetadataMainAccountSettingsAccountDescription
                ),
                row(
                    "lock",
                    .nativeSwiftRouteMetadataMainAccountSettingsSecurityTitle,
                    .nativeSwiftRouteMetadataMainAccountSettingsSecurityDescription
                )
            ]
        case .profileSettings:
            [
                row(
                    "person.crop.circle",
                    .nativeSwiftRouteMetadataMainProfileSettingsProfileTitle,
                    .nativeSwiftRouteMetadataMainProfileSettingsProfileDescription
                ),
                row(
                    "photo",
                    .nativeSwiftRouteMetadataMainProfileSettingsProfileImageTitle,
                    .nativeSwiftRouteMetadataMainProfileSettingsProfileImageDescription
                )
            ]
        case .advancedSettings:
            [
                row(
                    "slider.horizontal.3",
                    .nativeSwiftRouteMetadataMainAdvancedSettingsAdvancedTitle,
                    .nativeSwiftRouteMetadataMainAdvancedSettingsAdvancedDescription
                ),
                row(
                    "doc.text",
                    .nativeSwiftRouteMetadataMainAdvancedSettingsLandingPagesTitle,
                    .nativeSwiftRouteMetadataMainAdvancedSettingsLandingPagesDescription
                )
            ]
        case .notificationSettings:
            [
                row(
                    "bell",
                    .nativeSwiftRouteMetadataMainNotificationsNotificationsTitle,
                    .nativeSwiftRouteMetadataMainNotificationsNotificationsDescription
                )
            ]
        case .friendRecommendations:
            [
                row(
                    "person.2.badge.plus",
                    .nativeSwiftNavigationTitlesFriends,
                    .nativeSwiftRouteMetadataMainUsersBrowseUsersDescription
                )
            ]
        case .referrals, .landingPages, .lists, .bookmarks, .plans: libraryNativeRows
        case .topicRecommendations:
            [
                row(
                    "lightbulb",
                    .nativeSwiftRouteMetadataMainTopicRecommendationsRecommendationsTitle,
                    .nativeSwiftRouteMetadataMainTopicRecommendationsRecommendationsDescription
                ),
                row(
                    "square.and.pencil",
                    .nativeSwiftRouteMetadataMainTopicRecommendationsCreateEditTitle,
                    .nativeSwiftRouteMetadataMainTopicRecommendationsCreateEditDescription
                )
            ]
        case .moderationCases:
            [
                row(
                    "exclamationmark.triangle",
                    .nativeSwiftRouteMetadataMainModerationCasesAppealsTitle,
                    .nativeSwiftRouteMetadataMainModerationCasesAppealsDescription
                ),
                row(
                    "paperplane",
                    .nativeSwiftRouteMetadataMainModerationCasesSubmitAppealTitle,
                    .nativeSwiftRouteMetadataMainModerationCasesSubmitAppealDescription
                )
            ]
        case .moderationTransparency:
            [
                row(
                    "shield",
                    .nativeSwiftCommunityRowsModerationTransparency,
                    .nativeSwiftCommunityRowsTransparencyEmpty
                )
            ]
        case .compare: comparisonNativeRows
        default: staffNativeRows
        }
    }
}
