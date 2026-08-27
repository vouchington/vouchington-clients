import Foundation
import XCTest

// Duplicated across Swift test targets so each package stays independently testable.

private enum FilamentsContractRoot {
    static func url() throws -> URL {
        let environment = ProcessInfo.processInfo.environment
        let configuredValue = environment["VOUCHA_FILAMENTS_CONTRACT_ROOT"]?.trimmingCharacters(
            in: .whitespacesAndNewlines
        )
        guard
            let value = configuredValue,
            !value.isEmpty
        else {
            throw ContractRootError.missingEnvironment
        }

        let root = URL(fileURLWithPath: value).standardizedFileURL
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: root.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw ContractRootError.invalidRoot(root.path)
        }
        guard FileManager.default.fileExists(
            atPath: root.appendingPathComponent("api-fixtures/v1/manifest.json").path
        ) else {
            throw ContractRootError.missingManifest
        }
        return root
    }
}

private enum ContractRootError: Error, CustomStringConvertible {
    case missingEnvironment
    case invalidRoot(String)
    case missingManifest

    var description: String {
        switch self {
        case .missingEnvironment: "Missing required VOUCHA_FILAMENTS_CONTRACT_ROOT. Fetch Filaments contracts before running native contract tests."
        case let .invalidRoot(path): "VOUCHA_FILAMENTS_CONTRACT_ROOT must name an existing directory, got \(path)."
        case .missingManifest: "Filaments contract root is missing api-fixtures/v1/manifest.json."
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
            let root = try? FilamentsContractRoot.url(),
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
            let fixture = try FilamentsContractRoot.url().appendingPathComponent("api-fixtures/v1/\(bodyFile)")
            guard FileManager.default.fileExists(atPath: fixture.path) else {
                throw ContractRootError.missingManifest
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

private struct ApiFixtureManifestEntry: Decodable {
    let id: String
    let bodyFile: String?
    let consumers: [String]
}
