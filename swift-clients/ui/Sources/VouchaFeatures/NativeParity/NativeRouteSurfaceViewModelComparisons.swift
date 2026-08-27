import VouchaAPI
import VouchaLocalization

struct NativeTopicsBootstrapResponse: Decodable {
    let results: [NativeIdentifiedResult]
    let topics: [String: NativeTopicSummary]
}

struct NativeTopicSummary: Decodable, Identifiable {
    let id: String
    let name: String
    let slug: String
    let topicType: String
}

struct NativeTopicCompareResponse: Decodable {
    let topics: [String: NativeTopicSummary]
}

struct NativeHostnamesBootstrapResponse: Decodable {
    let results: [NativeIdentifiedResult]
    let hostnames: [String: NativeHostnameSummary]
}

struct NativeHostnameSummary: Decodable, Identifiable {
    let id: String
    let hostname: String
    let topicId: String?
    let blocked: Bool
    let crawlable: Bool?
    let linkRelFollow: Bool?

    private enum CodingKeys: String, CodingKey {
        case id
        case hostname
        case topicId
        case blocked
        case crawlable
        case linkRelFollow
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        hostname = try container.decode(String.self, forKey: .hostname)
        topicId = try container.decodeIfPresent(String.self, forKey: .topicId)
        blocked = try container.decodeIfPresent(Bool.self, forKey: .blocked) ?? false
        crawlable = try container.decodeIfPresent(Bool.self, forKey: .crawlable)
        linkRelFollow = try container.decodeIfPresent(Bool.self, forKey: .linkRelFollow)
    }
}

struct NativeHostnameCompareResponse: Decodable {
    let hostnames: [String: NativeHostnameSummary]
}

extension NativeRouteSurfaceViewModel {
    func loadTopicCompareRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let bootstrap: NativeTopicsBootstrapResponse = try await client.send(
            Endpoint(
                .GET,
                path: "/api/v1/topics",
                queryItems: [.init(name: "limit", value: "2"), .init(name: "sort", value: "hot")]
            )
        )
        guard bootstrap.results.count >= 2 else { return rows }

        let topicIds = bootstrap.results.prefix(2).map(\.id)
        guard let first = bootstrap.topics[topicIds[0]], let second = bootstrap.topics[topicIds[1]] else {
            return rows
        }

        return try await loadTopicCompareRows(client: client, slugs: [first.slug, second.slug])
    }

    func loadTopicCompareRows(client: APIClient, slugs: [String]) async throws -> [NativeRouteDestinationRow] {
        guard slugs.count == 2 else { return rows }
        let compare: NativeTopicCompareResponse = try await client.send(
            Endpoint(
                .GET,
                path: "/api/v1/topics/compare",
                queryItems: [.init(name: "slugs", value: slugs.joined(separator: ","))]
            )
        )
        let topics = compare.topics.values
        guard let comparedFirst = topics.first(where: { $0.slug == slugs[0] }),
              let comparedSecond = topics.first(where: { $0.slug == slugs[1] }) else { return rows }

        return [
            row(
                "tag",
                appText(
                    .nativeSwiftRouteSurfaceValueVersusValue,
                    parameters: ["first": comparedFirst.name, "second": comparedSecond.name]
                ),
                rawText("\(comparedFirst.topicType) · \(comparedSecond.topicType)")
            ),
            row(
                "chart.bar",
                appText(.nativeSwiftRouteSurfaceTopicComparison),
                appText(.nativeSwiftRouteSurfaceLoadedFromTopicComparison)
            )
        ]
    }

    func loadHostnameCompareRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let bootstrap: NativeHostnamesBootstrapResponse = try await client.send(
            Endpoint(.GET, path: "/api/v1/hostnames/top", queryItems: [.init(name: "limit", value: "2")])
        )
        guard bootstrap.results.count >= 2 else { return rows }

        let hostnameIds = bootstrap.results.prefix(2).map(\.id)
        guard let first = bootstrap.hostnames[hostnameIds[0]], let second = bootstrap.hostnames[hostnameIds[1]] else {
            return rows
        }

        return try await loadHostnameCompareRows(client: client, ids: [first.id, second.id])
    }

    func loadHostnameCompareRows(client: APIClient, ids: [String]) async throws -> [NativeRouteDestinationRow] {
        let uniqueIds = ids.reduce(into: [String]()) { values, id in
            if !values.contains(id) {
                values.append(id)
            }
        }.prefix(10)
        guard !uniqueIds.isEmpty else { return rows }
        let compare: NativeHostnameCompareResponse = try await client.send(
            Endpoint(
                .GET,
                path: "/api/v1/hostnames/compare",
                queryItems: [.init(name: "ids", value: uniqueIds.joined(separator: ","))]
            )
        )
        let hostnames = uniqueIds.compactMap { id in
            compare.hostnames[id] ?? compare.hostnames.values.first { $0.hostname == id }
        }
        guard let comparedFirst = hostnames.first else { return rows }
        let comparedSecond = hostnames.dropFirst().first

        return [
            row(
                "globe",
                comparedSecond.map {
                    appText(
                        .nativeSwiftRouteSurfaceValueVersusValue,
                        parameters: ["first": comparedFirst.hostname, "second": $0.hostname]
                    )
                } ?? rawText(comparedFirst.hostname),
                compareTopicDetail(for: comparedFirst, second: comparedSecond)
            ),
            row(
                "chart.bar",
                appText(.nativeSwiftRouteSurfaceHostnameComparison),
                appText(.nativeSwiftRouteSurfaceLoadedFromHostnameComparison)
            )
        ]
    }

    private func compareTopicDetail(
        for first: NativeHostnameSummary,
        second: NativeHostnameSummary?
    ) -> UiVerbatimText {
        let firstDetail = first.topicId.map(UiVerbatimText.verbatim)
            ?? .message(.nativeSwiftRouteSurfaceUnassigned)
        guard let second else { return firstDetail }
        let secondDetail = second.topicId.map(UiVerbatimText.verbatim)
            ?? .message(.nativeSwiftRouteSurfaceUnassigned)
        return .joined([firstDetail, secondDetail])
    }
}
