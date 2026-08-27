import Foundation
import XCTest

// Duplicated across Swift test targets so each package stays independently testable.

enum FilamentsContractRoot {
    static let environmentKey = "VOUCHA_FILAMENTS_CONTRACT_ROOT"

    static func url(
        environment: [String: String] = ProcessInfo.processInfo.environment,
        requiredPaths: [String] = []
    ) throws -> URL {
        guard
            let value = environment[environmentKey]?.trimmingCharacters(in: .whitespacesAndNewlines),
            !value.isEmpty
        else {
            throw FilamentsContractRootError.missingEnvironment(environmentKey)
        }

        let root = URL(fileURLWithPath: value).standardizedFileURL
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: root.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw FilamentsContractRootError.invalidRoot(root.path)
        }

        for requiredPath in requiredPaths {
            let candidate = root.appendingPathComponent(requiredPath)
            guard FileManager.default.fileExists(atPath: candidate.path) else {
                throw FilamentsContractRootError.missingPath(requiredPath)
            }
        }

        return root
    }
}

enum FilamentsContractRootError: Error, CustomStringConvertible {
    case missingEnvironment(String)
    case invalidRoot(String)
    case missingPath(String)

    var description: String {
        switch self {
        case let .missingEnvironment(key):
            "Missing required \(key). Fetch Filaments contracts before running native contract tests."
        case let .invalidRoot(path):
            "\(FilamentsContractRoot.environmentKey) must name an existing directory, got \(path)."
        case let .missingPath(path):
            "Filaments contract root is missing required path \(path)."
        }
    }
}

enum ApiFixtureLoader {
    static func data(
        _ id: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) -> Data {
        guard let entry = manifestEntry(for: id) else {
            XCTFail("\(id) is not declared in api-fixtures/v1/manifest.json.", file: file, line: line)
            return Data("{}".utf8)
        }
        let isSwiftConsumer = entry.consumers.contains("swift-core") || entry.consumers.contains("swift-ui")
        if !isSwiftConsumer {
            XCTFail("\(id) is not marked as a swift-core or swift-ui fixture consumer.", file: file, line: line)
        }

        guard let bodyFile = entry.bodyFile else {
            XCTFail("\(id) does not declare a response body.", file: file, line: line)
            return Data("{}".utf8)
        }

        let path = fixtureURL(bodyFile, file: file, line: line)
        do {
            return try Data(contentsOf: path)
        } catch {
            XCTFail("Failed to read API fixture \(id): \(error)", file: file, line: line)
            return Data("{}".utf8)
        }
    }

    /// Every fixture id this client consumes that has a response body (excludes 204/304
    /// no-content statuses). Feeds `ApiFixtureCoverageTests`'s coverage-gate, which asserts
    /// each id is accounted for in `ApiFixtureCoverage.registry`.
    static var swiftFixtureIdsWithBody: [String] {
        manifest
            .filter { entry in
                (entry.consumers.contains("swift-core") || entry.consumers.contains("swift-ui")) &&
                    entry.status != 204 && entry.status != 304
            }
            .map(\.id)
    }

    static var swiftRouteFixtures: [ApiFixtureEndpointEntry] {
        manifest
            .filter { entry in
                (entry.consumers.contains("swift-core") || entry.consumers.contains("swift-ui")) &&
                    entry.route != nil
            }
            .map { entry in
                ApiFixtureEndpointEntry(
                    id: entry.id,
                    method: entry.method,
                    path: entry.path,
                    query: entry.query ?? [:],
                    requestBody: entry.requestBody,
                    routeTemplate: entry.route?.routeTemplate ?? ""
                )
            }
    }

    private static func manifestEntry(for id: String) -> ApiFixtureManifestEntry? {
        manifest.first { $0.id == id }
    }

    private static let manifest: [ApiFixtureManifestEntry] = {
        guard
            let root = try? FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/manifest.json"]),
            let data = try? Data(contentsOf: root.appendingPathComponent("api-fixtures/v1/manifest.json")),
            let decoded = try? JSONDecoder().decode(ApiFixtureManifest.self, from: data)
        else {
            XCTFail("Could not load api-fixtures/v1/manifest.json")
            return []
        }
        return decoded.fixtures
    }()

    private static func fixtureURL(
        _ bodyFile: String,
        file: StaticString,
        line: UInt
    ) -> URL {
        do {
            let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/manifest.json"])
            let fixture = root.appendingPathComponent("api-fixtures/v1/\(bodyFile)")
            guard FileManager.default.fileExists(atPath: fixture.path) else {
                throw FilamentsContractRootError.missingPath("api-fixtures/v1/\(bodyFile)")
            }
            return fixture
        } catch {
            XCTFail("Could not load API fixture \(bodyFile): \(error)", file: file, line: line)
        }

        return FileManager.default.temporaryDirectory
            .appendingPathComponent("missing-api-fixture-\(UUID().uuidString).json")
    }
}

private struct ApiFixtureManifest: Decodable {
    let fixtures: [ApiFixtureManifestEntry]
}

struct ApiFixtureEndpointEntry {
    let id: String
    let method: String
    let path: String
    let query: [String: String]
    let requestBody: JSONValue?
    let routeTemplate: String
}

private struct ApiFixtureManifestEntry: Decodable {
    let id: String
    let method: String
    let path: String
    let query: [String: String]?
    let requestBody: JSONValue?
    let route: ApiFixtureRouteContract?
    let bodyFile: String?
    let consumers: [String]
    let status: Int
}

private struct ApiFixtureRouteContract: Decodable {
    let routeTemplate: String
}

enum JSONValue: Decodable, Equatable {
    case null
    case bool(Bool)
    case number(Double)
    case string(String)
    case array([JSONValue])
    case object([String: JSONValue])

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([JSONValue].self) {
            self = .array(value)
        } else {
            self = try .object(container.decode([String: JSONValue].self))
        }
    }
}
