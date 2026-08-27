import Foundation
import XCTest

final class LocalLLMEndpointPolicyContractTests: XCTestCase {
    func testRejectsSymbolicLinkedContractRootAndComponents() throws {
        let root = try makeContractRoot()
        defer { try? FileManager.default.removeItem(at: root) }

        let linkedRoot = root.appendingPathComponent("linked-root", isDirectory: true)
        try FileManager.default.createSymbolicLink(at: linkedRoot, withDestinationURL: root)
        XCTAssertThrowsError(try LocalLLMEndpointPolicyContract.load(root: linkedRoot)) { error in
            XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
        }

        let fixtures = root.appendingPathComponent("api-fixtures", isDirectory: true)
        let movedFixtures = root.appendingPathComponent("linked-api-fixtures", isDirectory: true)
        try FileManager.default.moveItem(at: fixtures, to: movedFixtures)
        try FileManager.default.createSymbolicLink(at: fixtures, withDestinationURL: movedFixtures)
        XCTAssertThrowsError(try LocalLLMEndpointPolicyContract.load(root: root)) { error in
            XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
        }
    }

    func testHostPolicyRowRejectsUnknownVerdict() {
        let json = Data(
            """
            {"id":"typo","requiredConsumers":["swift-android"],"endpoint":"http://192.168.1.20:11434","verdict":"rejetced","notes":"misspelled"}
            """.utf8
        )

        XCTAssertThrowsError(try JSONDecoder().decode(LocalLLMHostPolicyRow.self, from: json))
    }

    func testHostPolicyRowRejectsUnknownConsumer() {
        let json = Data(
            """
            {"id":"typo","requiredConsumers":["swift-croe"],"endpoint":"http://192.168.1.20:11434","verdict":"allowed","notes":"misspelled"}
            """.utf8
        )

        XCTAssertThrowsError(try JSONDecoder().decode(LocalLLMHostPolicyRow.self, from: json))
    }

    private func makeContractRoot() throws -> URL {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("filaments-android-contract-\(UUID().uuidString)", isDirectory: true)
        let fixtureRoot = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        try FileManager.default.createDirectory(at: fixtureRoot, withIntermediateDirectories: true)
        try Data("{\"hostPolicyRows\":[],\"originPairs\":[]}".utf8).write(
            to: fixtureRoot.appendingPathComponent("local-llm-endpoint-policy.json")
        )
        try Data("{}".utf8).write(
            to: fixtureRoot.appendingPathComponent("local-llm-endpoint-policy.schema.json")
        )
        return root
    }
}
