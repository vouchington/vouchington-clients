extension NativeRouteDestinationIdentifier {
    var entityNativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .topicManagement:
            [
                row(
                    "slider.horizontal.3",
                    .nativeSwiftRouteMetadataMainTopicManagementTopicSettingsTitle,
                    .nativeSwiftRouteMetadataMainTopicManagementTopicSettingsDescription
                ),
                row(
                    "photo",
                    .nativeSwiftRouteMetadataMainTopicManagementImagesTitle,
                    .nativeSwiftRouteMetadataMainTopicManagementImagesDescription
                )
            ]
        case .sourcesBrowse:
            [
                row(
                    "list.bullet.rectangle",
                    .nativeSwiftRouteMetadataMainSourcesBrowseSourcesTitle,
                    .nativeSwiftRouteMetadataMainSourcesBrowseSourcesDescription
                ),
                row(
                    "plus.circle",
                    .nativeSwiftRouteMetadataMainSourcesBrowseImportExportTitle,
                    .nativeSwiftRouteMetadataMainSourcesBrowseImportExportDescription
                )
            ]
        case .sourceDetail:
            [
                row(
                    "newspaper",
                    .nativeSwiftRouteMetadataMainSourceDetailSourceDetailTitle,
                    .nativeSwiftRouteMetadataMainSourceDetailSourceDetailDescription
                ),
                row(
                    "bell",
                    .nativeSwiftRouteMetadataMainSourceDetailSubscriptionControlsTitle,
                    .nativeSwiftRouteMetadataMainSourceDetailSubscriptionControlsDescription
                )
            ]
        default: secondaryNativeRows
        }
    }
}
