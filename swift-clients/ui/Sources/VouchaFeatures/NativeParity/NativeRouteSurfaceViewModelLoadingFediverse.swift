import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadFediverseInstanceDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: FediverseInstanceDetailResponse = try await client.send(
            .fediverseInstance(idOrSlug: routeMatch?.param("idOrSlug") ?? routeMatch?.path.routeLastSegment ?? "")
        )
        topicDetailId = response.topic.id
        topicDetailElection = response.topicElection
        detailRelationEntityType = "topic"
        detailRelationEntityId = response.topic.id
        detailRelationIsSelfProfile = false
        let bookmarks: EntityBookmarksResponse? = await (try? client.send(.bookmarks(
            entityType: "topic",
            entityId: response.topic.id
        )))
        detailRelationBookmarks = bookmarks?.bookmarks ?? [:]

        let software = [response.instance?.software, response.instance?.nodeinfoSoftwareVersion]
            .compactMap { $0 }.joined(separator: " ")
        let registration: UiVerbatimText = switch response.instance?.openRegistrations {
        case true: appText(.nativeSwiftRouteSurfaceOpen)
        case false: appText(.nativeSwiftRouteSurfaceClosed)
        case nil: appText(.nativeSwiftRouteSurfaceUnknown)
        }
        return fediverseInstanceDetailRows(
            response: response,
            software: software,
            registration: registration,
            trust: FediverseTrustTier(election: response.hostnameElection).label
        )
    }

    private func fediverseInstanceDetailRows(
        response: FediverseInstanceDetailResponse,
        software: String,
        registration: UiVerbatimText,
        trust: UiVerbatimText
    ) -> [NativeRouteDestinationRow] {
        [
            row("server.rack", .userContent(response.topic.name), .userContent(response.topic.markdown ?? "")),
            row(
                "gearshape.2",
                appText(.nativeSwiftRouteSurfaceSoftware),
                software.isEmpty ? appText(.nativeSwiftRouteSurfaceUnclassified) : .externalProvider(software)
            ),
            row(
                "point.3.connected.trianglepath.dotted",
                appText(.nativeSwiftRouteSurfaceProtocol),
                response.instance?.protocolName.map(UiVerbatimText.protocolValue)
                    ?? appText(.nativeSwiftRouteSurfaceUnknown)
            ),
            row(
                "person.2",
                appText(.nativeSwiftRouteSurfaceUsers),
                response.instance?.totalUsers.map { .verbatim(String($0)) }
                    ?? appText(.nativeSwiftRouteSurfaceUnknown)
            ),
            row(
                "person.2.wave.2",
                appText(.nativeSwiftRouteSurfaceMonthlyActiveUsers),
                response.instance?.monthlyActiveUsers.map { .verbatim(String($0)) }
                    ?? appText(.nativeSwiftRouteSurfaceUnknown)
            ),
            row("person.badge.plus", appText(.nativeSwiftRouteSurfaceRegistrations), registration),
            row(
                "checkmark.shield",
                appText(.nativeSwiftRouteSurfaceTrust),
                trust
            )
        ]
    }
}
