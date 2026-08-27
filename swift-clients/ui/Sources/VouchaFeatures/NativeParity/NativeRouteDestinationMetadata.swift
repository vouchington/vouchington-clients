extension NativeRouteDestinationIdentifier {
    var nativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .signIn: signInNativeRows
        case .webSearch:
            [
                row(
                    "magnifyingglass",
                    .nativeSwiftRouteMetadataMainWebSearchSearchResultsTitle,
                    .nativeSwiftRouteMetadataMainWebSearchSearchResultsDescription
                ),
                row(
                    "line.3.horizontal.decrease.circle",
                    .nativeSwiftRouteMetadataMainWebSearchFiltersTitle,
                    .nativeSwiftRouteMetadataMainWebSearchFiltersDescription
                )
            ]
        case .fediverseSearch, .fediverseInstances: fediverseNativeRows
        case .feedPosts: feedPostsNativeRows
        case .feedNews: feedNewsNativeRows
        case .feedPodcasts: feedPodcastsNativeRows
        case .feedVideos: feedVideosNativeRows
        case .feedReferralLinks: feedReferralLinksNativeRows
        case .postsBrowse:
            [
                row(
                    "doc.text",
                    .nativeSwiftRouteMetadataMainPostsBrowsePostsTitle,
                    .nativeSwiftRouteMetadataMainPostsBrowsePostsDescription
                ),
                row(
                    "line.3.horizontal.decrease.circle",
                    .nativeSwiftRouteMetadataMainPostsBrowsePostFiltersTitle,
                    .nativeSwiftRouteMetadataMainPostsBrowsePostFiltersDescription
                )
            ]
        case .postDetail:
            [
                row(
                    "doc.text.magnifyingglass",
                    .nativeSwiftRouteMetadataMainPostDetailPostDetailTitle,
                    .nativeSwiftRouteMetadataMainPostDetailPostDetailDescription
                ),
                row(
                    "bubble.left",
                    .nativeSwiftRouteMetadataMainPostDetailCommentsTitle,
                    .nativeSwiftRouteMetadataMainPostDetailCommentsDescription
                )
            ]
        case .rssFeedItemDetail:
            [
                row(
                    "newspaper",
                    .nativeSwiftRouteMetadataMainRssFeedItemDetailRssFeedItemDetailTitle,
                    .nativeSwiftRouteMetadataMainRssFeedItemDetailRssFeedItemDetailDescription
                ),
                row(
                    "tag",
                    .nativeSwiftRouteMetadataMainRssFeedItemDetailTagsTitle,
                    .nativeSwiftRouteMetadataMainRssFeedItemDetailTagsDescription
                )
            ]
        case .postCompose:
            [
                row(
                    "square.and.pencil",
                    .nativeSwiftRouteMetadataMainPostComposeComposeTitle,
                    .nativeSwiftRouteMetadataMainPostComposeComposeDescription
                ),
                row(
                    "tray.and.arrow.down",
                    .nativeSwiftRouteMetadataMainPostComposeDraftsTitle,
                    .nativeSwiftRouteMetadataMainPostComposeDraftsDescription
                )
            ]
        case .storiesBrowse:
            [
                row(
                    "photo.on.rectangle",
                    .nativeSwiftRouteMetadataMainStoriesBrowseStoriesTitle,
                    .nativeSwiftRouteMetadataMainStoriesBrowseStoriesDescription
                ),
                row(
                    "doc.richtext",
                    .nativeSwiftRouteMetadataMainStoriesBrowseStoryDetailsTitle,
                    .nativeSwiftRouteMetadataMainStoriesBrowseStoryDetailsDescription
                )
            ]
        case .topicsBrowse:
            [
                row(
                    "tag",
                    .nativeSwiftRouteMetadataMainTopicsBrowseTopicsTitle,
                    .nativeSwiftRouteMetadataMainTopicsBrowseTopicsDescription
                ),
                row(
                    "bookmark",
                    .nativeSwiftRouteMetadataMainTopicsBrowseFollowStateTitle,
                    .nativeSwiftRouteMetadataMainTopicsBrowseFollowStateDescription
                )
            ]
        case .topicImportExport:
            [
                row(
                    "square.and.arrow.up.on.square",
                    .nativeSwiftRouteSurfaceImportTopics,
                    .nativeSwiftRouteSurfaceImportTopicsDetail
                )
            ]
        case .topicDetail:
            [
                row(
                    "tag.circle",
                    .nativeSwiftRouteMetadataMainTopicDetailTopicDetailTitle,
                    .nativeSwiftRouteMetadataMainTopicDetailTopicDetailDescription
                ),
                row(
                    "lightbulb",
                    .nativeSwiftRouteMetadataMainTopicDetailRecommendationsTitle,
                    .nativeSwiftRouteMetadataMainTopicDetailRecommendationsDescription
                )
            ]
        case .sourceImportExport:
            [
                row(
                    "square.and.arrow.up.on.square",
                    .nativeSwiftRouteSurfaceImportSources,
                    .nativeSwiftRouteSurfaceImportSourcesDetail
                )
            ]
        case .household:
            [
                row(
                    "person.2",
                    .nativeSwiftRebasedRouteSurfacesHouseholdMetadataTitle,
                    .nativeSwiftRebasedRouteSurfacesHouseholdMetadataDescription
                )
            ]
        case .paymentCards:
            [
                row(
                    "creditcard",
                    .nativeSwiftSettingsCards,
                    .nativeSwiftRouteMetadataMainProfileSettingsProfileDescription
                )
            ]
        case .pointValuations:
            [
                row(
                    "chart.bar.doc.horizontal",
                    .extractedRewardsProgramPointValuationsPagePointValuationsF90821b2,
                    .extractedRewardsProgramPointValuationsPageManageYourRewardsProgramPointValuationsE3bcc429
                )
            ]
        case .spendingCategories:
            [
                row(
                    "list.bullet.rectangle",
                    .extractedSpendingCategoriesPageSpendingCategories3ed30dfb,
                    .extractedSpendingCategoriesPageManageYourSpendingCategoriesAndAmounts72fd1919
                )
            ]
        case .rewardsProgramStatuses:
            [
                row(
                    "tag",
                    .nativeSwiftSettingsRewardStatuses,
                    .nativeSwiftSettingsRewardStatuses
                )
            ]
        default: entityNativeRows
        }
    }
}
