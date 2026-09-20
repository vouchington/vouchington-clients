import Foundation
@testable import VouchaAPI
import VouchaTestSupport
import XCTest

final class EndpointManifestCoverageTests: XCTestCase {
    func testEveryManifestFixtureIsRepresentedInTheSwiftCoverageRegistry() {
        let manifestIds = Set(ManifestLoader.swiftRouteFixtures.map(\.id))
        let registeredIds = Set(EndpointManifestCoverage.registry.map(\.id))
        let duplicateIds = Dictionary(grouping: EndpointManifestCoverage.registry, by: \.id)
            .filter { $0.value.count > 1 }
            .keys.sorted()

        XCTAssertTrue(duplicateIds.isEmpty, "Duplicate Swift endpoint registrations: \(duplicateIds)")

        let unaccounted = manifestIds.subtracting(registeredIds)
        XCTAssertTrue(
            unaccounted.isEmpty,
            "Swift-consumed route fixtures not represented in the Swift coverage registry: " +
                "\(unaccounted.sorted())"
        )

        let stale = registeredIds.subtracting(Set(ManifestLoader.fixtures.map(\.id)))
        XCTAssertTrue(stale.isEmpty, "Unexpected stale manifest coverage entries: \(stale.sorted())")
    }

    func testManifestRegisteredEndpointsMatchTheirFixtureShape() throws {
        for registration in EndpointManifestCoverage.registry {
            try XCTContext.runActivity(named: registration.id) { _ in
                let fixture = try XCTUnwrap(ManifestLoader.fixture(id: registration.id))
                try assertManifestEndpoint(registration.makeEndpoint(), matches: fixture)
            }
        }
    }
}

private struct ManifestFixture {
    let id: String
    let method: String
    let path: String
    let query: [String: String]
    let requestBody: Any?
    let consumers: [String]
    let hasRoute: Bool
}

private enum ManifestLoader {
    static let fixtures: [ManifestFixture] = {
        guard
            let url = manifestURL(),
            let data = try? Data(contentsOf: url),
            let root = try? JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed]) as? [String: Any],
            let rawFixtures = root["fixtures"] as? [[String: Any]]
        else {
            XCTFail("Could not load api-fixtures/v1/manifest.json")
            return []
        }
        return rawFixtures.compactMap(ManifestFixture.init)
    }()

    static func fixture(id: String) -> ManifestFixture? {
        fixtures.first { $0.id == id }
    }

    static let swiftRouteFixtures = fixtures.filter { fixture in
        fixture.hasRoute && fixture.consumers.contains { ["swift-core", "swift-ui"].contains($0) }
    }

    private static func manifestURL() -> URL? {
        guard let root = try? FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/manifest.json"]) else {
            return nil
        }
        return root.appendingPathComponent("api-fixtures/v1/manifest.json")
    }
}

private extension ManifestFixture {
    init?(_ raw: [String: Any]) {
        guard
            let id = raw["id"] as? String,
            let method = raw["method"] as? String,
            let path = raw["path"] as? String
        else {
            return nil
        }

        self.id = id
        self.method = method
        self.path = path
        query = raw["query"] as? [String: String] ?? [:]
        requestBody = raw["requestBody"]
        consumers = raw["consumers"] as? [String] ?? []
        hasRoute = raw["route"] is [String: Any]
    }
}

private func assertManifestEndpoint(
    _ endpoint: Endpoint,
    matches fixture: ManifestFixture,
    file: StaticString = #filePath,
    line: UInt = #line
) throws {
    XCTAssertEqual(endpoint.method.rawValue, fixture.method, file: file, line: line)
    XCTAssertEqual(endpoint.path, fixture.path, file: file, line: line)

    for (name, value) in fixture.query {
        XCTAssertTrue(
            endpoint.queryItems.contains(where: { $0.name == name && $0.value == value }),
            "Missing query item \(name)=\(value) for \(fixture.id)",
            file: file,
            line: line
        )
    }

    guard let expectedBody = fixture.requestBody else {
        return
    }

    let body = try XCTUnwrap(endpoint.body, "Missing request body for \(fixture.id)", file: file, line: line)
    let encodedBody = try encodedJSONObject(from: body, keyEncodingStrategy: endpoint.bodyKeyEncodingStrategy)
    XCTAssertTrue(
        jsonObject(encodedBody, contains: expectedBody),
        "Request body for \(fixture.id) does not match manifest shape",
        file: file,
        line: line
    )
}

private func encodedJSONObject(
    from body: any Encodable,
    keyEncodingStrategy: EndpointBodyKeyEncodingStrategy
) throws -> Any {
    let encoder = JSONEncoder()
    if keyEncodingStrategy == .convertToSnakeCase {
        encoder.keyEncodingStrategy = .convertToSnakeCase
    }
    let data = try encoder.encode(body)
    return try JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed])
}

private func jsonObject(_ lhs: Any, contains rhs: Any) -> Bool {
    switch (lhs, rhs) {
    case let (lhs as [String: Any], rhs as [String: Any]):
        return rhs.allSatisfy { key, expectedValue in
            guard let actualValue = lhs[key] else { return false }
            return jsonObject(actualValue, contains: expectedValue)
        }
    case let (lhs as [Any], rhs as [Any]):
        guard lhs.count == rhs.count else { return false }
        return zip(lhs, rhs).allSatisfy { jsonObject($0, contains: $1) }
    case let (lhs as String, rhs as String):
        return lhs == rhs
    case let (lhs as NSNumber, rhs as NSNumber):
        return lhs == rhs
    case (_ as NSNull, _ as NSNull):
        return true
    default:
        return false
    }
}
