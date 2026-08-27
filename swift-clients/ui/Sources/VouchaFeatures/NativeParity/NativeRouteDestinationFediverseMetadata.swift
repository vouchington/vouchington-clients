extension NativeRouteDestinationIdentifier {
    var fediverseNativeRows: [NativeRouteDestinationRow] {
        switch self {
        case .fediverseSearch:
            [
                row(
                    "point.3.connected.trianglepath.dotted",
                    .nativeSwiftRouteMetadataFediverseFediverseSearchFediverseSearchTitle,
                    .nativeSwiftRouteMetadataFediverseFediverseSearchFediverseSearchDescription
                ),
                row(
                    "play.rectangle",
                    .nativeSwiftRouteMetadataFediverseFediverseSearchPeertubeTitle,
                    .nativeSwiftRouteMetadataFediverseFediverseSearchPeertubeDescription
                )
            ]
        case .fediverseInstances:
            [
                row(
                    "server.rack",
                    .nativeSwiftRouteMetadataFediverseFediverseInstancesFediverseInstancesTitle,
                    .nativeSwiftRouteMetadataFediverseFediverseInstancesFediverseInstancesDescription
                ),
                row(
                    "person.2.wave.2",
                    .nativeSwiftRouteMetadataFediverseFediverseInstancesInstanceTopicsTitle,
                    .nativeSwiftRouteMetadataFediverseFediverseInstancesInstanceTopicsDescription
                )
            ]
        default:
            []
        }
    }
}
