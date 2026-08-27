extension NativeRouteDestinationIdentifier {
    var signInNativeRows: [NativeRouteDestinationRow] {
        [
            row(
                "person.crop.circle.badge.checkmark",
                .nativeSwiftRouteMetadataMainSignInSignInTitle,
                .nativeSwiftRouteMetadataMainSignInSignInDescription
            ),
            row(
                "key",
                .nativeSwiftRouteMetadataMainSignInPasskeysTitle,
                .nativeSwiftRouteMetadataMainSignInPasskeysDescription
            )
        ]
    }

    var feedPostsNativeRows: [NativeRouteDestinationRow] {
        [
            row(
                "doc.text",
                .nativeSwiftRouteMetadataMainFeedPostsPostFeedTitle,
                .nativeSwiftRouteMetadataMainFeedPostsPostFeedDescription
            ),
            row(
                "line.3.horizontal.decrease.circle",
                .nativeSwiftRouteMetadataMainFeedPostsFiltersTitle,
                .nativeSwiftRouteMetadataMainFeedPostsFiltersDescription
            )
        ]
    }

    var feedNewsNativeRows: [NativeRouteDestinationRow] {
        [
            row(
                "newspaper",
                .nativeSwiftRouteMetadataMainFeedNewsNewsFeedTitle,
                .nativeSwiftRouteMetadataMainFeedNewsNewsFeedDescription
            ),
            row(
                "person.crop.circle.badge.checkmark",
                .nativeSwiftRouteMetadataMainFeedNewsYourFeedTitle,
                .nativeSwiftRouteMetadataMainFeedNewsYourFeedDescription
            )
        ]
    }

    var feedPodcastsNativeRows: [NativeRouteDestinationRow] {
        [
            row(
                "headphones",
                .nativeSwiftRouteMetadataMainFeedPodcastsPodcastFeedTitle,
                .nativeSwiftRouteMetadataMainFeedPodcastsPodcastFeedDescription
            ),
            row(
                "list.bullet.rectangle",
                .nativeSwiftRouteMetadataMainFeedPodcastsPodcastSourcesTitle,
                .nativeSwiftRouteMetadataMainFeedPodcastsPodcastSourcesDescription
            )
        ]
    }

    var feedVideosNativeRows: [NativeRouteDestinationRow] {
        [
            row(
                "play.rectangle",
                .nativeSwiftRouteMetadataMainFeedVideosVideoFeedTitle,
                .nativeSwiftRouteMetadataMainFeedVideosVideoFeedDescription
            ),
            row(
                "list.bullet.rectangle",
                .nativeSwiftRouteMetadataMainFeedVideosVideoSourcesTitle,
                .nativeSwiftRouteMetadataMainFeedVideosVideoSourcesDescription
            )
        ]
    }

    var feedReferralLinksNativeRows: [NativeRouteDestinationRow] {
        [
            row(
                "link",
                .nativeSwiftRouteMetadataMainFeedReferralLinksReferralFeedTitle,
                .nativeSwiftRouteMetadataMainFeedReferralLinksReferralFeedDescription
            ),
            row(
                "person.2",
                .nativeSwiftRouteMetadataMainFeedReferralLinksMutualReferralsTitle,
                .nativeSwiftRouteMetadataMainFeedReferralLinksMutualReferralsDescription
            )
        ]
    }
}
