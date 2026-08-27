extension NativeRouteDestinationIdentifier {
    var libraryNativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .referrals:
            [
                row(
                    "arrow.triangle.branch",
                    .nativeSwiftRouteMetadataLibraryReferralsReferralsTitle,
                    .nativeSwiftRouteMetadataLibraryReferralsReferralsDescription
                ),
                row(
                    "chart.line.uptrend.xyaxis",
                    .nativeSwiftRouteMetadataLibraryReferralsReferralActivityTitle,
                    .nativeSwiftRouteMetadataLibraryReferralsReferralActivityDescription
                )
            ]
        case .landingPages:
            [
                row(
                    "doc.text",
                    .nativeSwiftRouteMetadataLibraryLandingPagesLandingPagesTitle,
                    .nativeSwiftRouteMetadataLibraryLandingPagesLandingPagesDescription
                ),
                row(
                    "link",
                    .nativeSwiftRouteMetadataLibraryLandingPagesPublicRoutesTitle,
                    .nativeSwiftRouteMetadataLibraryLandingPagesPublicRoutesDescription
                )
            ]
        case .lists:
            [
                row(
                    "list.bullet.rectangle",
                    .nativeSwiftRouteMetadataLibraryListsListsTitle,
                    .nativeSwiftRouteMetadataLibraryListsListsDescription
                ),
                row(
                    "person.3.sequence",
                    .nativeSwiftRouteMetadataLibraryListsCommunitiesTitle,
                    .nativeSwiftRouteMetadataLibraryListsCommunitiesDescription
                )
            ]
        case .bookmarks:
            [
                row(
                    "bookmark",
                    .nativeSwiftRouteMetadataLibraryBookmarksBookmarksTitle,
                    .nativeSwiftRouteMetadataLibraryBookmarksBookmarksDescription
                ),
                row(
                    "line.3.horizontal.decrease.circle",
                    .nativeSwiftRouteMetadataLibraryBookmarksBookmarkFiltersTitle,
                    .nativeSwiftRouteMetadataLibraryBookmarksBookmarkFiltersDescription
                )
            ]
        case .plans:
            [
                row(
                    "creditcard",
                    .nativeSwiftRouteMetadataLibraryPlansPlansTitle,
                    .nativeSwiftRouteMetadataLibraryPlansPlansDescription
                ),
                row(
                    "checkmark.seal",
                    .nativeSwiftRouteMetadataLibraryPlansMembershipTitle,
                    .nativeSwiftRouteMetadataLibraryPlansMembershipDescription
                )
            ]
        default:
            []
        }
    }
}
