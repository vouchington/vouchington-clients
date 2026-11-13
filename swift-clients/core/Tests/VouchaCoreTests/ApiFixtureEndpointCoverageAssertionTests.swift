@testable import VouchaAPI
import XCTest

extension ApiFixtureEndpointCoverageTests {
    func testRegisteredFixtureRoutesMatchSwiftEndpointBuilders() throws {
        for (fixtureId, endpoint) in Self.registry {
            let fixture = try XCTUnwrap(
                ApiFixtureLoader.swiftRouteFixtures.first { $0.id == fixtureId },
                "\(fixtureId) is not a Swift-consumed route fixture."
            )

            XCTAssertEqual(endpoint.method.rawValue, fixture.method)
            XCTAssertEqual(endpoint.path, fixture.path)
            XCTAssertFalse(fixture.routeTemplate.isEmpty, "\(fixtureId) route template is required")
            XCTAssertEqual(
                try requestBodyJSON(from: endpoint),
                fixture.requestBody,
                "\(fixtureId) request body mismatch"
            )
            let queryNames = endpoint.queryItems.map(\.name)
            XCTAssertEqual(
                Set(queryNames).count,
                queryNames.count,
                "\(fixtureId) endpoint query items must not contain duplicate names"
            )
            let query = Dictionary(
                endpoint.queryItems.map { ($0.name, $0.value ?? "") },
                uniquingKeysWith: { first, _ in first }
            )
            XCTAssertEqual(query, fixture.query, "\(fixtureId) query mismatch")
        }
    }

    func testEverySwiftRouteFixtureIsRegistered() {
        let routeFixtures = ApiFixtureLoader.swiftRouteFixtures
        let consumed = Set(routeFixtures.map(\.id))
        let accountedFor = Set(Self.registry.keys)

        XCTAssertEqual(
            ApiFixtureCoveragePolicy.unregistered(
                routeFixtures.map { FixtureCoverageCandidate(id: $0.id, pending: $0.pending) },
                registered: accountedFor
            ),
            [],
            "Route-bearing fixtures consumed by Swift need endpoint coverage."
        )
        XCTAssertEqual(
            accountedFor.subtracting(consumed).sorted(),
            [],
            "ApiFixtureEndpointCoverageTests.registry has entries no longer consumed by Swift."
        )
    }
}
