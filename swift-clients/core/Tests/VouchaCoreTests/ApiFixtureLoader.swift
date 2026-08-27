import Foundation
import XCTest

// Duplicated across Swift test targets so each package stays independently testable.

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
            let url = ancestorPath(withRelativePath: "api-fixtures/v1/manifest.json", startingFrom: #filePath),
            let data = try? Data(contentsOf: url),
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
        if let found = ancestorPath(withRelativePath: "api-fixtures/v1/\(bodyFile)", startingFrom: file) {
            return found
        }

        XCTFail("Could not locate repo root for API fixture \(bodyFile)", file: file, line: line)
        return FileManager.default.temporaryDirectory
            .appendingPathComponent("missing-api-fixture-\(UUID().uuidString).json")
    }

    private static func ancestorPath(withRelativePath relativePath: String, startingFrom file: StaticString) -> URL? {
        for root in repoRootCandidates(startingFrom: file) {
            let candidate = root.appendingPathComponent(relativePath)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return candidate
            }
        }
        return nil
    }

    private static func repoRootCandidates(startingFrom file: StaticString) -> [URL] {
        let filePath = "\(file)"
        var roots: [URL] = []

        if let githubWorkspace = ProcessInfo.processInfo.environment["GITHUB_WORKSPACE"] {
            roots.append(URL(fileURLWithPath: githubWorkspace))
        }

        let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        roots.append(currentDirectory)
        roots.append(contentsOf: ancestors(of: currentDirectory))

        let fileURL = URL(fileURLWithPath: filePath, relativeTo: currentDirectory).standardizedFileURL
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
