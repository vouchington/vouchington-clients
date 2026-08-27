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
    case invalidFixturePath(String)
    case symbolicLink(String)

    var description: String {
        switch self {
        case let .missingEnvironment(key):
            "Missing required \(key). Fetch Filaments contracts before running native contract tests."
        case let .invalidRoot(path):
            "\(FilamentsContractRoot.environmentKey) must name an existing directory, got \(path)."
        case let .missingPath(path):
            "Filaments contract root is missing required path \(path)."
        case let .invalidFixturePath(path):
            "API fixture bodyFile must be a relative path beneath api-fixtures/v1, got \(path)."
        case let .symbolicLink(path):
            "API fixture paths must not contain symbolic links, found \(path)."
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

    private static func manifestEntry(for id: String) -> ApiFixtureManifestEntry? {
        manifest.first { $0.id == id }
    }

    private static let manifest: [ApiFixtureManifestEntry] = {
        guard
            let manifestURL = try? fixtureURL("manifest.json"),
            let data = try? Data(contentsOf: manifestURL),
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
            return try fixtureURL(bodyFile)
        } catch {
            XCTFail("Could not load API fixture \(bodyFile): \(error)", file: file, line: line)
        }
        return FileManager.default.temporaryDirectory
            .appendingPathComponent("missing-api-fixture-\(UUID().uuidString).json")
    }

    static func fixtureURL(_ bodyFile: String) throws -> URL {
        guard isRelativeFixturePath(bodyFile) else {
            throw FilamentsContractRootError.invalidFixturePath(bodyFile)
        }

        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/manifest.json"])
        let fixtureRoot = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        let fixture = fixtureRoot.appendingPathComponent(bodyFile)
        let components = ["api-fixtures", "v1"] + bodyFile.split(separator: "/").map(String.init)
        try rejectSymbolicLinks(beneath: root, components: components)

        guard FileManager.default.fileExists(atPath: fixture.path) else {
            throw FilamentsContractRootError.missingPath("api-fixtures/v1/\(bodyFile)")
        }

        let resolvedRoot = fixtureRoot.resolvingSymlinksInPath().standardizedFileURL
        let resolvedFixture = fixture.resolvingSymlinksInPath().standardizedFileURL
        guard isDescendant(resolvedFixture, of: resolvedRoot) else {
            throw FilamentsContractRootError.invalidFixturePath(bodyFile)
        }
        return fixture
    }

    private static func isRelativeFixturePath(_ path: String) -> Bool {
        !path.isEmpty &&
            !path.hasPrefix("/") &&
            !path.hasPrefix("\\") &&
            path.split(separator: "/", omittingEmptySubsequences: false).allSatisfy { component in
                !component.isEmpty && component != "." && component != ".."
            }
    }

    private static func rejectSymbolicLinks(beneath root: URL, components: [String]) throws {
        if (try? FileManager.default.destinationOfSymbolicLink(atPath: root.path)) != nil {
            throw FilamentsContractRootError.symbolicLink(root.path)
        }

        var candidate = root
        for component in components {
            candidate.appendPathComponent(component)
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: candidate.path)) != nil {
                throw FilamentsContractRootError.symbolicLink(candidate.path)
            }
        }
    }

    private static func isDescendant(_ candidate: URL, of root: URL) -> Bool {
        let rootComponents = root.pathComponents
        let candidateComponents = candidate.pathComponents
        return candidateComponents.count > rootComponents.count &&
            candidateComponents.starts(with: rootComponents)
    }
}

private struct ApiFixtureManifest: Decodable {
    let fixtures: [ApiFixtureManifestEntry]
}

private struct ApiFixtureManifestEntry: Decodable {
    let id: String
    let bodyFile: String?
    let consumers: [String]
}
