extension NativeRouteDestinationIdentifier {
    var staffNativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .crmContacts:
            [
                row(
                    "person.text.rectangle",
                    .nativeSwiftRouteMetadataStaffCrmContactsCrmContactsTitle,
                    .nativeSwiftRouteMetadataStaffCrmContactsCrmContactsDescription
                ),
                row(
                    "envelope",
                    .nativeSwiftRouteMetadataStaffCrmContactsEmailHistoryTitle,
                    .nativeSwiftRouteMetadataStaffCrmContactsEmailHistoryDescription
                )
            ]
        case .membershipGrants:
            [
                row(
                    "checkmark.seal",
                    .nativeSwiftRouteMetadataStaffMembershipGrantsMembershipGrantsTitle,
                    .nativeSwiftRouteMetadataStaffMembershipGrantsMembershipGrantsDescription
                ),
                row(
                    "person.badge.plus",
                    .nativeSwiftRouteMetadataStaffMembershipGrantsGrantAccessTitle,
                    .nativeSwiftRouteMetadataStaffMembershipGrantsGrantAccessDescription
                )
            ]
        case .userAdmin:
            [
                row(
                    "person.badge.shield.checkmark",
                    .nativeSwiftRouteMetadataStaffUserAdminIdentityVerificationTitle,
                    .nativeSwiftRouteMetadataStaffUserAdminIdentityVerificationDescription
                )
            ]
        case .supportStaffThreads:
            [
                row(
                    "tray.full",
                    .nativeSwiftRouteMetadataStaffSupportStaffThreadsSupportThreadsTitle,
                    .nativeSwiftRouteMetadataStaffSupportStaffThreadsSupportThreadsDescription
                ),
                row(
                    "bubble.left.and.bubble.right",
                    .nativeSwiftRouteMetadataStaffSupportStaffThreadsThreadDetailTitle,
                    .nativeSwiftRouteMetadataStaffSupportStaffThreadsThreadDetailDescription
                )
            ]
        case .supportStaffContacts:
            [
                row(
                    "person.crop.rectangle.stack",
                    .nativeSwiftRouteMetadataStaffSupportStaffContactsSupportContactsTitle,
                    .nativeSwiftRouteMetadataStaffSupportStaffContactsSupportContactsDescription
                ),
                row(
                    "person.text.rectangle",
                    .nativeSwiftRouteMetadataStaffSupportStaffContactsContactDetailTitle,
                    .nativeSwiftRouteMetadataStaffSupportStaffContactsContactDetailDescription
                )
            ]
        case .engineeringQueues:
            [
                row(
                    "tray.full",
                    .nativeSwiftRouteMetadataStaffEngineeringQueuesQueuesTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringQueuesQueuesDescription
                )
            ]
        case .engineeringPostgresql:
            [
                row(
                    "cylinder.split.1x2",
                    .nativeSwiftRouteMetadataStaffEngineeringPostgresqlPostgresqlTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringPostgresqlPostgresqlDescription
                ),
                row(
                    "speedometer",
                    .nativeSwiftRouteMetadataStaffEngineeringPostgresqlPerformanceTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringPostgresqlPerformanceDescription
                )
            ]
        case .engineeringValkey:
            [
                row(
                    "externaldrive.connected.to.line.below",
                    .nativeSwiftRouteMetadataStaffEngineeringValkeyValkeyTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringValkeyValkeyDescription
                ),
                row(
                    "waveform.path.ecg",
                    .nativeSwiftRouteMetadataStaffEngineeringValkeyTelemetryTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringValkeyTelemetryDescription
                )
            ]
        case .engineeringAiCosts:
            [
                row(
                    "chart.bar.xaxis",
                    .nativeSwiftRouteMetadataStaffEngineeringAiCostsAiCostsTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringAiCostsAiCostsDescription
                )
            ]
        case .engineeringDynamicConfig:
            [
                row(
                    "switch.2",
                    .nativeSwiftRouteMetadataStaffEngineeringDynamicConfigDynamicConfigTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringDynamicConfigDynamicConfigDescription
                ),
                row(
                    "slider.horizontal.3",
                    .nativeSwiftRouteMetadataStaffEngineeringDynamicConfigFlagsTitle,
                    .nativeSwiftRouteMetadataStaffEngineeringDynamicConfigFlagsDescription
                )
            ]
        case .growthDashboard:
            [
                row(
                    "chart.line.uptrend.xyaxis",
                    .nativeSwiftRouteMetadataStaffGrowthDashboardGrowthTitle,
                    .nativeSwiftRouteMetadataStaffGrowthDashboardGrowthDescription
                ),
                row(
                    "person.3.sequence",
                    .nativeSwiftRouteMetadataStaffGrowthDashboardAudienceTitle,
                    .nativeSwiftRouteMetadataStaffGrowthDashboardAudienceDescription
                )
            ]
        default:
            staffModerationNativeRows
        }
    }
}
