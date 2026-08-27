extension NativeRouteDestinationIdentifier {
    var staffModerationNativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .moderationReports:
            [
                row(
                    "flag",
                    .nativeSwiftRouteMetadataStaffModerationReportsReportsTitle,
                    .nativeSwiftRouteMetadataStaffModerationReportsReportsDescription
                ),
                row(
                    "checklist",
                    .nativeSwiftRouteMetadataStaffModerationReportsQueueTitle,
                    .nativeSwiftRouteMetadataStaffModerationReportsQueueDescription
                )
            ]
        case .moderationAppeals:
            [
                row(
                    "arrow.uturn.left.circle",
                    .nativeSwiftRouteMetadataStaffModerationAppealsAppealsTitle,
                    .nativeSwiftRouteMetadataStaffModerationAppealsAppealsDescription
                ),
                row(
                    "person.crop.circle.badge.questionmark",
                    .nativeSwiftRouteMetadataStaffModerationAppealsAppealContextTitle,
                    .nativeSwiftRouteMetadataStaffModerationAppealsAppealContextDescription
                )
            ]
        case .moderationDisputes:
            [
                row(
                    "exclamationmark.bubble",
                    .nativeSwiftRouteMetadataStaffModerationDisputesDisputesTitle,
                    .nativeSwiftRouteMetadataStaffModerationDisputesDisputesDescription
                ),
                row(
                    "scale.3d",
                    .nativeSwiftRouteMetadataStaffModerationDisputesResolutionTitle,
                    .nativeSwiftRouteMetadataStaffModerationDisputesResolutionDescription
                )
            ]
        case .moderationReviewQueue:
            [
                row(
                    "doc.text.magnifyingglass",
                    .nativeSwiftRouteMetadataStaffModerationReviewQueuePostReviewQueueTitle,
                    .nativeSwiftRouteMetadataStaffModerationReviewQueuePostReviewQueueDescription
                ),
                row(
                    "checkmark.seal",
                    .nativeSwiftRouteMetadataStaffModerationReviewQueueDecisionTitle,
                    .nativeSwiftRouteMetadataStaffModerationReviewQueueDecisionDescription
                )
            ]
        case .moderationAdmin:
            [
                row(
                    "clock.arrow.circlepath",
                    .nativeSwiftRouteMetadataStaffModerationAdminModLogTitle,
                    .nativeSwiftRouteMetadataStaffModerationAdminModLogDescription
                ),
                row(
                    "chart.xyaxis.line",
                    .nativeSwiftRouteMetadataStaffModerationAdminAnalyticsTitle,
                    .nativeSwiftRouteMetadataStaffModerationAdminAnalyticsDescription
                )
            ]
        case .moderationIntegrity:
            [
                row(
                    "shield.lefthalf.filled",
                    .nativeSwiftRouteMetadataStaffModerationIntegrityIntegrityFlagsTitle,
                    .nativeSwiftRouteMetadataStaffModerationIntegrityIntegrityFlagsDescription
                ),
                row(
                    "flag.slash",
                    .nativeSwiftRouteMetadataStaffModerationIntegrityAbuseSignalsTitle,
                    .nativeSwiftRouteMetadataStaffModerationIntegrityAbuseSignalsDescription
                )
            ]
        default:
            []
        }
    }
}
