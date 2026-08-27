import Foundation
import VouchaFeatures
import XCTest

final class ClientIntentParityTests: XCTestCase {
    func testSharedApiFixtureMatchesDocsContract() throws {
        XCTAssertEqual(
            try ClientIntentParityContract.loadFixtureData(),
            try ClientIntentParityContract.loadDocsData()
        )
    }

    func testClientIntentVisibilityMatchesPersonaGates() throws {
        let contract = try ClientIntentParityContract.load()

        assertVisibleIntentIDsMatchContract(contract: contract, isSignedIn: false, userRoles: [])
        assertVisibleIntentIDsMatchContract(contract: contract, isSignedIn: true, userRoles: [])
        assertVisibleIntentIDsMatchContract(contract: contract, isSignedIn: true, userRoles: ["administrator"])
        assertVisibleIntentIDsMatchContract(contract: contract, isSignedIn: true, userRoles: ["investor"])
    }

    func testClientIntentContractMapsToRealSwiftSections() throws {
        let contract = try ClientIntentParityContract.load()
        let appSectionIDs = Set(AppSection.allCases.map(\.rawValue))

        XCTAssertFalse(contract.intents.isEmpty)

        for intent in contract.intents {
            XCTAssertTrue(
                appSectionIDs.contains(intent.swiftSection),
                "\(intent.id) maps to missing Swift AppSection \(intent.swiftSection)"
            )
        }
    }

    func testDirectSwiftSectionIntentAuthMatchesContract() throws {
        let contract = try ClientIntentParityContract.load()

        for intent in contract.intents where intent.id == intent.swiftSection {
            let section = try XCTUnwrap(AppSection(rawValue: intent.swiftSection))
            XCTAssertEqual(
                section.requiresAuth,
                intent.requiresAuth || !intent.roles.isEmpty,
                "\(intent.id) auth gate drifted"
            )
        }
    }

    private func assertVisibleIntentIDsMatchContract(
        contract: ClientIntentParityContract,
        isSignedIn: Bool,
        userRoles: [String],
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        let expected: [String] = contract.intents.compactMap { intent in
            if intent.requiresAuth, !isSignedIn {
                return nil
            }
            if !intent.roles.isEmpty, !intent.roles.contains(where: { userRoles.contains($0) }) {
                return nil
            }
            return intent.id
        }
        let actual: [String] = contract.intents.compactMap { intent in
            guard let section = AppSection(rawValue: intent.swiftSection),
                  section.isVisible(isSignedIn: isSignedIn, userRoles: userRoles)
            else {
                return nil
            }
            return intent.id
        }

        XCTAssertEqual(actual, expected, file: file, line: line)
    }
}

private struct ClientIntentParityContract: Decodable {
    let intents: [ClientIntentParityIntent]

    static func loadFixtureData(
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws -> Data {
        try Data(contentsOf: contractURL(path: "api-fixtures/v1/client-intents.json", file: file, line: line))
    }

    static func loadDocsData(
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws -> Data {
        try Data(
            contentsOf: contractURL(
                path: "docs/requirements/navigation/client-intent-parity.json",
                file: file,
                line: line
            )
        )
    }

    static func load(
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws -> ClientIntentParityContract {
        let data = try loadFixtureData(file: file, line: line)
        return try JSONDecoder().decode(ClientIntentParityContract.self, from: data)
    }

    private static func contractURL(
        path: String,
        file: StaticString,
        line: UInt
    ) -> URL {
        var directory = URL(fileURLWithPath: "\(file)").deletingLastPathComponent()
        for _ in 0 ..< 12 {
            let candidate = directory.appendingPathComponent(path)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return candidate
            }
            directory.deleteLastPathComponent()
        }

        XCTFail("Could not locate client intent parity contract at \(path)", file: file, line: line)
        return FileManager.default.temporaryDirectory
            .appendingPathComponent("missing-client-intent-parity-\(UUID().uuidString).json")
    }
}

private struct ClientIntentParityIntent: Decodable {
    let id: String
    let swiftSection: String
    let requiresAuth: Bool
    let roles: [String]
}
