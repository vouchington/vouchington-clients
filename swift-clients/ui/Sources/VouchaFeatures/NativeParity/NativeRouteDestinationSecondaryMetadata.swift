extension NativeRouteDestinationIdentifier {
    var secondaryNativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .domainsBrowse:
            [
                row(
                    "globe",
                    .nativeSwiftRouteMetadataMainDomainsBrowseDomainsTitle,
                    .nativeSwiftRouteMetadataMainDomainsBrowseDomainsDescription
                ),
                row(
                    "bookmark",
                    .nativeSwiftRouteMetadataMainDomainsBrowseSavedDomainsTitle,
                    .nativeSwiftRouteMetadataMainDomainsBrowseSavedDomainsDescription
                )
            ]
        case .domainDetail:
            [
                row(
                    "globe",
                    .nativeSwiftRouteMetadataMainDomainDetailDomainDetailTitle,
                    .nativeSwiftRouteMetadataMainDomainDetailDomainDetailDescription
                ),
                row(
                    "arrow.left.and.right",
                    .nativeSwiftRouteMetadataMainDomainDetailCompareTitle,
                    .nativeSwiftRouteMetadataMainDomainDetailCompareDescription
                )
            ]
        case .urlsBrowse:
            [
                row(
                    "link",
                    .nativeSwiftRouteMetadataMainUrlsBrowseUrlsTitle,
                    .nativeSwiftRouteMetadataMainUrlsBrowseUrlsDescription
                ),
                row(
                    "line.3.horizontal.decrease.circle",
                    .nativeSwiftRouteMetadataMainUrlsBrowseUrlFiltersTitle,
                    .nativeSwiftRouteMetadataMainUrlsBrowseUrlFiltersDescription
                )
            ]
        case .urlDetail:
            [
                row(
                    "link.circle",
                    .nativeSwiftRouteMetadataMainUrlDetailUrlDetailTitle,
                    .nativeSwiftRouteMetadataMainUrlDetailUrlDetailDescription
                ),
                row(
                    "bookmark",
                    .nativeSwiftRouteMetadataMainUrlDetailSavedStateTitle,
                    .nativeSwiftRouteMetadataMainUrlDetailSavedStateDescription
                )
            ]
        case .usersBrowse:
            [
                row(
                    "person.2",
                    .nativeSwiftRouteMetadataMainUsersBrowseUsersTitle,
                    .nativeSwiftRouteMetadataMainUsersBrowseUsersDescription
                ),
                row(
                    "person.badge.plus",
                    .nativeSwiftRouteMetadataMainUsersBrowseRelationshipsTitle,
                    .nativeSwiftRouteMetadataMainUsersBrowseRelationshipsDescription
                )
            ]
        case .userProfile:
            [
                row(
                    "person.circle",
                    .nativeSwiftRouteMetadataMainUserProfileUserProfileTitle,
                    .nativeSwiftRouteMetadataMainUserProfileUserProfileDescription
                ),
                row(
                    "link",
                    .nativeSwiftRouteMetadataMainUserProfilePublicLinksTitle,
                    .nativeSwiftRouteMetadataMainUserProfilePublicLinksDescription
                )
            ]
        case .communitiesBrowse:
            [
                row(
                    "person.3",
                    .nativeSwiftRouteMetadataMainCommunitiesBrowseCommunitiesTitle,
                    .nativeSwiftRouteMetadataMainCommunitiesBrowseCommunitiesDescription
                ),
                row(
                    "person.badge.plus",
                    .nativeSwiftRouteMetadataMainCommunitiesBrowseMembershipsTitle,
                    .nativeSwiftRouteMetadataMainCommunitiesBrowseMembershipsDescription
                )
            ]
        case .communityDetail:
            [
                row(
                    "person.3.fill",
                    .nativeSwiftRouteMetadataMainCommunityDetailCommunityDetailTitle,
                    .nativeSwiftRouteMetadataMainCommunityDetailCommunityDetailDescription
                ),
                row(
                    "newspaper",
                    .nativeSwiftRouteMetadataMainCommunityDetailCommunityFeedsTitle,
                    .nativeSwiftRouteMetadataMainCommunityDetailCommunityFeedsDescription
                )
            ]
        case .messages:
            [
                row(
                    "message",
                    .nativeSwiftRouteMetadataMainMessagesMessagesTitle,
                    .nativeSwiftRouteMetadataMainMessagesMessagesDescription
                ),
                row(
                    "bubble.left.and.bubble.right",
                    .nativeSwiftRouteMetadataMainMessagesThreadTitle,
                    .nativeSwiftRouteMetadataMainMessagesThreadDescription
                )
            ]
        case .chat:
            [
                row(
                    "bubble.left.and.bubble.right",
                    .nativeSwiftRouteMetadataMainChatChatTitle,
                    .nativeSwiftRouteMetadataMainChatChatDescription
                ),
                row(
                    "plus.message",
                    .nativeSwiftRouteMetadataMainChatNewChatTitle,
                    .nativeSwiftRouteMetadataMainChatNewChatDescription
                )
            ]
        case .notifications:
            [
                row(
                    "bell",
                    .nativeSwiftRouteMetadataMainNotificationsNotificationsTitle,
                    .nativeSwiftRouteMetadataMainNotificationsNotificationsDescription
                ),
                row(
                    "checkmark.circle",
                    .nativeSwiftRouteMetadataMainNotificationsMarkReadTitle,
                    .nativeSwiftRouteMetadataMainNotificationsMarkReadDescription
                )
            ]
        default: settingsNativeRows
        }
    }
}
