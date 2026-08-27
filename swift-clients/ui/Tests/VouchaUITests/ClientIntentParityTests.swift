import Foundation
import VouchaFeatures
import VouchaTestSupport
import XCTest

final class ClientIntentParityTests: XCTestCase {
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

    static func loadFixtureData() throws -> Data {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/client-intents.json"])
        return try Data(contentsOf: root.appendingPathComponent("api-fixtures/v1/client-intents.json"))
    }

    static func load() throws -> ClientIntentParityContract {
        let data = try loadFixtureData()
        return try JSONDecoder().decode(ClientIntentParityContract.self, from: data)
    }

}

private struct ClientIntentParityIntent: Decodable {
    let id: String
    let swiftSection: String
    let requiresAuth: Bool
    let roles: [String]
}
