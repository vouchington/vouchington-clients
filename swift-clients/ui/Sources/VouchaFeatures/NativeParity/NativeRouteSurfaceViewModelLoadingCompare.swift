import VouchaAPI

extension NativeRouteSurfaceViewModel {
    func loadCompareRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if let lhs = routeMatch?.param("slugA"), let rhs = routeMatch?.param("slugB") {
            return try await loadTopicCompareRows(client: client, slugs: [lhs, rhs])
        }

        if routeMatch?.path == "/domains/compare",
           let ids = routeMatch?.queryValue("ids")?.split(separator: ",").map(String.init),
           !ids.isEmpty {
            return try await loadHostnameCompareRows(client: client, ids: ids)
        }

        async let topics: NativeGenericListResponse = client.send(
            Endpoint(.GET, path: "/api/v1/topics", queryItems: [.init(name: "limit", value: "5")])
        )
        async let domains: NativeGenericListResponse = client.send(
            Endpoint(.GET, path: "/api/v1/hostnames", queryItems: [.init(name: "limit", value: "5")])
        )
        let loadedTopics = try await topics
        let loadedDomains = try await domains

        return [
            row(
                "tag",
                appText(.nativeSwiftNavigationTitlesTopics),
                countText(loadedTopics.results.count, item: "topic")
            ),
            row(
                "globe",
                appText(.nativeSwiftRouteMetadataMainDomainsBrowseDomainsTitle),
                countText(loadedDomains.results.count, item: "domain")
            )
        ]
    }
}
