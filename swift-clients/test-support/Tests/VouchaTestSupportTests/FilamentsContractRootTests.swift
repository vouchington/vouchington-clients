import Foundation
@testable import VouchaTestSupport
import XCTest

final class FilamentsContractRootTests: XCTestCase {
    func testRequiresExplicitEnvironment() {
        XCTAssertThrowsError(try FilamentsContractRoot.url(environment: [:])) { error in
            XCTAssertEqual(
                String(describing: error),
                "Missing required VOUCHA_FILAMENTS_CONTRACT_ROOT. Fetch Filaments contracts before running native contract tests."
            )
        }
    }

    func testFixtureURLRejectsEscapePathsAndSymbolicLinks() throws {
        let root = try makeContractRoot()
        defer { try? FileManager.default.removeItem(at: root) }

        for path in ["/outside.json", "../outside.json", "nested/../../outside.json", "nested\\fixture.json"] {
            XCTAssertThrowsError(try FilamentsContractRoot.fixtureURL(path, root: root), path)
        }

        let fixtures = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        let outside = root.appendingPathComponent("outside.json")
        try Data("{}".utf8).write(to: outside)
        try FileManager.default.createSymbolicLink(
            at: fixtures.appendingPathComponent("linked.json"),
            withDestinationURL: outside
        )
        XCTAssertThrowsError(try FilamentsContractRoot.fixtureURL("linked.json", root: root)) { error in
            XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
        }
    }

    private func makeContractRoot() throws -> URL {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("filaments-contract-root-\(UUID().uuidString)", isDirectory: true)
        let fixtures = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        try FileManager.default.createDirectory(at: fixtures, withIntermediateDirectories: true)
        try Data("{}".utf8).write(to: fixtures.appendingPathComponent("manifest.json"))
        try Data("{}".utf8).write(to: fixtures.appendingPathComponent("fixture.json"))
        return root
    }
}
