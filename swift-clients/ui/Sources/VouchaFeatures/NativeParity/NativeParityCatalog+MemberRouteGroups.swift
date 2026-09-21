import VouchaLocalization

extension NativeParityCatalog {
    static let topicGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryTopicsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryTopicsSummary),
            icon: "tag",
            entries: entries(for: .topicsBrowse, .topicImportExport, .topicDetail)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryTopicAdministrationTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryTopicAdministrationSummary),
            icon: "slider.horizontal.3",
            entries: entries(for: .topicManagement),
            requiredRoles: ["administrator"]
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectorySourcesTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectorySourcesSummary),
            icon: "newspaper",
            entries: entries(for: .sourcesBrowse, .sourceImportExport, .sourceDetail)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryDomainsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryDomainsSummary),
            icon: "globe",
            entries: entries(for: .domainsBrowse, .domainDetail, .urlsBrowse, .urlDetail)
        )
    ]

    static let communityGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryCommunitiesTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryCommunitiesSummary),
            icon: "person.3",
            entries: entries(for: .communitiesBrowse, .communityDetail)
        )
    ]

    static let messageGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryMessagesTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryMessagesSummary),
            icon: "message",
            entries: entries(for: .messages)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryChatTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryChatSummary),
            icon: "bubble.left.and.bubble.right",
            entries: entries(for: .chat)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryNotificationsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryNotificationsSummary),
            icon: "bell",
            entries: entries(for: .notifications)
        )
    ]

    static let settingsGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryAccountTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryAccountSummary),
            icon: "gearshape",
            entries: entries(for: .accountSettings)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryProfileTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryProfileSummary),
            icon: "person.crop.circle",
            entries: entries(
                for: .profileSettings,
                .household,
                .paymentCards,
                .pointValuations,
                .spendingCategories,
                .rewardsProgramStatuses
            )
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryAdvancedTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryAdvancedSummary),
            icon: "slider.horizontal.3",
            entries: entries(for: .notificationSettings, .advancedSettings, .landingPages)
        )
    ]

    static let actionGroups: [NativeRouteFamilyGroup] = [
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryAppealsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryAppealsSummary),
            icon: "exclamationmark.triangle",
            entries: entries(for: .moderationCases, .moderationTransparency)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryRecommendationsTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryRecommendationsSummary),
            icon: "lightbulb",
            entries: entries(for: .topicRecommendations)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryCompareTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryCompareSummary),
            icon: "arrow.left.and.right",
            entries: entries(for: .compare)
        ),
        .init(
            title: UiMessage(.nativeSwiftRouteFamilyDirectoryCreateEditTitle),
            summary: UiMessage(.nativeSwiftRouteFamilyDirectoryCreateEditSummary),
            icon: "square.and.pencil",
            entries: entries(for: .postDetail, .postCompose)
        )
    ]
}
