import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointManifestCoverageTests: XCTestCase {
    func testEveryManifestFixtureIsRepresentedInTheSwiftCoverageRegistry() {
        let manifestIds = Set(ManifestLoader.fixtures.map(\.id))
        let registeredIds = Set(EndpointManifestCoverage.registry.map(\.id))
        let duplicateIds = Dictionary(grouping: EndpointManifestCoverage.registry, by: \.id)
            .filter { $0.value.count > 1 }
            .keys.sorted()
        let nonSwiftIds = EndpointManifestCoverage.nonSwiftManifestFixtureIds
        let accountedFor = registeredIds.union(nonSwiftIds)

        XCTAssertTrue(duplicateIds.isEmpty, "Duplicate Swift endpoint registrations: \(duplicateIds)")

        let conflictingIds = registeredIds.intersection(nonSwiftIds)
        XCTAssertTrue(
            conflictingIds.isEmpty,
            "Fixtures cannot be both Swift endpoint registrations and explicitly non-Swift: " +
                "\(conflictingIds.sorted())"
        )

        let unaccounted = manifestIds.subtracting(accountedFor)
        XCTAssertTrue(
            unaccounted.isEmpty,
            "Fixtures in api-fixtures/v1/manifest.json that are not represented in the Swift coverage registry: " +
                "\(unaccounted.sorted())"
        )

        let stale = accountedFor.subtracting(manifestIds)
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

    private static func manifestURL() -> URL? {
        for root in repoRootCandidates() {
            let candidate = root.appendingPathComponent("api-fixtures/v1/manifest.json")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return candidate
            }
        }
        return nil
    }

    private static func repoRootCandidates() -> [URL] {
        var roots: [URL] = []

        if let githubWorkspace = ProcessInfo.processInfo.environment["GITHUB_WORKSPACE"] {
            roots.append(URL(fileURLWithPath: githubWorkspace))
        }

        let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        roots.append(currentDirectory)
        roots.append(contentsOf: ancestors(of: currentDirectory))

        let fileURL = URL(fileURLWithPath: #filePath, relativeTo: currentDirectory).standardizedFileURL
        roots.append(fileURL.deletingLastPathComponent())
        roots.append(contentsOf: ancestors(of: fileURL.deletingLastPathComponent()))

        return roots
    }

    private static func ancestors(of url: URL) -> [URL] {
        var directory = url
        var result: [URL] = []
        for _ in 0 ..< 16 {
            directory.deleteLastPathComponent()
            result.append(directory)
        }
        return result
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
