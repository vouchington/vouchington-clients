import XCTest

final class ApiFixtureCoveragePolicyTests: XCTestCase {
    private struct Failure: Error {}

    private let manifest = Data("""
    {"fixtures": [
      {"id": "a.plain", "method": "GET", "path": "/a", "consumers": ["swift-core"], "status": 200,
       "bodyFile": "a.json", "route": {"routeTemplate": "/a"}},
      {"id": "b.pending", "method": "GET", "path": "/b", "consumers": ["swift-core"], "status": 200,
       "bodyFile": "b.json", "route": {"routeTemplate": "/b"}, "pending": true},
      {"id": "c.explicit-false", "method": "GET", "path": "/c", "consumers": ["swift-ui"], "status": 200,
       "bodyFile": "c.json", "pending": false},
      {"id": "d.other-client", "method": "GET", "path": "/d", "consumers": ["dotnet-core"], "status": 200,
       "bodyFile": "d.json", "pending": true}
    ]}
    """.utf8)

    func testManifestDecodingTreatsMissingPendingAsFalse() throws {
        let entries = try ApiFixtureLoader.decodeManifest(manifest)

        XCTAssertEqual(entries.map(\.isPending), [false, true, false, true])
        XCTAssertNil(entries[0].pending)
    }

    func testBodyAndRouteFixturesCarryThePendingFlag() throws {
        let entries = try ApiFixtureLoader.decodeManifest(manifest)

        XCTAssertEqual(ApiFixtureLoader.swiftBodyFixtures(in: entries), [
            FixtureCoverageCandidate(id: "a.plain", pending: false),
            FixtureCoverageCandidate(id: "b.pending", pending: true),
            FixtureCoverageCandidate(id: "c.explicit-false", pending: false)
        ])
        XCTAssertEqual(
            ApiFixtureLoader.swiftRouteFixtures(in: entries).map { [$0.id, String($0.pending)] },
            [["a.plain", "false"], ["b.pending", "true"]]
        )
    }

    func testUnregisteredPendingFixtureDoesNotFailTheGate() throws {
        let entries = try ApiFixtureLoader.decodeManifest(manifest)
        let unaccounted = ApiFixtureCoveragePolicy.unregistered(
            ApiFixtureLoader.swiftBodyFixtures(in: entries),
            registered: ["a.plain", "c.explicit-false"]
        )

        XCTAssertEqual(unaccounted, [])
    }

    func testUnregisteredNonPendingFixtureStillFailsTheGate() throws {
        let entries = try ApiFixtureLoader.decodeManifest(manifest)
        let unaccounted = ApiFixtureCoveragePolicy.unregistered(
            ApiFixtureLoader.swiftBodyFixtures(in: entries),
            registered: ["b.pending"]
        )

        XCTAssertEqual(unaccounted, ["a.plain", "c.explicit-false"])
    }

    func testRegisteredPendingFixtureKeepsItsRoundTripChecks() {
        let registry = [
            RegisteredFixture(id: "b.pending") { _ in throw Failure() },
            RegisteredFixture(id: "a.plain") { _ in }
        ]
        let candidates = [FixtureCoverageCandidate(id: "b.pending", pending: true)]

        XCTAssertEqual(ApiFixtureCoveragePolicy.unregistered(candidates, registered: ["b.pending"]), [])
        let failures = ApiFixtureCoveragePolicy.roundTripFailures(registry)
        XCTAssertEqual(failures.count, 1)
        XCTAssertTrue(failures[0].hasPrefix("b.pending:"))
    }
}
