import Foundation
@testable import VouchaTestSupport
import XCTest

final class FilamentsApiContractRootTests: XCTestCase {
    func testContractURLReadsWithoutResponseFixturesAndRejectsMissingPaths() throws {
        let root = try makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        let directory = root.appendingPathComponent("api-contracts/v1")
        XCTAssertThrowsError(try FilamentsContractRoot.apiContractURL("request-contracts.json", root: root))
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let file = directory.appendingPathComponent("request-contracts.json")
        try Data("contract".utf8).write(to: file)
        let loaded = try FilamentsContractRoot.apiContractURL("request-contracts.json", root: root)
        XCTAssertEqual(try Data(contentsOf: loaded), Data("contract".utf8))
        XCTAssertThrowsError(try FilamentsContractRoot.apiContractURL("nested/missing.json", root: root))
    }

    func testContractURLRejectsUnsafePaths() throws {
        let root = try makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        for path in [
            "",
            "/outside.json",
            "../outside.json",
            "nested/../../outside.json",
            "nested\\contract.json",
            "C:contract.json",
            "nested//contract.json"
        ] {
            XCTAssertThrowsError(try FilamentsContractRoot.apiContractURL(path, root: root), path)
        }
    }

    func testContractURLRejectsSymbolicLinksAtEveryComponent() throws {
        for component in [
            "root",
            "api-contracts",
            "api-contracts/v1",
            "api-contracts/v1/nested",
            "api-contracts/v1/nested/request-contracts.json"
        ] {
            let temporary = try makeRoot()
            defer { try? FileManager.default.removeItem(at: temporary) }
            let root = temporary.appendingPathComponent("root")
            let outside = temporary.appendingPathComponent("outside")
            let relative = "api-contracts/v1/nested"
            try FileManager.default.createDirectory(
                at: root.appendingPathComponent(relative),
                withIntermediateDirectories: true
            )
            try FileManager.default.createDirectory(
                at: outside.appendingPathComponent(relative),
                withIntermediateDirectories: true
            )
            let outsideFile = outside.appendingPathComponent("request-contracts.json")
            try Data("outside".utf8).write(to: outsideFile)
            let linked = component == "root" ? root : root.appendingPathComponent(component)
            let target: URL
            if component.hasSuffix(".json") {
                target = outsideFile
            } else {
                try FileManager.default.removeItem(at: linked)
                target = component == "root" ? outside : outside.appendingPathComponent(component)
            }
            try FileManager.default.createSymbolicLink(at: linked, withDestinationURL: target)
            XCTAssertThrowsError(try FilamentsContractRoot.apiContractURL(
                "nested/request-contracts.json",
                root: root
            )) { error in
                XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"), component)
            }
        }
    }

    private func makeRoot() throws -> URL {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("filaments-contracts-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        return root
    }
}
